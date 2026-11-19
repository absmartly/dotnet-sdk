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
