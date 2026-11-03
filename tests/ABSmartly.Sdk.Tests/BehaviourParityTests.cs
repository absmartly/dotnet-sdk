using System.Globalization;
using ABSmartly.Models;
using ABSmartly.Services;
using ABSmartly.Time;
using Microsoft.Extensions.Logging;

namespace ABSmartly.Sdk.Tests;

// Regression tests for behaviour that must match 1.3.0 and the Java SDK, and for
// bugs found in review of the 1.4.0 changes.
[TestFixture]
public class BehaviourParityTests
{
    private readonly Clock _clock = Clock.Fixed(1_620_000_000_000L);
    private readonly Dictionary<string, string> _units = new() { ["session_id"] = "e791e240fcd3df7d238cfc285f475e8152fcc0ec" };

    private ContextData _data = null!;
    private IContextDataProvider _dataProvider = null!;
    private IContextPublisher _publisher = null!;
    private IContextEventLogger _eventLogger = null!;
    private IVariableParser _variableParser = null!;
    private AudienceMatcher _audienceMatcher = null!;

    [SetUp]
    public void SetUp()
    {
        using var contextStream =
            GetType().Assembly.GetManifestResourceStream("ABSmartly.Sdk.Tests.Resources.context.json");
        _data = new DefaultContextDataDeserializer().Deserialize(contextStream);

        _dataProvider = Mock.Of<IContextDataProvider>();
        _publisher = Mock.Of<IContextPublisher>();
        _eventLogger = Mock.Of<IContextEventLogger>();
        _variableParser = new DefaultVariableParser();
        _audienceMatcher = new AudienceMatcher(new DefaultAudienceDeserializer());
    }

    private Context CreateContext(ContextData data, ContextConfig? config = null) =>
        new(config ?? new ContextConfig { PublishDelay = TimeSpan.FromSeconds(60) }.SetUnits(_units), data, _clock,
            _dataProvider, _publisher, _eventLogger, _variableParser, _audienceMatcher, new LoggerFactory());

    private static ContextData DataWithCustomFields(params CustomFieldValue[] fields) =>
        new(new[]
        {
            new Experiment
            {
                Id = 1, Name = "exp", UnitType = "session_id", Iteration = 1,
                Split = new[] { 0.5, 0.5 }, TrafficSplit = new[] { 0.0, 1.0 },
                Variants = new[] { new ExperimentVariant { Name = "A" }, new ExperimentVariant { Name = "B" } },
                CustomFieldValues = fields
            }
        });

    #region Custom fields

    [TestCase("de-DE")]
    [TestCase("pt-BR")]
    [TestCase("fr-FR")]
    [TestCase("en-US")]
    public void NumberCustomFieldsParseWithInvariantCulture(string culture)
    {
        var previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo(culture);

            var context = CreateContext(DataWithCustomFields(
                new CustomFieldValue { Name = "ratio", Type = "number", Value = "1.5" },
                new CustomFieldValue { Name = "count", Type = "number", Value = "42" },
                new CustomFieldValue { Name = "grouped", Type = "number", Value = "1,5" }));

            context.GetCustomFieldValue("exp", "ratio").Should().Be(1.5);
            context.GetCustomFieldValue("exp", "count").Should().Be(42L);
            // A culture-specific decimal comma is not a JSON number: not silently read as 15.
            context.GetCustomFieldValue("exp", "grouped").Should().BeNull();
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }

    [Test]
    public void JsonCustomFieldsGoThroughTheConfiguredVariableParser()
    {
        var parsed = new Dictionary<string, object> { ["from"] = "custom parser" };
        var parser = new Mock<IVariableParser>();
        parser.Setup(p => p.Parse(It.IsAny<IContext>(), "exp", "settings", "{\"a\":1}")).Returns(parsed);
        _variableParser = parser.Object;

        var context = CreateContext(DataWithCustomFields(
            new CustomFieldValue { Name = "settings", Type = "json", Value = "{\"a\":1}" }));

        context.GetCustomFieldValue("exp", "settings").Should().BeSameAs(parsed);
        parser.Verify(p => p.Parse(It.IsAny<IContext>(), "exp", "settings", "{\"a\":1}"), Times.Once);
    }

    #endregion

    #region Attributes

    [Test]
    public void AttributeReadsAreSafeAgainstConcurrentSetAttribute()
    {
        var context = CreateContext(_data);
        using var stop = new CancellationTokenSource();
        Exception? readError = null;

        var reader = Task.Run(() =>
        {
            try
            {
                while (!stop.IsCancellationRequested)
                {
                    _ = context.Attributes;
                    _ = context.GetAttribute("attr");
                }
            }
            catch (Exception e)
            {
                readError = e;
            }
        });

        for (var i = 0; i < 20_000 && readError == null; i++)
            context.SetAttribute("attr", i);

        stop.Cancel();
        reader.Wait(TimeSpan.FromSeconds(10)).Should().BeTrue();

        readError.Should().BeNull();
        context.GetAttribute("attr").Should().NotBeNull();
    }

    [Test]
    public void AttributesStayReadableAfterDispose()
    {
        var context = CreateContext(_data);
        context.SetAttribute("a", 1);
        context.SetAttribute("a", 2);
        context.Dispose();

        context.GetAttribute("a").Should().Be(2);
        context.Attributes.Should().BeEquivalentTo(new Dictionary<string, object> { ["a"] = 2 });
    }

    #endregion

    #region Audience

    private sealed class CountingAudienceDeserializer : IAudienceDeserializer
    {
        private readonly DefaultAudienceDeserializer _inner = new();
        public int Calls;

        public Dictionary<string, object> Deserialize(string audience)
        {
            Interlocked.Increment(ref Calls);
            return _inner.Deserialize(audience);
        }
    }

    [Test]
    public void AudienceIsNotReEvaluatedOnEveryCallAfterSetAttribute()
    {
        var counting = new CountingAudienceDeserializer();
        _audienceMatcher = new AudienceMatcher(counting);
        var data = DataWithCustomFields();
        data.Experiments[0].Audience = "{\"filter\":[{\"gte\":[{\"var\":\"age\"},{\"value\":20}]}]}";

        var context = CreateContext(data);
        context.SetAttribute("age", 30);

        var variant = context.GetTreatment("exp");
        counting.Calls.Should().Be(1, "the first assignment evaluates the audience");

        context.SetAttribute("age", 31);
        context.GetTreatment("exp").Should().Be(variant);
        counting.Calls.Should().Be(2, "a changed attribute re-checks the audience once");

        for (var i = 0; i < 10; i++)
        {
            context.GetTreatment("exp").Should().Be(variant);
            context.PeekTreatment("exp").Should().Be(variant);
        }
        counting.Calls.Should().Be(2, "the revalidated assignment is cached until the next SetAttribute");

        // A change that flips the audience result still invalidates the assignment.
        context.SetAttribute("age", 10);
        context.GetTreatment("exp");
        context.PendingCount.Should().Be(2, "the mismatched assignment is a new exposure");
    }

    #endregion

    #region Variables

    private static Experiment VariableExperiment(int id, string name, string controlConfig, string treatmentConfig) =>
        new()
        {
            Id = id, Name = name, UnitType = "session_id", Iteration = 1,
            Split = new[] { 0.5, 0.5 }, TrafficSplit = new[] { 0.0, 1.0 },
            Variants = new[]
            {
                new ExperimentVariant { Name = "A", Config = controlConfig },
                new ExperimentVariant { Name = "B", Config = treatmentConfig }
            }
        };

    [Test]
    public void VariableLookupSkipsAnAssignedExperimentWhoseVariantLacksTheKey()
    {
        // Both experiments index "color" (via variant B); exp_low wins by id, but is
        // overridden to variant A, which does not define it.
        var data = new ContextData(new[]
        {
            VariableExperiment(1, "exp_low", "{\"other\":1}", "{\"color\":\"red\"}"),
            VariableExperiment(2, "exp_high", "{\"color\":\"blue\"}", "{\"color\":\"blue\"}")
        });
        var context = CreateContext(data);
        context.SetOverride("exp_low", 0);
        context.SetOverride("exp_high", 1);

        context.PeekVariableValue("color", "default").Should().Be("blue");
        context.GetVariableValue("color", "default").Should().Be("blue");
        context.PendingCount.Should().Be(1, "only the experiment that supplied the value is exposed");

        // When the lowest-id experiment's variant does define the key, it still wins.
        context.SetOverride("exp_low", 1);
        context.PeekVariableValue("color", "default").Should().Be("red");
    }

    #endregion

    #region Publish and close

    private static async Task<bool> Completes(Task task, int timeoutMs = 5000) =>
        await Task.WhenAny(task, Task.Delay(timeoutMs)) == task;

    [Test]
    public async Task PendingCountIsZeroWhilePublishingAndRestoredOnFailure()
    {
        var entered = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        PublishEvent? published = null;
        Mock.Get(_publisher).Setup(x => x.PublishAsync(It.IsAny<IContext>(), It.IsAny<PublishEvent>()))
            .Returns<IContext, PublishEvent>(async (_, e) =>
            {
                entered.TrySetResult(true);
                await release.Task;
                throw new InvalidOperationException("publish failed");
            });

        var context = CreateContext(_data);
        context.GetTreatment("exp_test_ab");
        context.Track("goal1", null);
        context.PendingCount.Should().Be(2);

        var publish = context.PublishAsync();
        (await Completes(entered.Task)).Should().BeTrue();
        context.PendingCount.Should().Be(0, "the batch is detached while in flight");

        // Tracked during the failed publish: kept after the restored batch.
        context.Track("goal2", null);
        context.PendingCount.Should().Be(1);

        release.SetResult(true);
        Func<Task> act = () => publish;
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("publish failed");
        context.PendingCount.Should().Be(3, "the failed batch is restored and stays retryable");

        Mock.Get(_publisher).Setup(x => x.PublishAsync(It.IsAny<IContext>(), It.IsAny<PublishEvent>()))
            .Callback<IContext, PublishEvent>((_, e) => published = e)
            .Returns(Task.CompletedTask);
        await context.PublishAsync();

        context.PendingCount.Should().Be(0);
        published!.Exposures.Should().ContainSingle(x => x.Name == "exp_test_ab");
        published.Goals.Select(g => g.Name).Should().Equal("goal1", "goal2");
    }

    [Test]
    public async Task ConcurrentFlushesPublishEachEventOnce()
    {
        var published = new System.Collections.Concurrent.ConcurrentBag<PublishEvent>();
        Mock.Get(_publisher).Setup(x => x.PublishAsync(It.IsAny<IContext>(), It.IsAny<PublishEvent>()))
            .Returns<IContext, PublishEvent>(async (_, e) =>
            {
                published.Add(e);
                await Task.Yield();
            });

        var context = CreateContext(_data);
        var tasks = new List<Task>();
        for (var i = 0; i < 200; i++)
        {
            context.Track("goal" + i, null);
            tasks.Add(Task.Run(() => context.PublishAsync()));
        }

        await Task.WhenAll(tasks);
        await context.PublishAsync();

        context.PendingCount.Should().Be(0);
        published.SelectMany(e => e.Goals ?? Array.Empty<GoalAchievement>()).Select(g => g.Name)
            .Should().OnlyHaveUniqueItems().And.HaveCount(200);
    }

    [Test]
    [Timeout(10000)]
    public async Task PublisherCanCallPublishAsyncReentrantly()
    {
        Context context = null!;
        var calls = 0;
        Mock.Get(_publisher).Setup(x => x.PublishAsync(It.IsAny<IContext>(), It.IsAny<PublishEvent>()))
            .Returns<IContext, PublishEvent>(async (_, _) =>
            {
                if (Interlocked.Increment(ref calls) == 1)
                {
                    context.Track("from_publisher", null);
                    await context.PublishAsync();
                }
            });

        context = CreateContext(_data);
        context.Track("goal1", null);

        (await Completes(context.PublishAsync())).Should().BeTrue("a re-entrant publish must not deadlock");
        calls.Should().Be(2);
        context.PendingCount.Should().Be(0);
    }

    [Test]
    [Timeout(10000)]
    public async Task PublisherCanCallFinalizeAsyncReentrantly()
    {
        Context context = null!;
        Mock.Get(_publisher).Setup(x => x.PublishAsync(It.IsAny<IContext>(), It.IsAny<PublishEvent>()))
            .Returns<IContext, PublishEvent>(async (_, _) => await context.FinalizeAsync());

        context = CreateContext(_data);
        context.Track("goal1", null);

        (await Completes(context.PublishAsync())).Should().BeTrue("a re-entrant finalize must not deadlock");
        // The close seals once the publisher's own publication has settled.
        (await Completes(context.FinalizeAsync())).Should().BeTrue();
        context.IsFinalized.Should().BeTrue();
    }

    [Test]
    [Timeout(10000)]
    public async Task PublisherCanCallFinalizeAsyncDuringClose()
    {
        Context context = null!;
        Mock.Get(_publisher).Setup(x => x.PublishAsync(It.IsAny<IContext>(), It.IsAny<PublishEvent>()))
            .Returns<IContext, PublishEvent>(async (_, _) => await context.FinalizeAsync());

        context = CreateContext(_data);
        context.Track("goal1", null);

        (await Completes(context.FinalizeAsync())).Should().BeTrue();
        context.IsFinalized.Should().BeTrue();
    }

    // A publication already handed to the publisher when the context is closed may still
    // fail. The close must wait for it, so the restored events are retried by the close's
    // own flush instead of being stranded on a sealed context.
    [TestCase(false)]
    [TestCase(true)]
    [Timeout(10000)]
    public async Task CloseWaitsForOutstandingPublishAndRetriesItsFailedEvents(bool dispose)
    {
        var entered = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var calls = 0;
        PublishEvent? retried = null;
        Mock.Get(_publisher).Setup(x => x.PublishAsync(It.IsAny<IContext>(), It.IsAny<PublishEvent>()))
            .Returns<IContext, PublishEvent>(async (_, e) =>
            {
                if (Interlocked.Increment(ref calls) == 1)
                {
                    entered.TrySetResult(true);
                    await release.Task;
                    throw new InvalidOperationException("publish failed");
                }

                retried = e;
            });

        var context = CreateContext(_data);
        context.Track("goal1", null);

        var publish = context.PublishAsync();
        (await Completes(entered.Task)).Should().BeTrue();

        var close = dispose ? context.DisposeAsync().AsTask() : context.FinalizeAsync();
        await Task.Delay(100);
        close.IsCompleted.Should().BeFalse("the close waits for the outstanding publication");
        context.IsFinalized.Should().BeFalse();

        release.SetResult(true);
        Func<Task> failedPublish = () => publish;
        await failedPublish.Should().ThrowAsync<InvalidOperationException>().WithMessage("publish failed");

        (await Completes(close)).Should().BeTrue();
        await close;
        calls.Should().Be(2, "the close retried the restored events");
        retried!.Goals.Select(g => g.Name).Should().Equal("goal1");
        context.PendingCount.Should().Be(0);
        context.IsFinalized.Should().BeTrue();
    }

    // If the close's retry fails too, the context stays open and the events stay retryable.
    [Test]
    [Timeout(10000)]
    public async Task CloseRacingFailedPublishLeavesEventsRetryableWhenRetryFails()
    {
        var entered = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var fail = true;
        Mock.Get(_publisher).Setup(x => x.PublishAsync(It.IsAny<IContext>(), It.IsAny<PublishEvent>()))
            .Returns<IContext, PublishEvent>(async (_, _) =>
            {
                entered.TrySetResult(true);
                await release.Task;
                if (Volatile.Read(ref fail)) throw new InvalidOperationException("publish failed");
            });

        var context = CreateContext(_data);
        context.Track("goal1", null);

        var publish = context.PublishAsync();
        (await Completes(entered.Task)).Should().BeTrue();
        var close = context.FinalizeAsync();

        release.SetResult(true);
        Func<Task> failedPublish = () => publish;
        await failedPublish.Should().ThrowAsync<InvalidOperationException>();
        Func<Task> failedClose = () => close;
        (await Completes(close.ContinueWith(_ => { }))).Should().BeTrue();
        await failedClose.Should().ThrowAsync<InvalidOperationException>();

        context.IsFinalized.Should().BeFalse("a failed close leaves the context open");
        context.PendingCount.Should().Be(1, "the events stay retryable");

        Volatile.Write(ref fail, false);
        await context.FinalizeAsync();
        context.PendingCount.Should().Be(0);
        context.IsFinalized.Should().BeTrue();
    }

    // A publisher whose publication a concurrent close is waiting for may itself call
    // FinalizeAsync: that must not deadlock.
    [Test]
    [Timeout(10000)]
    public async Task PublisherFinalizingWhileAnotherCloseWaitsForItDoesNotDeadlock()
    {
        Context context = null!;
        var entered = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var closeStarted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        Mock.Get(_publisher).Setup(x => x.PublishAsync(It.IsAny<IContext>(), It.IsAny<PublishEvent>()))
            .Returns<IContext, PublishEvent>(async (_, _) =>
            {
                entered.TrySetResult(true);
                await closeStarted.Task;
                await context.FinalizeAsync();
            });

        context = CreateContext(_data);
        context.Track("goal1", null);

        var publish = context.PublishAsync();
        (await Completes(entered.Task)).Should().BeTrue();
        var close = context.FinalizeAsync();
        closeStarted.SetResult(true);

        (await Completes(publish)).Should().BeTrue("the publisher's nested finalize must not deadlock");
        (await Completes(close)).Should().BeTrue();
        context.IsFinalized.Should().BeTrue();
        context.PendingCount.Should().Be(0);
    }

    // A single-threaded SynchronizationContext, like a UI thread: posted continuations
    // run only on its own thread.
    private sealed class SingleThreadSyncContext : SynchronizationContext, IDisposable
    {
        private readonly System.Collections.Concurrent.BlockingCollection<(SendOrPostCallback, object?)> _queue = new();
        private readonly Thread _thread;

        public SingleThreadSyncContext()
        {
            _thread = new Thread(() =>
            {
                SetSynchronizationContext(this);
                foreach (var (callback, state) in _queue.GetConsumingEnumerable()) callback(state);
            }) { IsBackground = true };
            _thread.Start();
        }

        public override void Post(SendOrPostCallback d, object? state) => _queue.Add((d, state));

        public Task Run(Action action)
        {
            var done = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            Post(_ =>
            {
                try
                {
                    action();
                    done.SetResult(true);
                }
                catch (Exception e)
                {
                    done.SetException(e);
                }
            }, null);
            return done.Task;
        }

        public void Dispose() => _queue.CompleteAdding();
    }

    // A synchronous Dispose()/Close() on a UI-like thread must not wait for a publication
    // whose continuation needs that same thread: it would deadlock.
    [TestCase(false)]
    [TestCase(true)]
    [Timeout(10000)]
    public async Task SynchronousCloseOnUiThreadDuringPublishDoesNotDeadlock(bool dispose)
    {
        var release = new TaskCompletionSource<bool>();
        Mock.Get(_publisher).Setup(x => x.PublishAsync(It.IsAny<IContext>(), It.IsAny<PublishEvent>()))
            .Returns<IContext, PublishEvent>(async (_, _) => await release.Task); // resumes on the UI thread

        var context = CreateContext(_data);
        context.Track("goal1", null);
        using var ui = new SingleThreadSyncContext();
        Task publish = null!;

        var closed = ui.Run(() =>
        {
            publish = context.PublishAsync();
            if (dispose) context.Dispose();
            else context.Close();
        });
        _ = Task.Run(async () =>
        {
            await Task.Delay(200);
            release.SetResult(true);
        });

        (await Completes(closed)).Should().BeTrue("the UI thread must not deadlock");
        (await Completes(publish)).Should().BeTrue();
        // The close seals in the background once the outstanding publication settles.
        (await Completes(context.FinalizeAsync())).Should().BeTrue();
        context.IsFinalized.Should().BeTrue();
    }

    // A synchronous Close()/Dispose() on a UI-like thread that joins a close already in
    // progress (owned by FinalizeAsync, waiting for a publication whose continuation needs
    // the UI thread) must not block that thread. The owning close still retries the
    // failed publication's events.
    [TestCase(false)]
    [TestCase(true)]
    [Timeout(10000)]
    public async Task SynchronousCloseOnUiThreadJoiningCloseInProgressDoesNotDeadlock(bool dispose)
    {
        var gate = new TaskCompletionSource<bool>();
        var entered = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var calls = 0;
        PublishEvent? retried = null;
        Mock.Get(_publisher).Setup(x => x.PublishAsync(It.IsAny<IContext>(), It.IsAny<PublishEvent>()))
            .Returns<IContext, PublishEvent>(async (_, e) =>
            {
                if (Interlocked.Increment(ref calls) == 1)
                {
                    entered.TrySetResult(true);
                    await gate.Task; // resumes on the UI thread
                    throw new InvalidOperationException("publish failed");
                }

                retried = e;
            });

        var context = CreateContext(_data);
        context.Track("goal1", null);
        using var ui = new SingleThreadSyncContext();
        Task publish = null!;

        await ui.Run(() => publish = context.PublishAsync());
        (await Completes(entered.Task)).Should().BeTrue();

        var finalize = context.FinalizeAsync(); // owns the close, waits for the publication
        await Task.Delay(50);
        finalize.IsCompleted.Should().BeFalse();

        var joined = ui.Run(() =>
        {
            if (dispose) context.Dispose();
            else context.Close();
        });
        (await Completes(joined)).Should().BeTrue("the UI thread must not block on the close in progress");

        gate.SetResult(true);
        Func<Task> failedPublish = () => publish;
        (await Completes(publish.ContinueWith(_ => { }))).Should().BeTrue();
        await failedPublish.Should().ThrowAsync<InvalidOperationException>();
        (await Completes(finalize)).Should().BeTrue();
        await finalize;

        retried!.Goals!.Select(g => g.Name).Should().Equal("goal1");
        context.PendingCount.Should().Be(0);
        context.IsFinalized.Should().BeTrue();
        typeof(Context).GetField("_locksDisposed",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
            .GetValue(context).Should().Be(dispose, "a Dispose that joined without waiting still disposes once closed");
    }

    // A synchronous Close()/Dispose() on a UI-like thread that owns the close does not wait
    // for an outstanding publication (that could deadlock), but must not seal the context
    // before it settles: when it fails, its events are retried by the close.
    [TestCase(false)]
    [TestCase(true)]
    [Timeout(10000)]
    public async Task SynchronousCloseOnUiThreadRetriesEventsOfOutstandingFailedPublish(bool dispose)
    {
        var gate = new TaskCompletionSource<bool>();
        var calls = 0;
        PublishEvent? retried = null;
        Mock.Get(_publisher).Setup(x => x.PublishAsync(It.IsAny<IContext>(), It.IsAny<PublishEvent>()))
            .Returns<IContext, PublishEvent>(async (_, e) =>
            {
                if (Interlocked.Increment(ref calls) == 1)
                {
                    await gate.Task; // resumes on the UI thread
                    throw new InvalidOperationException("publish failed");
                }

                retried = e;
            });

        var context = CreateContext(_data);
        context.Track("goal1", null);
        using var ui = new SingleThreadSyncContext();
        Task publish = null!;

        var closed = ui.Run(() =>
        {
            publish = context.PublishAsync();
            if (dispose) context.Dispose();
            else context.Close();
        });
        (await Completes(closed)).Should().BeTrue("the UI thread must not block");
        context.IsFinalized.Should().BeFalse("the close waits for the outstanding publication before sealing");

        gate.SetResult(true);
        Func<Task> failedPublish = () => publish;
        (await Completes(publish.ContinueWith(_ => { }))).Should().BeTrue();
        await failedPublish.Should().ThrowAsync<InvalidOperationException>();

        (await Completes(context.FinalizeAsync())).Should().BeTrue();
        retried!.Goals!.Select(g => g.Name).Should().Equal("goal1");
        context.PendingCount.Should().Be(0);
        context.IsFinalized.Should().BeTrue();
        typeof(Context).GetField("_locksDisposed",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
            .GetValue(context).Should().Be(dispose);
    }

    // A publisher that finalizes its own context and then fails: the close must not seal
    // before that publication settles, and retries its restored events.
    [Test]
    [Timeout(10000)]
    public async Task PublisherFinalizingThenFailingHasItsEventsRetriedByTheClose()
    {
        Context context = null!;
        var calls = 0;
        PublishEvent? retried = null;
        Mock.Get(_publisher).Setup(x => x.PublishAsync(It.IsAny<IContext>(), It.IsAny<PublishEvent>()))
            .Returns<IContext, PublishEvent>(async (_, e) =>
            {
                if (Interlocked.Increment(ref calls) == 1)
                {
                    await context.FinalizeAsync();
                    context.IsFinalized.Should().BeFalse("its own publication is still outstanding");
                    throw new InvalidOperationException("publish failed");
                }

                retried = e;
            });

        context = CreateContext(_data);
        context.Track("goal1", null);

        Func<Task> failedPublish = () => context.PublishAsync();
        await failedPublish.Should().ThrowAsync<InvalidOperationException>().WithMessage("publish failed");

        (await Completes(context.FinalizeAsync())).Should().BeTrue();
        retried!.Goals!.Select(g => g.Name).Should().Equal("goal1");
        context.PendingCount.Should().Be(0);
        context.IsFinalized.Should().BeTrue();
    }

    // If the deferred close's retry fails too, the context stays open and the events stay
    // retryable.
    [Test]
    [Timeout(10000)]
    public async Task DeferredCloseWhoseRetryFailsLeavesEventsRetryable()
    {
        Context context = null!;
        var fail = true;
        var first = true;
        Mock.Get(_publisher).Setup(x => x.PublishAsync(It.IsAny<IContext>(), It.IsAny<PublishEvent>()))
            .Returns<IContext, PublishEvent>(async (_, _) =>
            {
                if (first)
                {
                    first = false;
                    await context.FinalizeAsync();
                }

                await Task.Yield();
                if (Volatile.Read(ref fail)) throw new InvalidOperationException("publish failed");
            });

        context = CreateContext(_data);
        context.Track("goal1", null);
        Func<Task> failedPublish = () => context.PublishAsync();
        await failedPublish.Should().ThrowAsync<InvalidOperationException>();

        Func<Task> failedClose = () => context.FinalizeAsync();
        await failedClose.Should().ThrowAsync<InvalidOperationException>();
        context.IsFinalized.Should().BeFalse();
        context.PendingCount.Should().Be(1);

        Volatile.Write(ref fail, false);
        await context.FinalizeAsync();
        context.PendingCount.Should().Be(0);
        context.IsFinalized.Should().BeTrue();
    }

    // While a close is waiting for an outstanding publication, a flush (as the publish
    // timer) leaves the queued events for the close instead of detaching them.
    [Test]
    [Timeout(10000)]
    public async Task FlushDuringCloseLeavesEventsForTheClose()
    {
        var entered = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var batches = new System.Collections.Concurrent.ConcurrentQueue<string[]>();
        Mock.Get(_publisher).Setup(x => x.PublishAsync(It.IsAny<IContext>(), It.IsAny<PublishEvent>()))
            .Returns<IContext, PublishEvent>(async (_, e) =>
            {
                var names = e.Goals!.Select(g => g.Name).ToArray();
                if (names.Contains("goal1") && entered.TrySetResult(true))
                {
                    await release.Task;
                    throw new InvalidOperationException("publish failed");
                }

                batches.Enqueue(names);
            });

        var context = CreateContext(_data);
        context.Track("goal1", null);
        var publish = context.PublishAsync();
        (await Completes(entered.Task)).Should().BeTrue();
        context.Track("goal2", null);

        var close = context.FinalizeAsync();
        var flush = (Task)typeof(Context).GetMethod("Flush",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.Invoke(context, null)!;
        await flush;
        batches.Should().BeEmpty("a flush during the close must not detach the batch");
        context.PendingCount.Should().Be(1);

        release.SetResult(true);
        Func<Task> failedPublish = () => publish;
        await failedPublish.Should().ThrowAsync<InvalidOperationException>();
        (await Completes(close)).Should().BeTrue();

        batches.Should().ContainSingle().Which.Should().Equal("goal1", "goal2");
        context.PendingCount.Should().Be(0);
        context.IsFinalized.Should().BeTrue();
    }

    // Work a publisher started (here Task.Run) that closes the context after its
    // publication has settled waits for the running close instead of returning early.
    [Test]
    [Timeout(10000)]
    public async Task CloseFromPublisherStartedWorkAfterItsPublicationSettledWaitsForTheClose()
    {
        Context context = null!;
        var otherEntered = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseOther = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var startLater = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        Task later = null!;
        var calls = 0;
        Mock.Get(_publisher).Setup(x => x.PublishAsync(It.IsAny<IContext>(), It.IsAny<PublishEvent>()))
            .Returns<IContext, PublishEvent>(async (_, _) =>
            {
                var call = Interlocked.Increment(ref calls);
                if (call == 1)
                {
                    // Started from inside this publication, run after it has settled.
                    later = Task.Run(async () =>
                    {
                        await startLater.Task;
                        await context.FinalizeAsync();
                    });
                }
                else if (call == 2)
                {
                    otherEntered.TrySetResult(true);
                    await releaseOther.Task;
                }
            });

        context = CreateContext(_data);
        context.Track("goal1", null);
        await context.PublishAsync(); // publication 1 settles

        context.Track("goal2", null);
        var otherPublish = context.PublishAsync(); // publication 2 in flight
        (await Completes(otherEntered.Task)).Should().BeTrue();
        var close = context.FinalizeAsync(); // waits for publication 2

        startLater.SetResult(true);
        await Task.Delay(100);
        later.IsCompleted.Should().BeFalse("the close it joined is still waiting for publication 2");

        releaseOther.SetResult(true);
        await otherPublish;
        (await Completes(close)).Should().BeTrue();
        (await Completes(later)).Should().BeTrue();
        context.IsFinalized.Should().BeTrue();
    }

    // An event logger that, like a UI dispatcher, hands Close()/Dispose() to another
    // thread and blocks until it has run. With suppressFlow the other thread does not
    // inherit the caller's AsyncLocal state, as a pre-existing UI thread.
    private sealed class CrossThreadClosingLogger : IContextEventLogger
    {
        private readonly EventType _on;
        private readonly bool _dispose;
        private readonly bool _suppressFlow;
        private int _dispatched;
        public Context Context = null!;
        public bool? DispatchCompleted;

        public CrossThreadClosingLogger(EventType on, bool dispose, bool suppressFlow)
        {
            _on = on;
            _dispose = dispose;
            _suppressFlow = suppressFlow;
        }

        public void HandleEvent(IContext context, EventType eventType, object data)
        {
            if (eventType != _on || Interlocked.Exchange(ref _dispatched, 1) == 1) return;

            var other = new Thread(() =>
            {
                try
                {
                    if (_dispose) Context.Dispose();
                    else Context.Close();
                }
                catch (Exception)
                {
                    // A retried close may fail again; only the wait matters here.
                }
            });
            if (_suppressFlow)
                using (ExecutionContext.SuppressFlow())
                    other.Start();
            else
                other.Start();
            DispatchCompleted = other.Join(TimeSpan.FromSeconds(3));
        }
    }

    [Test]
    [Timeout(20000)]
    public async Task EventLoggerDispatchingCloseToAnotherThreadDoesNotDeadlock(
        [Values(EventType.Publish, EventType.Close)] EventType on, [Values] bool dispose, [Values] bool suppressFlow)
    {
        var logger = new CrossThreadClosingLogger(on, dispose, suppressFlow);
        _eventLogger = logger;
        Mock.Get(_publisher).Setup(x => x.PublishAsync(It.IsAny<IContext>(), It.IsAny<PublishEvent>()))
            .Returns(Task.CompletedTask);
        var context = logger.Context = CreateContext(_data);
        context.Track("goal1", null);

        (await Completes(Task.Run(() => context.Close()))).Should().BeTrue();

        logger.DispatchCompleted.Should().BeTrue("the dispatched Close() must not wait on the attempt that called it");
        context.IsFinalized.Should().BeTrue();
    }

    [Test]
    [Timeout(20000)]
    public async Task ErrorLoggerDispatchingCloseToAnotherThreadAfterFailedCloseDoesNotDeadlock(
        [Values] bool dispose, [Values] bool suppressFlow)
    {
        var logger = new CrossThreadClosingLogger(EventType.Error, dispose, suppressFlow);
        _eventLogger = logger;
        var attempts = 0;
        Mock.Get(_publisher).Setup(x => x.PublishAsync(It.IsAny<IContext>(), It.IsAny<PublishEvent>()))
            .Returns(() => Interlocked.Increment(ref attempts) == 1
                ? Task.FromException(new InvalidOperationException("publish failed"))
                : Task.CompletedTask);
        var context = logger.Context = CreateContext(_data);
        context.Track("goal1", null);

        var close = Task.Run(() => context.Close());
        (await Completes(close.ContinueWith(_ => { }))).Should().BeTrue();

        close.IsFaulted.Should().BeTrue("the first close attempt failed");
        logger.DispatchCompleted.Should().BeTrue();
        attempts.Should().Be(2, "the dispatched close retried the publish");
        context.IsFinalized.Should().BeTrue();
        context.PendingCount.Should().Be(0);
    }

    [Test]
    public void EventLoggerExceptionsPropagateExceptForPublishOutcomes()
    {
        // As 1.3.0 and the Java SDK: a throwing logger surfaces to the caller, except
        // for the Publish/Error events of a flush, where it must not turn a delivered
        // publish into a failure (resending the batch) or mask the publish error.
        var published = 0;
        Mock.Get(_publisher).Setup(x => x.PublishAsync(It.IsAny<IContext>(), It.IsAny<PublishEvent>()))
            .Callback(() => published++)
            .Returns(Task.CompletedTask);
        var context = CreateContext(_data);
        Mock.Get(_eventLogger).Setup(x => x.HandleEvent(It.IsAny<IContext>(), It.IsAny<EventType>(), It.IsAny<object>()))
            .Throws(new InvalidOperationException("logger failed"));

        var track = () => context.Track("goal1", null);
        track.Should().Throw<InvalidOperationException>().WithMessage("logger failed");
        context.PendingCount.Should().Be(1, "the goal was queued before the logger ran");

        var publish = () => context.Publish();
        publish.Should().NotThrow();
        published.Should().Be(1);
        context.PendingCount.Should().Be(0);

        Mock.Get(_publisher).Setup(x => x.PublishAsync(It.IsAny<IContext>(), It.IsAny<PublishEvent>()))
            .Returns(Task.FromException(new InvalidOperationException("publish failed")));
        try { context.Track("goal2", null); } catch (InvalidOperationException) { }
        publish.Should().Throw<InvalidOperationException>().WithMessage("publish failed");
        context.PendingCount.Should().Be(1);
    }

    [Test]
    public void EventLoggerExceptionDuringConstructionPropagates()
    {
        Mock.Get(_eventLogger).Setup(x => x.HandleEvent(It.IsAny<IContext>(), EventType.Ready, It.IsAny<object>()))
            .Throws(new InvalidOperationException("logger failed"));

        var act = () => CreateContext(_data);
        act.Should().Throw<InvalidOperationException>().WithMessage("logger failed");
    }

    #endregion

}
