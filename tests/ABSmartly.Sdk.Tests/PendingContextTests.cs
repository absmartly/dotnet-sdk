using System.Net;
using ABSmartly.Models;
using ABSmartly.Services;

namespace ABSmartly.Sdk.Tests;

[TestFixture]
public class PendingContextTests
{
    private readonly Dictionary<string, string> _units = new() { ["session_id"] = "e791e240fcd3df7d238cfc285f475e8152fcc0ec" };

    private ContextData _data = null!;
    private RecordingLogger _logger = null!;
    private IContextPublisher _publisher = null!;
    private List<PublishEvent> _published = null!;

    [SetUp]
    public void SetUp()
    {
        using var stream =
            GetType().Assembly.GetManifestResourceStream("ABSmartly.Sdk.Tests.Resources.context.json");
        _data = new DefaultContextDataDeserializer().Deserialize(stream);

        _logger = new RecordingLogger();
        _published = new List<PublishEvent>();
        _publisher = Mock.Of<IContextPublisher>();
        Mock.Get(_publisher)
            .Setup(x => x.PublishAsync(It.IsAny<IContext>(), It.IsAny<PublishEvent>()))
            .Callback<IContext, PublishEvent>((_, e) => _published.Add(e))
            .Returns(Task.CompletedTask);
    }

    [Test]
    public async Task TrackBeforeReadyQueuesGoalAndPublishesItOnceReady()
    {
        var gate = new TaskCompletionSource<ContextData>(TaskCreationOptions.RunContinuationsAsynchronously);
        var sdk = BuildSdk(gate.Task);

        var context = sdk.CreatePendingContext(Config());

        context.IsReady().Should().BeFalse();
        context.IsFailed().Should().BeFalse();
        context.IsFinalized.Should().BeFalse();
        _logger.Types.Should().BeEmpty("no ready/error event before the data arrives");

        context.Track("purchase", new Dictionary<string, object> { ["amount"] = 125.5 });

        context.PendingCount.Should().Be(1);
        _logger.Types.Should().Equal(EventType.Goal);

        gate.SetResult(_data);
        await context.WaitUntilReadyAsync();
        // Readiness completes before the Ready event is logged (so a Ready callback may
        // wait on it); wait for the load to settle before checking the logged events.
        await ((Context)context).WaitUntilLoadSettledAsync();

        context.IsReady().Should().BeTrue();
        context.IsFailed().Should().BeFalse();
        context.PendingCount.Should().Be(1);
        _logger.Types.Should().Equal(EventType.Goal, EventType.Ready);

        await context.PublishAsync();

        context.PendingCount.Should().Be(0);
        _published.Should().ContainSingle();
        _published[0].Goals.Should().ContainSingle().Which.Name.Should().Be("purchase");
    }

    [Test]
    public async Task ReadMethodsBeforeReadyThrowWithoutExposure()
    {
        var gate = new TaskCompletionSource<ContextData>(TaskCreationOptions.RunContinuationsAsynchronously);
        var sdk = BuildSdk(gate.Task);

        var context = (Context)sdk.CreatePendingContext(Config());

        context.IsReady().Should().BeFalse();

        var reads = new Dictionary<string, Action>
        {
            ["GetTreatment"] = () => context.GetTreatment("exp_test_ab"),
            ["PeekTreatment"] = () => context.PeekTreatment("exp_test_ab"),
            ["GetVariableValue"] = () => context.GetVariableValue("banner.border", "blue"),
            ["PeekVariableValue"] = () => context.PeekVariableValue("banner.border", "blue"),
            ["VariableKeys"] = () => _ = context.VariableKeys,
            ["Experiments"] = () => _ = context.Experiments,
            ["GetCustomFieldKeys"] = () => context.GetCustomFieldKeys(),
            ["GetCustomFieldValue"] = () => context.GetCustomFieldValue("exp_test_ab", "country"),
            ["GetCustomFieldValueType"] = () => context.GetCustomFieldValueType("exp_test_ab", "country"),
        };

        foreach (var (name, read) in reads)
            read.Should().Throw<InvalidOperationException>(name)
                .Which.Message.Should().Be("ABSmartly Context is not yet ready", name);

        context.PendingCount.Should().Be(0, "a not-ready read must not queue an exposure");
        _logger.Types.Should().BeEmpty();

        gate.SetResult(_data);
        await context.WaitUntilReadyAsync();

        context.GetTreatment("exp_test_ab").Should().Be(1);
        context.PendingCount.Should().Be(1);
    }

    [Test]
    public void ReadMethodsOnFinalizedContextStillThrow()
    {
        var sdk = BuildSdk(Task.FromResult(_data));
        var context = sdk.CreatePendingContext(Config()).WaitUntilReady();
        context.Close();

        var act = () => context.GetTreatment("exp_test_ab");
        act.Should().Throw<InvalidOperationException>().WithMessage("ABSmartly Context is closed");
    }

    [Test]
    public void ContextDataAccessBeforeReadyStillThrows()
    {
        var gate = new TaskCompletionSource<ContextData>();
        var sdk = BuildSdk(gate.Task);

        var context = sdk.CreatePendingContext(Config());

        var act = () => context.GetContextData();
        act.Should().Throw<InvalidOperationException>().WithMessage("*not yet ready*");

        gate.SetResult(_data);
    }

    [Test]
    public async Task UnitsAttributesAndOverridesSetBeforeReadyApplyOnceReady()
    {
        var gate = new TaskCompletionSource<ContextData>(TaskCreationOptions.RunContinuationsAsynchronously);
        var sdk = BuildSdk(gate.Task);

        var context = sdk.CreatePendingContext(new ContextConfig { PublishDelay = TimeSpan.FromMilliseconds(-1) });
        context.SetUnits(_units);
        context.SetAttribute("user_agent", "Chrome");
        context.SetOverride("exp_test_abc", 2);

        gate.SetResult(_data);
        await context.WaitUntilReadyAsync();

        context.Units.Should().BeEquivalentTo(_units);
        context.GetAttribute("user_agent").Should().Be("Chrome");
        context.GetTreatment("exp_test_ab").Should().Be(1);
        context.GetTreatment("exp_test_abc").Should().Be(2);
    }

    [Test]
    public async Task FailedLoadExposesRealErrorAsReadyError()
    {
        var error = new HttpRequestException("connection refused");
        var sdk = BuildSdk(Task.FromException<ContextData>(error));

        var context = (IContextV2)await sdk.CreateContextAsync(Config());

        context.IsReady().Should().BeTrue();
        context.IsFailed().Should().BeTrue();
        context.ReadyError.Should().BeSameAs(error);
        _logger.Events.Should().ContainSingle(e => e.Type == EventType.Error)
            .Which.Data.Should().Be("connection refused");

        context.GetTreatment("exp_test_ab").Should().Be(0);
        context.Experiments.Should().BeEmpty();
    }

    [Test]
    public void FailedLoadDoesNotThrowFromSyncCreateContext()
    {
        var sdk = BuildSdk(Task.FromException<ContextData>(new HttpRequestException("boom")));

        var context = (IContextV2)sdk.CreateContext(Config());

        context.IsFailed().Should().BeTrue();
        context.ReadyError.Should().BeOfType<HttpRequestException>().Which.Message.Should().Be("boom");
    }

    [Test]
    public async Task PendingContextBecomesFailedWithErrorWhenLoadFails()
    {
        var gate = new TaskCompletionSource<ContextData>(TaskCreationOptions.RunContinuationsAsynchronously);
        var sdk = BuildSdk(gate.Task);

        var context = sdk.CreatePendingContext(Config());
        context.IsReady().Should().BeFalse();
        context.ReadyError.Should().BeNull();

        var error = new TimeoutException("collector timed out");
        gate.SetException(error);
        (await context.WaitUntilReadyAsync()).Should().BeSameAs(context);

        context.IsFailed().Should().BeTrue();
        context.ReadyError.Should().BeSameAs(error);
    }

    // The default HTTP provider: a refused connection must surface as ReadyError,
    // not be swallowed into a null-data failure with no error.
    [Test]
    public async Task DefaultHttpProviderFetchFailureSetsReadyError()
    {
        var httpClient = Mock.Of<IABsmartlyHttpClient>();
        Mock.Get(httpClient).Setup(x => x.GetAsync(It.IsAny<string>()))
            .ThrowsAsync(new HttpRequestException("Connection refused (127.0.0.1:9)"));
        var factory = Mock.Of<IABsmartlyHttpClientFactory>(x => x.CreateClient() == httpClient);
        var sdk = new ABsmartly.Sdk(factory, H.ServiceConfig("website", "http://127.0.0.1:9", "dev", "key"),
            new ABsmartlyConfig { ContextEventLogger = _logger, ContextPublisher = _publisher });

        var context = (IContextV2)await sdk.CreateContextAsync(Config());

        context.IsFailed().Should().BeTrue();
        context.ReadyError.Should().BeOfType<HttpRequestException>()
            .Which.Message.Should().Contain("Connection refused");
        _logger.Types.Should().Equal(EventType.Error);
    }

    [Test]
    public async Task DefaultHttpProviderHttpErrorStatusSetsReadyError()
    {
        var httpClient = Mock.Of<IABsmartlyHttpClient>();
        Mock.Get(httpClient).Setup(x => x.GetAsync(It.IsAny<string>()))
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.InternalServerError));
        var factory = Mock.Of<IABsmartlyHttpClientFactory>(x => x.CreateClient() == httpClient);
        var sdk = new ABsmartly.Sdk(factory, H.ServiceConfig("website", "http://127.0.0.1:9", "dev", "key"),
            new ABsmartlyConfig { ContextEventLogger = _logger, ContextPublisher = _publisher });

        var context = (IContextV2)await sdk.CreateContextAsync(Config());

        context.IsFailed().Should().BeTrue();
        context.ReadyError.Should().BeOfType<HttpRequestException>();
    }

    [Test]
    public async Task DefaultHttpProviderMalformedBodySetsReadyError()
    {
        // The default deserializer returns null on malformed JSON (as 1.3.0 and Java);
        // the context still fails with a meaningful error instead of hanging or an NRE.
        var httpClient = Mock.Of<IABsmartlyHttpClient>();
        Mock.Get(httpClient).Setup(x => x.GetAsync(It.IsAny<string>()))
            .ReturnsAsync(() => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{broken") });
        var factory = Mock.Of<IABsmartlyHttpClientFactory>(x => x.CreateClient() == httpClient);
        var sdk = new ABsmartly.Sdk(factory, H.ServiceConfig("website", "http://127.0.0.1:9", "dev", "key"),
            new ABsmartlyConfig { ContextEventLogger = _logger, ContextPublisher = _publisher });

        var context = (IContextV2)await sdk.CreateContextAsync(Config());

        context.IsFailed().Should().BeTrue();
        context.ReadyError.Should().BeOfType<InvalidOperationException>()
            .Which.Message.Should().Contain("deserializer returned null");
    }

    [Test]
    public async Task RefreshRethrowsRealFetchError()
    {
        var calls = 0;
        var provider = Mock.Of<IContextDataProvider>();
        Mock.Get(provider).Setup(x => x.GetContextDataAsync()).Returns(() =>
            ++calls == 1
                ? Task.FromResult(_data)
                : Task.FromException<ContextData>(new HttpRequestException("refresh failed")));
        var sdk = BuildSdk(provider);

        var context = (IContextV2)await sdk.CreateContextAsync(Config());
        context.IsReady().Should().BeTrue();

        var act = async () => await context.RefreshAsync();

        await act.Should().ThrowAsync<HttpRequestException>().WithMessage("refresh failed");
        context.IsFailed().Should().BeFalse("a failed refresh keeps the data already loaded");
    }

    // CreateContext blocks via a pumped synchronization context; the background load
    // must not post continuations back to it, or this would deadlock.
    [Test]
    [Timeout(5000)]
    public void SyncCreateContextWithSlowProviderDoesNotDeadlock()
    {
        var provider = Mock.Of<IContextDataProvider>();
        Mock.Get(provider).Setup(x => x.GetContextDataAsync()).Returns(async () =>
        {
            await Task.Delay(50);
            return _data;
        });
        var sdk = BuildSdk(provider);

        var context = (IContextV2)sdk.CreateContext(Config());

        context.IsReady().Should().BeTrue();
        context.IsFailed().Should().BeFalse();
        context.GetTreatment("exp_test_ab").Should().Be(1);
    }

    [Test]
    [Timeout(5000)]
    public void WaitUntilReadyBlocksUntilLoadSettles()
    {
        var gate = new TaskCompletionSource<ContextData>(TaskCreationOptions.RunContinuationsAsynchronously);
        var sdk = BuildSdk(gate.Task);
        var context = sdk.CreatePendingContext(Config());

        _ = Task.Run(async () =>
        {
            await Task.Delay(50);
            gate.SetResult(_data);
        });

        context.WaitUntilReady().Should().BeSameAs(context);
        context.IsReady().Should().BeTrue();
    }

    [Test]
    public async Task ClosingBeforeLoadSettlesDropsLateDataWithoutError()
    {
        var gate = new TaskCompletionSource<ContextData>(TaskCreationOptions.RunContinuationsAsynchronously);
        var sdk = BuildSdk(gate.Task);
        var context = sdk.CreatePendingContext(Config());

        await ((Context)context).DisposeAsync();
        context.IsFinalized.Should().BeTrue();

        gate.SetResult(_data);
        await context.WaitUntilReadyAsync();

        context.IsReady().Should().BeFalse();
        _logger.Types.Should().Equal(EventType.Close);
    }

    [Test]
    [Timeout(5000)]
    public async Task LoggerClosingFromPublishCallbackDoesNotDeadlock()
    {
        var sdk = BuildSdk(Task.FromResult(_data));
        var logger = new CallbackLogger();
        var config = Config();
        config.ContextEventLogger = logger;
        var context = (IContextV2)sdk.CreateContextWith(config, _data);
        logger.OnEvent = (ctx, type) =>
        {
            if (type == EventType.Publish) ((IContextV2)ctx).Close();
        };
        context.Track("purchase", null);

        var dispose = ((Context)context).DisposeAsync().AsTask();
        (await Task.WhenAny(dispose, Task.Delay(3000))).Should().BeSameAs(dispose);
        await dispose;

        context.IsFinalized.Should().BeTrue();
        _published.Should().ContainSingle();
    }

    [Test]
    [Timeout(5000)]
    public async Task FailedCloseKeepsPendingInitialLoad()
    {
        var gate = new TaskCompletionSource<ContextData>(TaskCreationOptions.RunContinuationsAsynchronously);
        var publish = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var sdk = BuildSdk(gate.Task);
        Mock.Get(_publisher)
            .Setup(x => x.PublishAsync(It.IsAny<IContext>(), It.IsAny<PublishEvent>()))
            .Returns(publish.Task);
        var context = sdk.CreatePendingContext(Config());
        context.Track("purchase", null);

        var dispose = ((Context)context).DisposeAsync().AsTask();
        gate.SetResult(_data);
        await context.WaitUntilReadyAsync();
        context.IsReady().Should().BeTrue();
        publish.SetException(new InvalidOperationException("publish failed"));

        Assert.ThrowsAsync<InvalidOperationException>(() => dispose);
        await context.WaitUntilReadyAsync();

        context.IsFinalized.Should().BeFalse();
        context.IsReady().Should().BeTrue();
        context.IsFailed().Should().BeFalse();
        context.Experiments.Should().NotBeEmpty();
        context.PendingCount.Should().Be(1);
    }

    [Test]
    [Timeout(5000)]
    public async Task FailedCloseStaysOpenWithoutAutomaticRefresh()
    {
        // As Context.java closeAsync(): the refresh timer is cleared when the close
        // starts and a failed close leaves the context open for a retry, without
        // re-arming background refresh.
        var fetches = 0;
        var provider = Mock.Of<IContextDataProvider>();
        Mock.Get(provider).Setup(x => x.GetContextDataAsync())
            .Returns(() =>
            {
                Interlocked.Increment(ref fetches);
                return Task.FromResult(_data);
            });
        Mock.Get(_publisher)
            .Setup(x => x.PublishAsync(It.IsAny<IContext>(), It.IsAny<PublishEvent>()))
            .Returns(Task.FromException(new InvalidOperationException("publish failed")));
        var sdk = BuildSdk(provider);
        var config = Config();
        config.RefreshInterval = TimeSpan.FromMilliseconds(20);
        var context = (IContextV2)sdk.CreateContextWith(config, _data);
        context.Track("purchase", null);

        Assert.ThrowsAsync<InvalidOperationException>(() => ((Context)context).DisposeAsync().AsTask());
        context.IsFinalized.Should().BeFalse();
        context.PendingCount.Should().Be(1, "the failed close keeps its events");

        var afterClose = Volatile.Read(ref fetches);
        await Task.Delay(300);
        Volatile.Read(ref fetches).Should().Be(afterClose, "the refresh timer is not re-armed");

        // Explicit refresh still works on the open context.
        await context.RefreshAsync();
        Volatile.Read(ref fetches).Should().Be(afterClose + 1);

        Mock.Get(_publisher)
            .Setup(x => x.PublishAsync(It.IsAny<IContext>(), It.IsAny<PublishEvent>()))
            .Returns(Task.CompletedTask);
        await ((Context)context).DisposeAsync();
        context.IsFinalized.Should().BeTrue();
    }

    [Test]
    [Timeout(5000)]
    [TestCase(false)]
    [TestCase(true)]
    public async Task ErrorLoggerWaitingForReadyDuringFailedCloseDoesNotDeadlock(bool fetchFails)
    {
        var gate = new TaskCompletionSource<ContextData>(TaskCreationOptions.RunContinuationsAsynchronously);
        var errorEntered = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        Mock.Get(_publisher)
            .Setup(x => x.PublishAsync(It.IsAny<IContext>(), It.IsAny<PublishEvent>()))
            .Returns(Task.FromException(new InvalidOperationException("publish failed")));
        var sdk = BuildSdk(gate.Task);
        var logger = new CallbackLogger();
        var config = Config();
        config.ContextEventLogger = logger;
        var context = sdk.CreatePendingContext(config);
        logger.OnEvent = (ctx, type) =>
        {
            if (type != EventType.Error) return;
            errorEntered.TrySetResult(true);
            ((IContextV2)ctx).WaitUntilReady();
        };
        context.Track("purchase", null);

        var dispose = Task.Run(() => ((Context)context).DisposeAsync().AsTask());
        await errorEntered.Task;
        if (fetchFails) gate.SetException(new InvalidOperationException("fetch failed"));
        else gate.SetResult(_data);

        (await Task.WhenAny(dispose, Task.Delay(3000))).Should().BeSameAs(dispose);
        Assert.ThrowsAsync<InvalidOperationException>(() => dispose);
        await context.WaitUntilReadyAsync();
        context.IsReady().Should().BeTrue();
        context.IsFailed().Should().Be(fetchFails);
        context.IsFinalized.Should().BeFalse();
        if (!fetchFails) context.PendingCount.Should().Be(1);
    }

    // A finalized context stays readable: units, attributes and experiments, as in
    // the JavaScript SDK. Finalize must not release the locks those reads take.
    [Test]
    public async Task FinalizeAsyncKeepsUnitAndAttributeReadsWorking()
    {
        var sdk = BuildSdk(Task.FromResult(_data));
        var context = (IContextV2)sdk.CreateContextWith(Config(), _data);
        context.SetAttribute("country", "PT");
        context.Track("purchase", null);

        await context.FinalizeAsync();

        context.IsFinalized.Should().BeTrue();
        _published.Should().HaveCount(1);
        _logger.Types.Last().Should().Be(EventType.Close);
        context.Units.Should().Equal(_units);
        context.GetAttribute("country").Should().Be("PT");
        context.Attributes.Should().ContainKey("country");
        context.Experiments.Should().NotBeEmpty();
        context.Invoking(c => c.GetTreatment("exp_test_ab")).Should()
            .Throw<InvalidOperationException>().WithMessage("ABSmartly Context is closed");
    }

    [Test]
    public void CloseKeepsUnitReadsWorking()
    {
        var sdk = BuildSdk(Task.FromResult(_data));
        var context = (IContextV2)sdk.CreateContextWith(Config(), _data);

        context.Close();

        context.IsFinalized.Should().BeTrue();
        context.Units.Should().Equal(_units);
    }

    [TestCase(false)]
    [TestCase(true)]
    [Timeout(5000)]
    public async Task CreateContextReturnsAfterTheLoadEventWasLogged(bool fetchFails)
    {
        var sdk = BuildSdk(fetchFails ? Task.FromException<ContextData>(new HttpRequestException("down")) : Task.FromResult(_data));
        var logger = new CallbackLogger();
        var logged = 0;
        logger.OnEvent = (_, type) =>
        {
            if (type is EventType.Ready or EventType.Error)
            {
                Thread.Sleep(200);
                Interlocked.Exchange(ref logged, 1);
            }
        };
        var config = Config();
        config.ContextEventLogger = logger;

        await sdk.CreateContextAsync(config);

        logged.Should().Be(1);
    }

    [Test]
    public void WaitUntilReadyOnReadyContextReturnsImmediately()
    {
        var sdk = BuildSdk(Task.FromResult(_data));
        var context = (IContextV2)sdk.CreateContextWith(Config(), _data);

        context.WaitUntilReady().Should().BeSameAs(context);
        context.WaitUntilReadyAsync().IsCompleted.Should().BeTrue();
    }

    // As 1.3.0: an event logger that throws on the Ready or Error event of context
    // creation makes CreateContext/CreateContextAsync throw that exception. Readiness
    // itself still completes.
    [TestCase(false, false)]
    [TestCase(false, true)]
    [TestCase(true, false)]
    [TestCase(true, true)]
    [Timeout(10000)]
    public async Task CreateContextRethrowsReadyOrErrorLoggerException(bool failedLoad, bool synchronous)
    {
        var provider = Mock.Of<IContextDataProvider>();
        Mock.Get(provider).Setup(x => x.GetContextDataAsync()).Returns(failedLoad
            ? Task.FromException<ContextData>(new InvalidOperationException("load failed"))
            : Task.FromResult(_data));
        var expected = failedLoad ? EventType.Error : EventType.Ready;
        var logger = new CallbackLogger
        {
            OnEvent = (_, type) =>
            {
                if (type == expected) throw new InvalidOperationException("sentinel-logger");
            }
        };
        var sdk = new ABsmartly.Sdk(Mock.Of<IABsmartlyHttpClientFactory>(),
            H.ServiceConfig("website", "https://localhost/v1", "dev", "key"),
            new ABsmartlyConfig { ContextDataProvider = provider, ContextEventLogger = logger, ContextPublisher = _publisher });

        Func<Task> create = synchronous
            ? () => Task.Run(() => sdk.CreateContext(Config()))
            : () => sdk.CreateContextAsync(Config());
        await create.Should().ThrowAsync<InvalidOperationException>().WithMessage("sentinel-logger");
    }

    // A pending context's readiness does not depend on the Ready callback succeeding,
    // and the callback's exception does not surface as an unobserved task exception.
    [Test]
    [Timeout(10000)]
    public async Task PendingContextReadinessIgnoresThrowingReadyLogger()
    {
        var provider = Mock.Of<IContextDataProvider>();
        Mock.Get(provider).Setup(x => x.GetContextDataAsync()).Returns(Task.FromResult(_data));
        var logger = new CallbackLogger
        {
            OnEvent = (_, type) =>
            {
                if (type == EventType.Ready) throw new InvalidOperationException("sentinel-logger");
            }
        };
        var sdk = new ABsmartly.Sdk(Mock.Of<IABsmartlyHttpClientFactory>(),
            H.ServiceConfig("website", "https://localhost/v1", "dev", "key"),
            new ABsmartlyConfig { ContextDataProvider = provider, ContextEventLogger = logger, ContextPublisher = _publisher });

        var unobserved = false;
        EventHandler<UnobservedTaskExceptionEventArgs> handler = (_, e) =>
        {
            if (e.Exception.InnerExceptions.Any(x => x.Message == "sentinel-logger")) unobserved = true;
        };
        TaskScheduler.UnobservedTaskException += handler;
        try
        {
            var context = sdk.CreatePendingContext(Config());
            (await context.WaitUntilReadyAsync()).IsReady().Should().BeTrue();
            context.GetTreatment("exp_test_ab").Should().Be(1);

            context = null!;
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            unobserved.Should().BeFalse();
        }
        finally
        {
            TaskScheduler.UnobservedTaskException -= handler;
        }
    }

    private ContextConfig Config() =>
        new ContextConfig { PublishDelay = TimeSpan.FromMilliseconds(-1) }.SetUnits(_units);

    private ABsmartly.Sdk BuildSdk(Task<ContextData> dataTask)
    {
        var provider = Mock.Of<IContextDataProvider>();
        Mock.Get(provider).Setup(x => x.GetContextDataAsync()).Returns(dataTask);
        return BuildSdk(provider);
    }

    private ABsmartly.Sdk BuildSdk(IContextDataProvider provider)
    {
        var factory = Mock.Of<IABsmartlyHttpClientFactory>();
        return new ABsmartly.Sdk(factory, H.ServiceConfig("website", "https://localhost/v1", "dev", "key"),
            new ABsmartlyConfig
            {
                ContextDataProvider = provider,
                ContextEventLogger = _logger,
                ContextPublisher = _publisher
            });
    }

    private sealed class CallbackLogger : IContextEventLogger
    {
        public Action<IContext, EventType>? OnEvent { get; set; }

        public void HandleEvent(IContext context, EventType eventType, object? data) => OnEvent?.Invoke(context, eventType);
    }

    private sealed class RecordingLogger : IContextEventLogger
    {
        private readonly object _lock = new();
        private readonly List<(EventType Type, object? Data)> _events = new();

        public List<(EventType Type, object? Data)> Events
        {
            get { lock (_lock) return _events.ToList(); }
        }

        public List<EventType> Types => Events.Select(e => e.Type).ToList();

        public void HandleEvent(IContext context, EventType eventType, object? data)
        {
            lock (_lock) _events.Add((eventType, data));
        }
    }
}
