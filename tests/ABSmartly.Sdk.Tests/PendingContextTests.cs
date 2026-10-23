using System.Net;
using ABSmartly.Models;
using ABSmartly.Services;

namespace ABSmartly.Sdk.Tests;

// Regression tests for the not-ready context state and the failed-load error.
//
// Before this fix, CreateContext/CreateContextAsync awaited the data fetch before
// constructing the Context, so no caller could observe a context that was not ready
// yet, and ABSmartlyService swallowed fetch errors and returned null, so ReadyError
// was never set. Cross-SDK scenarios 67, 200 and 201 cover this behaviour.
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

    // Scenario 67: track() before ready queues the goal; it is published once ready.
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

        context.IsReady().Should().BeTrue();
        context.IsFailed().Should().BeFalse();
        context.PendingCount.Should().Be(1);
        _logger.Types.Should().Equal(EventType.Goal, EventType.Ready);

        await context.PublishAsync();

        context.PendingCount.Should().Be(0);
        _published.Should().ContainSingle();
        _published[0].Goals.Should().ContainSingle().Which.Name.Should().Be("purchase");
    }

    // Scenario 201: read methods before ready throw "not yet ready" (matching the JS SDK)
    // and queue no exposures.
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
                .Which.Message.Should().Be("ABsmartly Context is not yet ready.", name);

        context.PendingCount.Should().Be(0, "a not-ready read must not queue an exposure");
        _logger.Types.Should().BeEmpty();

        // Once ready, the same call assigns and exposes for real.
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
        act.Should().Throw<InvalidOperationException>().WithMessage("*finalized*");
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

    // Scenario 200: a failed load sets IsFailed(), keeps the real error as
    // ReadyError, and logs an Error event carrying that error.
    [Test]
    public async Task FailedLoadExposesRealErrorAsReadyError()
    {
        var error = new HttpRequestException("connection refused");
        var sdk = BuildSdk(Task.FromException<ContextData>(error));

        var context = await sdk.CreateContextAsync(Config());

        context.IsReady().Should().BeTrue();
        context.IsFailed().Should().BeTrue();
        context.ReadyError.Should().BeSameAs(error);
        _logger.Events.Should().ContainSingle(e => e.Type == EventType.Error)
            .Which.Data.Should().Be("connection refused");

        // A failed context keeps returning defaults.
        context.GetTreatment("exp_test_ab").Should().Be(0);
        context.Experiments.Should().BeEmpty();
    }

    [Test]
    public void FailedLoadDoesNotThrowFromSyncCreateContext()
    {
        var sdk = BuildSdk(Task.FromException<ContextData>(new HttpRequestException("boom")));

        var context = sdk.CreateContext(Config());

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
        var sdk = new ABsmartly(factory, H.ServiceConfig("website", "http://127.0.0.1:9", "dev", "key"),
            new ABsmartlyConfig { ContextEventLogger = _logger, ContextPublisher = _publisher });

        var context = await sdk.CreateContextAsync(Config());

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
        var sdk = new ABsmartly(factory, H.ServiceConfig("website", "http://127.0.0.1:9", "dev", "key"),
            new ABsmartlyConfig { ContextEventLogger = _logger, ContextPublisher = _publisher });

        var context = await sdk.CreateContextAsync(Config());

        context.IsFailed().Should().BeTrue();
        context.ReadyError.Should().BeOfType<HttpRequestException>();
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

        var context = await sdk.CreateContextAsync(Config());
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

        var context = sdk.CreateContext(Config());

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
    public void WaitUntilReadyOnReadyContextReturnsImmediately()
    {
        var sdk = BuildSdk(Task.FromResult(_data));
        var context = sdk.CreateContextWith(Config(), _data);

        context.WaitUntilReady().Should().BeSameAs(context);
        context.WaitUntilReadyAsync().IsCompleted.Should().BeTrue();
    }

    private ContextConfig Config() =>
        new ContextConfig { PublishDelay = TimeSpan.FromMilliseconds(-1) }.SetUnits(_units);

    private ABsmartly BuildSdk(Task<ContextData> dataTask)
    {
        var provider = Mock.Of<IContextDataProvider>();
        Mock.Get(provider).Setup(x => x.GetContextDataAsync()).Returns(dataTask);
        return BuildSdk(provider);
    }

    private ABsmartly BuildSdk(IContextDataProvider provider)
    {
        var factory = Mock.Of<IABsmartlyHttpClientFactory>();
        return new ABsmartly(factory, H.ServiceConfig("website", "https://localhost/v1", "dev", "key"),
            new ABsmartlyConfig
            {
                ContextDataProvider = provider,
                ContextEventLogger = _logger,
                ContextPublisher = _publisher
            });
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
