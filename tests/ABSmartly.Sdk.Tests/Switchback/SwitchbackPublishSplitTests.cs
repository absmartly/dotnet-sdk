using System.Text;
using System.Text.Json;
using ABSmartly.Models;
using ABSmartly.Services;

namespace ABSmartly.Sdk.Tests.Switchback;

// Review regressions (round 3): split publish requests reach the event logger in order, in normal
// and failing flushes and on close; serving unit counts are kept per slot; only cluster attributes
// are re-dated after a clock rewind; and only a late *linked* unit splits a request.
[TestFixture]
public class SwitchbackPublishSplitTests : SwitchbackTestBase
{
    private Context WithLogger(List<(EventType, object)> events, ContextData data)
    {
        var logger = new Mock<IContextEventLogger>();
        logger.Setup(x => x.HandleEvent(It.IsAny<IContext>(), It.IsAny<EventType>(), It.IsAny<object>()))
            .Callback<IContext, EventType, object>((_, t, e) => events.Add((t, e)));
        var config = new ContextConfig { PublishDelay = TimeSpan.FromMinutes(10) };
        config.SetUnit(UnitType, Uid);
        return Context.CreateWithData(config, data, Clock, DataProvider.Object, Publisher, logger.Object,
            new DefaultVariableParser(), new AudienceMatcher(new DefaultAudienceDeserializer()), null);
    }

    private Experiment Linked() { var e = NewSwitchback(2, Hour); e.SwitchbackClusters = Clusters(UnitType, "city", "user_id", "user_city"); return e; }

    [Test]
    public void LoggerSeesEverySplitRequest()
    {
        var events = new List<(EventType, object)>();
        var context = WithLogger(events, DataOf(Linked()));
        Clock.Now = Anchor + Hour + 1;
        context.SetAttribute("city", "Faro");
        context.GetTreatment("exp_sb");
        context.SetUnit("user_id", "u1");
        context.Track("purchase", null!);
        context.Publish();
        var pubs = events.Where(e => e.Item1 == EventType.Publish).Select(e => (PublishEvent)e.Item2).ToList();
        pubs.Select(p => p.Units.Length).Should().Equal(1, 2);
    }

    [Test]
    public void LoggerSeesEverySplitRequestOnClose()
    {
        var events = new List<(EventType, object)>();
        var context = WithLogger(events, DataOf(Linked()));
        Clock.Now = Anchor + Hour + 1;
        context.SetAttribute("city", "Faro");
        context.GetTreatment("exp_sb");
        context.SetUnit("user_id", "u1");
        context.Track("purchase", null!);
        context.Close();
        events.Where(e => e.Item1 == EventType.Publish).Select(e => ((PublishEvent)e.Item2).Units.Length).Should().Equal(1, 2);
    }

    [Test]
    public void LoggerSeesTheDeliveredRequestWhenALaterOneFails()
    {
        var calls = 0;
        var publisher = new Mock<IContextPublisher>();
        publisher.Setup(x => x.PublishAsync(It.IsAny<IContext>(), It.IsAny<PublishEvent>()))
            .Returns<IContext, PublishEvent>((_, _) => ++calls == 2 ? throw new InvalidOperationException("down") : Task.CompletedTask);
        Publisher = publisher.Object;
        var events = new List<(EventType, object)>();
        var context = WithLogger(events, DataOf(Linked()));
        Clock.Now = Anchor + Hour + 1;
        context.SetAttribute("city", "Faro");
        context.GetTreatment("exp_sb");
        context.SetUnit("user_id", "u1");
        context.Track("purchase", null!);
        FluentActions.Invoking(() => context.Publish()).Should().Throw<InvalidOperationException>();
        events.Select(e => e.Item1).Where(t => t != EventType.Ready).Should().Equal(EventType.Exposure, EventType.Goal, EventType.Publish, EventType.Error);
    }

    [Test]
    public void TwoServingUnitCountsGiveOrderedRequests()
    {
        // exp_a is served with one unit and exp_b with two; a third linked unit is set after both.
        var a = NewSwitchback(1, "exp_a", 2, Hour); a.SwitchbackClusters = Clusters(UnitType, "city", "device_id", "device_city");
        var b = NewSwitchback(2, "exp_b", 2, Hour); b.SwitchbackClusters = Clusters(UnitType, "city", "device_id", "device_city");
        var context = CreateContext(DataOf(a, b));
        Clock.Now = Anchor + Hour + 1;
        context.SetAttribute("city", "Faro");
        context.GetTreatment("exp_a");
        context.SetUnit("user_id", "u1");
        context.GetTreatment("exp_b");
        context.SetUnit("device_id", "d1");
        context.Publish();
        Published.Select(p => p.Event.Units.Length).Should().Equal(1, 2);
        Published.Select(p => p.Event.Exposures!.Single().Name).Should().Equal("exp_a", "exp_b");
    }

    [Test]
    public void RewindClampUsesTheLatestReadNotTheLast()
    {
        var a = NewSwitchback(1, "exp_a", 2, Hour); var b = NewSwitchback(2, "exp_b", 2, Hour);
        var context = CreateContext(DataOf(a, b));
        Clock.Now = Anchor + Hour + 20; context.PeekTreatment("exp_a");
        Clock.Now = Anchor + Hour + 15; context.PeekTreatment("exp_b");
        Clock.Now = Anchor + Hour + 17; context.SetAttribute(ClusterPath, "store-7");
        context.Track("g", null!);
        PublishEvent(context).Attributes!.Last().SetAt.Should().Be(Anchor + Hour + 21);
    }

    [Test]
    public void RebuiltSlotKeepsItsServingUnits()
    {
        var context = CreateContext(DataOf(Linked()));
        Clock.Now = Anchor + Hour + 1;
        context.SetAttribute("city", "Faro");
        context.PeekTreatment("exp_sb");
        context.SetUnit("user_id", "u1");
        context.SetAttribute("user_city", "Porto");
        context.GetTreatment("exp_sb");
        context.Publish();
        Published.Should().ContainSingle();
        Published[0].Event.Units.Select(u => u.Type).Should().Equal(UnitType);
    }

    [Test]
    public void NonClusterAttributeKeepsTheClockReadingAfterARewind()
    {
        var sb = NewSwitchback(1, "exp_sb", 2, Hour);
        var unit = NewSwitchback(2, "exp_unit", 2, Hour);
        unit.RandomizationType = null; unit.SwitchbackClusters = null; unit.SwitchbackStartAt = null; unit.SwitchbackPeriodMs = null;
        unit.Audience = "{\"filter\":[{\"eq\":[{\"var\":\"tier\"},{\"value\":\"gold\"}]}]}";
        unit.AudienceStrict = true;
        var context = CreateContext(DataOf(sb, unit));
        Clock.Now = Anchor + Hour + 20; context.PeekTreatment("exp_sb");
        Clock.Now = Anchor + Hour + 10;
        context.SetAttribute("tier", "gold");
        Clock.Now = Anchor + Hour + 12;
        var v = context.GetTreatment("exp_unit");
        var e = PublishEvent(context);
        var x = e.Exposures!.Single(z => z.Name == "exp_unit");
        e.Attributes!.Single(a => a.Name == "tier").SetAt.Should().BeLessThanOrEqualTo(x.ExposedAt, "Collector records the exposure's attributes at exposedAt");
    }

    [Test]
    public void UnlinkedLateUnitDoesNotSplitTheRequest()
    {
        var context = CreateContext(DataOf(NewSwitchback(2, Hour)));
        Clock.Now = Anchor + Hour + 1;
        context.GetTreatment("exp_sb");
        context.SetUnit("device", "d1");
        context.Track("g", null!);
        context.Publish();
        Published.Should().ContainSingle("device is not a linked unit of exp_sb");
    }

    [Test]
    public void CustomAssignmentRebuildKeepsServingUnits()
    {
        var e = NewSwitchback(2, Hour); e.SwitchbackClusters = Clusters(UnitType, "city", "user_id", "user_city");
        var context = CreateContext(DataOf(e));
        Clock.Now = Anchor + Hour + 1;
        context.SetAttribute("city", "Faro");
        context.PeekTreatment("exp_sb");
        context.SetUnit("user_id", "u1");
        context.SetAttribute("user_city", "Porto");
        context.SetCustomAssignment("exp_sb", 1);
        context.GetTreatment("exp_sb");
        context.Publish();
        Published.Select(p => p.Event.Units.Length).Should().Equal(1);
    }

    // A synchronous Close() on a UI-like thread while another publication is in flight publishes the
    // events queued since in its own flush (the close's "earlier" outcome, reported once the close
    // settles). When that batch splits, the logger still sees every request.
    [Test]
    [Timeout(10000)]
    public async Task SynchronousCloseWithAnOutstandingPublicationLogsEverySplitRequest()
    {
        var gate = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var entered = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var publisher = new Mock<IContextPublisher>();
        publisher.Setup(x => x.PublishAsync(It.IsAny<IContext>(), It.IsAny<PublishEvent>()))
            .Returns<IContext, PublishEvent>(async (_, e) =>
            {
                if (e.Goals != null && e.Goals.Any(g => g.Name == "first"))
                {
                    entered.TrySetResult(true);
                    await gate.Task;
                }
            });
        Publisher = publisher.Object;
        var events = new System.Collections.Concurrent.ConcurrentQueue<(EventType, object)>();
        var logger = new Mock<IContextEventLogger>();
        logger.Setup(x => x.HandleEvent(It.IsAny<IContext>(), It.IsAny<EventType>(), It.IsAny<object>()))
            .Callback<IContext, EventType, object>((_, t, e) => events.Enqueue((t, e)));
        var config = new ContextConfig { PublishDelay = TimeSpan.FromMinutes(10) };
        config.SetUnit(UnitType, Uid);
        var context = Context.CreateWithData(config, DataOf(Linked()), Clock, DataProvider.Object, Publisher,
            logger.Object, new DefaultVariableParser(), new AudienceMatcher(new DefaultAudienceDeserializer()), null);
        Clock.Now = Anchor + Hour + 1;
        context.Track("first", null!);
        var publish = context.PublishAsync();
        (await Task.WhenAny(entered.Task, Task.Delay(5000))).Should().Be(entered.Task);

        context.SetAttribute("city", "Faro");
        context.GetTreatment("exp_sb");
        context.SetUnit("user_id", "u1");
        context.Track("purchase", null!);
        using (var ui = new UiThread())
        {
            var closed = ui.Run(() => context.Close());
            (await Task.WhenAny(closed, Task.Delay(5000))).Should().Be(closed);
            await closed;
        }

        gate.SetResult(true);
        await publish;
        await context.FinalizeAsync();

        var logged = events.Where(e => e.Item1 == EventType.Publish).Select(e => (PublishEvent)e.Item2).ToList();
        logged.Should().HaveCount(3);
        logged.Should().ContainSingle(p => p.Exposures != null && p.Units.Length == 1);
        logged.Should().ContainSingle(p => p.Goals != null && p.Goals.Any(g => g.Name == "purchase") && p.Units.Length == 2);
    }

    [Test]
    public void SplitBatchKeepsTheNonLateExposure()
    {
        var late = Linked();
        var current = NewSwitchback(2, "exp_current", 2, Hour);
        var context = CreateContext(DataOf(late, current));
        Clock.Now = Anchor + Hour + 1;
        context.SetAttribute("city", "Faro");
        context.GetTreatment("exp_sb");
        context.SetUnit("user_id", "u1");
        context.GetTreatment("exp_current");
        context.Publish();
        Published.SelectMany(p => p.Event.Exposures ?? Array.Empty<Exposure>())
            .Select(e => e.Name).Should().Equal("exp_sb", "exp_current");
        Published.Should().HaveCount(2);
    }

    // Round 5: a split request keeps the publishedAt the unsplit batch had. Exposed at +20, a linked
    // unit set later, a goal at +18 after a rewind, published at +15: the goal-only request must not
    // carry publishedAt=+15, which the Collector rejects (achievedAt > publishedAt).
    [Test]
    public void SplitRequestsShareTheBatchPublishedAt()
    {
        var context = CreateContext(DataOf(Linked()));
        Clock.Now = Anchor + Hour + 20;
        context.SetAttribute("city", "Faro");
        context.GetTreatment("exp_sb");
        context.SetUnit("user_id", "u1");
        Clock.Now = Anchor + Hour + 18;
        context.Track("purchase", null!);
        Clock.Now = Anchor + Hour + 15;
        context.Publish();
        Published.Select(p => p.Event.Units.Length).Should().Equal(1, 2);
        Published.Select(p => p.Event.PublishedAt).Should().Equal(Anchor + Hour + 20, Anchor + Hour + 20);
        Published[1].Event.Goals!.Single().AchievedAt.Should().BeLessThanOrEqualTo(Published[1].Event.PublishedAt);
    }

    // Round 5: names are trimmed as the Collector trims them (Java String.trim(): only characters at
    // or below U+0020). A name padded with U+00A0 is published as is and replays as a different name,
    // so it keys no cluster and is not re-dated; one padded with a tab or space keys "city".
    [TestCase("\u00a0city\u00a0", false)]
    [TestCase("\u2003city", false)]
    [TestCase("\tcity ", true)]
    [TestCase("\u0001city\u0000", true)]
    public void AttributeNamesAreTrimmedAsJavaTrims(string name, bool matches)
    {
        var context = CreateContext(DataOf(Linked()));
        context.SetAttribute(name, "Faro");
        Clock.Now = Anchor + Hour + 20;
        context.PeekTreatment("exp_sb");
        context.Track("g", null!);
        var json = Encoding.UTF8.GetString(new DefaultContextEventSerializer().Serialize(PublishEvent(context)));
        json.Should().Contain(JsonSerializer.Serialize(name), "the name is published untrimmed");

        var served = CreateContext(DataOf(Linked()));
        served.SetAttribute(name, "Faro");
        Clock.Now = Anchor + Hour + 20;
        served.GetTreatment("exp_sb");
        served.PendingCount.Should().Be(matches ? 1 : 0);

        var rewound = CreateContext(DataOf(Linked()));
        Clock.Now = Anchor + Hour + 20;
        rewound.PeekTreatment("exp_sb");
        Clock.Now = Anchor + Hour + 10;
        rewound.SetAttribute(name, "Faro");
        rewound.Track("g", null!);
        PublishEvent(rewound).Attributes!.Last().SetAt.Should().Be(Anchor + Hour + (matches ? 21 : 10));
    }

    [TestCase(" city ", true)]
    [TestCase("City", false)]
    public void ClusterRootNamesAreTrimmedButCaseSensitive(string name, bool clamped)
    {
        var experiment = Linked();
        var context = CreateContext(DataOf(experiment));
        Clock.Now = Anchor + Hour + 20;
        context.PeekTreatment("exp_sb");
        Clock.Now = Anchor + Hour + 10;
        context.SetAttribute(name, "Faro");
        context.Track("g", null!);
        PublishEvent(context).Attributes.Last().SetAt.Should().Be(Anchor + Hour + (clamped ? 21 : 10));
    }

    [Test]
    public void EverySourceAndExperimentRegistersItsRoot()
    {
        var a = Linked();
        var b = NewSwitchback(2, "exp_b", 2, Hour);
        b.SwitchbackClusters = Clusters(UnitType, "other/value");
        var context = CreateContext(DataOf(a, b));
        Clock.Now = Anchor + Hour + 20;
        context.PeekTreatment("exp_sb");
        Clock.Now = Anchor + Hour + 10;
        context.SetAttribute("user_city", "Porto");
        context.SetAttribute("other", new Dictionary<string, object> { ["value"] = "Faro" });
        context.Track("g", null!);
        PublishEvent(context).Attributes.TakeLast(2).Select(a => a.SetAt)
            .Should().Equal(Anchor + Hour + 21, Anchor + Hour + 21);
    }

    [Test]
    public void RefreshRetainsRootsWhoseExposuresCanStillBePending()
    {
        var context = CreateContext(DataOf(Linked()));
        Clock.Now = Anchor + Hour + 20;
        context.PeekTreatment("exp_sb");
        var replacement = NewSwitchback(2, "replacement", 2, Hour);
        replacement.SwitchbackClusters = Clusters(UnitType, "other");
        RefreshTo(context, replacement);
        Clock.Now = Anchor + Hour + 10;
        context.SetAttribute("city", "Porto");
        context.Track("g", null!);
        PublishEvent(context).Attributes.Last().SetAt.Should().Be(Anchor + Hour + 21);
    }

    // A unit set from inside a publisher callback (between split requests) never reaches a later
    // request of the same flush: every request uses the units snapshotted when the flush started.
    [Test]
    public void UnitSetDuringASplitFlushDoesNotReachLaterRequests()
    {
        var experiment = NewSwitchback(2, Hour);
        experiment.SwitchbackClusters = Clusters(UnitType, "city", "device_id", "device_city", "user_id", "user_city");
        Context context = null!;
        var sent = new List<PublishEvent>();
        var publisher = new Mock<IContextPublisher>();
        publisher.Setup(x => x.PublishAsync(It.IsAny<IContext>(), It.IsAny<PublishEvent>()))
            .Returns<IContext, PublishEvent>((_, e) =>
            {
                sent.Add(e);
                if (sent.Count == 1) context.SetUnit("user_id", "u1");
                return Task.CompletedTask;
            });
        Publisher = publisher.Object;
        context = CreateContext(DataOf(experiment));
        Clock.Now = Anchor + Hour + 1;
        context.SetAttribute("city", "Faro");
        context.GetTreatment("exp_sb");
        context.SetUnit("device_id", "d1");
        context.Track("purchase", null!);
        context.Publish();

        sent.Should().HaveCount(2);
        sent[0].Units.Select(u => u.Type).Should().Equal(UnitType);
        sent[1].Units.Select(u => u.Type).Should().Equal(UnitType, "device_id");
    }

    // Refresh registers cluster roots outside the data lock. A lookup takes the context lock and
    // then the data lock, so registering under the data lock would deadlock with it. This forces
    // the interleaving: while a setter holds the context lock, a refresh starts (and would take the
    // data lock first) and a lookup queues behind the setter; everything must still complete.
    [Test]
    [Timeout(20000)]
    public void RefreshDuringALookupDoesNotDeadlock()
    {
        var context = CreateContext(DataOf(Linked()));
        DataProvider.Setup(x => x.GetContextDataAsync()).ReturnsAsync(() => DataOf(Linked()));
        Clock.Now = Anchor + Hour + 1;
        context.SetAttribute("city", "Faro");
        Thread? refresh = null;
        Task? lookup = null;
        context.InsideAttributeAppend = () =>
        {
            refresh = new Thread(() => context.Refresh()) { IsBackground = true };
            refresh.Start();
            Thread.Sleep(300);
            lookup = Task.Run(() => context.PeekTreatment("exp_sb"));
            Thread.Sleep(100);
        };
        var setter = Task.Run(() => context.SetAttribute("city", "Faro"));
        setter.Wait(5000).Should().BeTrue("the setter completes");
        refresh!.Join(5000).Should().BeTrue("the refresh completes");
        lookup!.Wait(5000).Should().BeTrue("the lookup completes");
    }

    // The rewind decision and the attribute append are one write-locked step: a serving read that
    // tries to run between them waits, so the appended value is never dated before a read it missed.
    [Test]
    [Timeout(10000)]
    public void ServingReadCannotSlipBetweenTheRewindDecisionAndTheAppend()
    {
        var context = CreateContext(DataOf(Linked()));
        Clock.Now = Anchor + Hour + 20;
        context.SetAttribute("city", "Faro");
        Clock.Now = Anchor + Hour + 10;
        Task? lookup = null;
        var lookupStarted = new ManualResetEventSlim();
        context.InsideAttributeAppend = () =>
        {
            Clock.Now = Anchor + Hour + 30;
            lookup = Task.Run(() =>
            {
                lookupStarted.Set();
                context.PeekTreatment("exp_sb");
            });
            lookupStarted.Wait();
            // The lookup needs the context lock this setter holds: it cannot complete in between.
            lookup.Wait(300).Should().BeFalse("a serving read must not run inside the append");
        };
        context.SetAttribute("city", "Porto");
        lookup!.Wait(5000).Should().BeTrue();
    }

    // A single-threaded SynchronizationContext, like a UI thread.
    private sealed class UiThread : SynchronizationContext, IDisposable
    {
        private readonly System.Collections.Concurrent.BlockingCollection<(SendOrPostCallback, object?)> _queue = new();

        public UiThread()
        {
            new Thread(() =>
            {
                SetSynchronizationContext(this);
                foreach (var (callback, state) in _queue.GetConsumingEnumerable()) callback(state);
            }) { IsBackground = true }.Start();
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
}
