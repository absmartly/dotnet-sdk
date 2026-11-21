using System.Text;
using System.Text.Json;
using ABSmartly.Internal;
using ABSmartly.Models;
using ABSmartly.Services;

namespace ABSmartly.Sdk.Tests.Switchback;

// Split publish requests and the publish-path replay rules: the logger sees every request, a
// failure retries only the undelivered ones, publishedAt bounds every request, and only cluster
// attributes are re-dated after a clock rewind.
[TestFixture]
public class SwitchbackPublishSplitTests : SwitchbackTestBase
{
    private Context WithLogger(List<(EventType, object)> events, ContextData data,
        TaskCompletionSource<bool>? closeLogged = null)
    {
        var logger = new Mock<IContextEventLogger>();
        logger.Setup(x => x.HandleEvent(It.IsAny<IContext>(), It.IsAny<EventType>(), It.IsAny<object>()))
            .Callback<IContext, EventType, object>((_, t, e) =>
            {
                lock (events) events.Add((t, e));
                if (t == EventType.Close) closeLogged?.TrySetResult(true);
            });
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
    public void MixedBatchPublishedAtIncludesTheLatestGoal()
    {
        var context = CreateContext(DataOf(Linked()));
        Clock.Now = Anchor + Hour + 10;
        context.SetAttribute("city", "Faro");
        context.GetTreatment("exp_sb");
        Clock.Now = Anchor + Hour + 20;
        context.Track("purchase", null!);
        Clock.Now = Anchor + Hour + 15;
        var published = PublishEvent(context);
        published.PublishedAt.Should().Be(Anchor + Hour + 20);
        published.Exposures.Should().ContainSingle();
        published.Goals.Should().ContainSingle();
    }

    [Test]
    public void GoalOneMillisecondAheadOfTheClockBoundsPublishedAt()
    {
        var context = CreateContext(DataOf(Linked()));
        Clock.Now = Anchor + Hour + 10;
        context.SetAttribute("city", "Faro");
        context.GetTreatment("exp_sb");
        Clock.Now = Anchor + Hour + 16;
        context.Track("purchase", null!);
        Clock.Now = Anchor + Hour + 15;
        PublishEvent(context).PublishedAt.Should().Be(Anchor + Hour + 16);
    }

    [Test]
    public void FailureRestoresEveryUndeliveredSplitGroup()
    {
        var calls = 0;
        var sent = new List<PublishEvent>();
        var publisher = new Mock<IContextPublisher>();
        publisher.Setup(x => x.PublishAsync(It.IsAny<IContext>(), It.IsAny<PublishEvent>()))
            .Returns<IContext, PublishEvent>((_, e) =>
            {
                if (++calls == 1) throw new InvalidOperationException("down");
                sent.Add(e);
                return Task.CompletedTask;
            });
        Publisher = publisher.Object;
        var a = NewSwitchback(1, "exp_a", 2, Hour); a.SwitchbackClusters = Clusters(UnitType, "city", "device_id", "device_city");
        var b = NewSwitchback(2, "exp_b", 2, Hour); b.SwitchbackClusters = Clusters(UnitType, "city", "device_id", "device_city");
        var context = CreateContext(DataOf(a, b));
        Clock.Now = Anchor + Hour + 1;
        context.SetAttribute("city", "Faro");
        context.GetTreatment("exp_a");
        context.SetUnit("user_id", "u1");
        context.GetTreatment("exp_b");
        context.SetUnit("device_id", "d1");
        context.Track("purchase", null!);
        FluentActions.Invoking(() => context.Publish()).Should().Throw<InvalidOperationException>();
        context.PendingCount.Should().Be(3);
        context.Publish();
        context.PendingCount.Should().Be(0);
        sent.SelectMany(p => p.Exposures ?? Array.Empty<Exposure>()).Select(e => e.Name).Should().Equal("exp_a", "exp_b");
        sent.SelectMany(p => p.Goals ?? Array.Empty<GoalAchievement>()).Select(g => g.Name).Should().Equal("purchase");
        sent.Select(p => p.Units.Length).Should().Equal(1, 2, 3);
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

    // A synchronous close on a UI thread publishes its own split batch, then finishes once an
    // in-flight publication settles, retrying it if it failed: the Publish events are logged in the
    // order the publisher completed them.
    [Test]
    [Timeout(20000)]
    public async Task CloseLogsItsPublishesInOrder()
    {
        var gate = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var entered = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var failedOnce = 0;
        var purchaseSent = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var publisher = new Mock<IContextPublisher>();
        publisher.Setup(x => x.PublishAsync(It.IsAny<IContext>(), It.IsAny<PublishEvent>()))
            .Returns<IContext, PublishEvent>(async (_, e) =>
            {
                if (e.Goals != null && e.Goals.Any(g => g.Name == "first") && Interlocked.Exchange(ref failedOnce, 1) == 0)
                {
                    entered.TrySetResult(true);
                    await gate.Task;
                    throw new InvalidOperationException("down");
                }

                if (e.Goals != null && e.Goals.Any(g => g.Name == "purchase")) purchaseSent.TrySetResult(true);
            });
        Publisher = publisher.Object;
        var events = new List<(EventType, object)>();
        var closeLogged = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var context = WithLogger(events, DataOf(Linked()), closeLogged);
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

        purchaseSent.Task.IsCompleted.Should().BeTrue("the close publishes its own batch first");
        gate.SetResult(true);
        await FluentActions.Awaiting(() => publish).Should().ThrowAsync<InvalidOperationException>();
        await context.FinalizeAsync();
        context.IsClosed().Should().BeTrue();
        (await Task.WhenAny(closeLogged.Task, Task.Delay(5000))).Should().Be(closeLogged.Task, "the close is reported");

        List<PublishEvent> logged;
        lock (events) logged = events.Where(e => e.Item1 == EventType.Publish).Select(e => (PublishEvent)e.Item2).ToList();
        logged.Select(p => string.Join(",", (p.Exposures ?? Array.Empty<Exposure>()).Select(e => e.Name)
            .Concat((p.Goals ?? Array.Empty<GoalAchievement>()).Select(g => g.Name)))).Should().Equal("exp_sb", "purchase", "first");
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

    // A split request keeps the publishedAt the unsplit batch had; the Collector rejects a goal
    // later than its request's publishedAt.
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

    // A partially delivered split retries its goal-only request without the exposure that set the
    // batch instant: the retry is still dated no earlier than the goal, or the Collector rejects it.
    [Test]
    public void GoalRetriedAfterAPartialSplitKeepsAValidPublishedAt()
    {
        var calls = 0;
        var sent = new List<PublishEvent>();
        var publisher = new Mock<IContextPublisher>();
        publisher.Setup(x => x.PublishAsync(It.IsAny<IContext>(), It.IsAny<PublishEvent>()))
            .Returns<IContext, PublishEvent>((_, e) =>
            {
                if (++calls == 2) throw new InvalidOperationException("down");
                sent.Add(e);
                return Task.CompletedTask;
            });
        Publisher = publisher.Object;
        var context = CreateContext(DataOf(Linked()));
        Clock.Now = Anchor + Hour + 20;
        context.SetAttribute("city", "Faro");
        context.GetTreatment("exp_sb");
        context.SetUnit("user_id", "u1");
        Clock.Now = Anchor + Hour + 18;
        context.Track("purchase", null!);
        Clock.Now = Anchor + Hour + 15;
        FluentActions.Invoking(() => context.Publish()).Should().Throw<InvalidOperationException>();
        context.Publish();

        sent.Should().HaveCount(2);
        sent[1].Exposures.Should().BeNull("the delivered exposure is not resent");
        var goal = sent[1].Goals.Should().ContainSingle().Subject;
        goal.AchievedAt.Should().Be(Anchor + Hour + 18);
        sent[1].PublishedAt.Should().Be(Anchor + Hour + 18);
        var json = Encoding.UTF8.GetString(new DefaultContextEventSerializer().Serialize(sent[1]));
        json.Should().Contain($"\"publishedAt\":{Anchor + Hour + 18}");
    }

    // A failure before the first request (here the clock) puts the whole batch back, and a close
    // that hit it settles and can be retried.
    [Test]
    [Timeout(20000)]
    public async Task FailureBeforeTheFirstRequestKeepsTheBatchAndSettlesClose()
    {
        var context = CreateContext(DataOf(Linked()));
        Clock.Now = Anchor + Hour + 1;
        context.SetAttribute("city", "Faro");
        context.GetTreatment("exp_sb");
        context.Track("purchase", null!);
        Clock.NextReadThrows = new InvalidOperationException("clock");
        FluentActions.Invoking(() => context.Publish()).Should().Throw<InvalidOperationException>();
        context.PendingCount.Should().Be(2);
        Published.Should().BeEmpty();

        context.Track("refund", null!);
        Clock.NextReadThrows = new InvalidOperationException("clock");
        await FluentActions.Awaiting(() => context.FinalizeAsync()).Should().ThrowAsync<InvalidOperationException>();
        context.IsClosing().Should().BeFalse();
        context.IsClosed().Should().BeFalse();
        context.PendingCount.Should().Be(3);

        var retry = context.FinalizeAsync();
        (await Task.WhenAny(retry, Task.Delay(5000))).Should().BeSameAs(retry, "the retried close completes");
        await retry;
        context.IsClosed().Should().BeTrue();
        Published.Should().ContainSingle();
        Published[0].Event.Exposures.Should().ContainSingle();
        Published[0].Event.Goals!.Select(g => g.Name).Should().Equal("purchase", "refund");
    }

    [Test]
    public void SplitRequestsUseTheLatestExposureOfTheWholeBatch()
    {
        var a = NewSwitchback(1, "exp_a", 2, Hour); a.SwitchbackClusters = Clusters(UnitType, "city", "user_id", "user_city");
        var b = NewSwitchback(2, "exp_b", 2, Hour); b.SwitchbackClusters = Clusters(UnitType, "city");
        var context = CreateContext(DataOf(a, b));
        Clock.Now = Anchor + Hour + 10;
        context.SetAttribute("city", "Faro");
        context.GetTreatment("exp_a");
        context.SetUnit("user_id", "u1");
        Clock.Now = Anchor + Hour + 20;
        context.GetTreatment("exp_b");
        Clock.Now = Anchor + Hour + 15;
        context.Publish();
        Published.Should().HaveCount(2);
        Published.Select(p => p.Event.PublishedAt).Should().Equal(Anchor + Hour + 20, Anchor + Hour + 20);
        foreach (var request in Published)
            request.Event.Exposures!.Should().OnlyContain(e => e.ExposedAt <= request.Event.PublishedAt);
    }

    // Names are trimmed as the Collector trims them (Java String.trim(): only characters at
    // or below U+0020). A name padded with U+00A0 is published as is and replays as a different name,
    // so it keys no cluster and is not re-dated; one padded with a tab or space keys "city".
    [TestCase("\u00a0city\u00a0", false)]
    [TestCase("\u2003city", false)]
    [TestCase("city\u00a0", false)]
    [TestCase("\u00a0city", false)]
    [TestCase("city\u0085", false)]
    [TestCase("   ", false)]
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
        rewound.SetAttribute("city", "Lisboa");
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
        context.SetAttribute("city", "Lisboa");
        Clock.Now = Anchor + Hour + 20;
        context.PeekTreatment("exp_sb");
        Clock.Now = Anchor + Hour + 10;
        context.SetAttribute(name, "Faro");
        context.Track("g", null!);
        PublishEvent(context).Attributes.Last().SetAt.Should().Be(Anchor + Hour + (clamped ? 21 : 10));
    }

    [Test]
    public void OnlyRootsAPublishableReadUsedAreRedated()
    {
        // user_id is not set, so exp_sb reads city, not user_city.
        var a = Linked();
        var b = NewSwitchback(2, "exp_b", 2, Hour);
        b.SwitchbackClusters = Clusters(UnitType, "other/value");
        var context = CreateContext(DataOf(a, b));
        context.SetAttribute("city", "Lisboa");
        Clock.Now = Anchor + Hour + 20;
        context.PeekTreatment("exp_sb");
        Clock.Now = Anchor + Hour + 10;
        context.SetAttribute("user_city", "Porto");
        context.SetAttribute("other", new Dictionary<string, object> { ["value"] = "Faro" });
        context.SetAttribute("city", "Faro");
        context.Track("g", null!);
        PublishEvent(context).Attributes.TakeLast(3).Select(a => a.SetAt)
            .Should().Equal(Anchor + Hour + 10, Anchor + Hour + 10, Anchor + Hour + 21);
    }

    [Test]
    public void ReadOfOneClusterDoesNotHideAnotherClustersFirstWrite()
    {
        var a = NewSwitchback(1, "exp_a", 2, Hour); a.SwitchbackClusters = Clusters(UnitType, "other_city");
        var b = NewSwitchback(2, "exp_b", 2, Hour); b.SwitchbackClusters = Clusters(UnitType, "city");
        var context = CreateContext(DataOf(a, b));
        context.SetAttribute("other_city", "Lisboa");
        Clock.Now = Anchor + Hour + 20;
        context.GetTreatment("exp_a");
        Clock.Now = Anchor + Hour + 15;
        context.SetAttribute("city", "Faro");
        Clock.Now = Anchor + Hour + 16;
        context.GetTreatment("exp_b").Should().Be(SwitchbackAssigner.Assign("Faro", 1, Seed, 2));
        var published = PublishEvent(context);
        published.Exposures!.Select(e => e.Name).Should().Equal("exp_a", "exp_b");
        published.Attributes!.Single(x => x.Name == "city").SetAt.Should().Be(Anchor + Hour + 15);
    }

    // A silent read (no usable cluster value) publishes nothing.
    [Test]
    public void SilentReadDoesNotRedateItsRoot()
    {
        var context = CreateContext(DataOf(Linked()));
        Clock.Now = Anchor + Hour + 20;
        context.PeekTreatment("exp_sb").Should().Be(0);
        Clock.Now = Anchor + Hour + 10;
        context.SetAttribute("city", "Faro");
        context.Track("g", null!);
        PublishEvent(context).Attributes!.Last().SetAt.Should().Be(Anchor + Hour + 10);
    }

    // A silent read is not reused by a rebuild that can publish: after an ineligible read, a
    // rewound cluster write and an override, the exposure is stamped at a new read, and the
    // Collector's replay at exposedAt finds the cluster and unit that served.
    [Test]
    public void OverrideAfterASilentReadReplaysTheServedCluster()
    {
        var context = CreateContext(DataOf(Linked()));
        context.SetUnit("user_id", "u1");
        context.SetAttribute("city", "Faro");
        context.SetAttribute("user_city", "Lisboa");
        Clock.Now = Anchor + Hour + 20;
        context.PeekTreatment("exp_sb").Should().Be(0, "city and user_city disagree");
        Clock.Now = Anchor + Hour + 10;
        context.SetAttribute("user_city", "Faro");
        context.SetOverride("exp_sb", 1);
        Clock.Now = Anchor + Hour + 12;
        context.GetTreatment("exp_sb").Should().Be(1);
        var published = PublishEvent(context);
        var exposure = published.Exposures!.Single();
        exposure.Should().Match<Exposure>(e => e.Overridden && e.Eligible && e.Unit == UnitType);
        exposure.ExposedAt.Should().Be(Anchor + Hour + 12);
        published.Attributes!.Where(a => a.Name == "user_city" && a.SetAt <= exposure.ExposedAt)
            .OrderBy(a => a.SetAt).Last().Value.Should().Be("Faro");
    }

    // The same after a pre-start silent read and a refresh that turns the experiment full-on: the
    // rebuild reads the cluster again at its own lookup, so it serves the value set since (Porto,
    // outside the allow-list) and publishes nothing the Collector would drop.
    [Test]
    public void FullOnRefreshAfterASilentReadReplaysTheServedCluster()
    {
        var experiment = NewSwitchback(2, Hour);
        experiment.SwitchbackClusters = Clusters(UnitType, "city");
        experiment.SwitchbackClusterValues = new[] { "Faro" };
        var context = CreateContext(DataOf(experiment));
        Clock.Now = Anchor - 100;
        context.SetAttribute("city", "Faro");
        Clock.Now = Anchor - 10;
        context.PeekTreatment("exp_sb").Should().Be(0, "before the start: an eligible cluster, silent");
        Clock.Now = Anchor - 20;
        context.SetAttribute("city", "Porto");
        var fullOn = NewSwitchback(2, Hour);
        fullOn.SwitchbackClusters = Clusters(UnitType, "city");
        fullOn.SwitchbackClusterValues = new[] { "Faro" };
        fullOn.FullOnVariant = 1;
        RefreshTo(context, fullOn);
        Clock.Now = Anchor - 15;
        context.GetTreatment("exp_sb").Should().Be(0, "Porto is outside the allow-list");
        PublishExposures(context).Should().BeEmpty();
    }

    [Test]
    public void RewoundWriteToTheSecondClusterRootIsRedated()
    {
        var context = CreateContext(DataOf(Linked()));
        context.SetUnit("user_id", "u1");
        context.SetAttribute("city", "Faro");
        context.SetAttribute("user_city", "Faro");
        Clock.Now = Anchor + Hour + 20;
        context.GetTreatment("exp_sb");
        Clock.Now = Anchor + Hour + 10;
        context.SetAttribute("user_city", "Porto");
        var published = PublishEvent(context);
        var exposure = published.Exposures!.Single();
        published.Attributes!.Last().SetAt.Should().Be(Anchor + Hour + 21);
        published.Attributes!.Where(a => a.Name == "user_city" && a.SetAt <= exposure.ExposedAt)
            .OrderBy(a => a.SetAt).Last().Value.Should().Be("Faro");
    }

    [Test]
    public void RebuildKeepsTheReadInstantForRedating()
    {
        var context = CreateContext(DataOf(Linked()));
        context.SetAttribute("city", "Faro");
        Clock.Now = Anchor + Hour + 20;
        context.PeekTreatment("exp_sb");
        Clock.Now = Anchor + Hour + 30;
        context.SetOverride("exp_sb", 1);
        context.GetTreatment("exp_sb").Should().Be(1);
        Clock.Now = Anchor + Hour + 25;
        context.SetAttribute("city", "Porto");
        var published = PublishEvent(context);
        published.Exposures!.Single().ExposedAt.Should().Be(Anchor + Hour + 20);
        published.Attributes!.Last().SetAt.Should().Be(Anchor + Hour + 25);
    }

    [Test]
    public void ReadWithNoLinkedUnitIsNotReused()
    {
        var context = CreateContext(new Dictionary<string, string>(), DataOf(Linked()), clusterAttribute: false);
        context.SetAttribute("user_city", "Faro");
        context.SetOverride("exp_sb", 1);
        Clock.Now = Anchor + Hour + 10;
        context.GetTreatment("exp_sb").Should().Be(1);
        context.SetUnit("user_id", "u1");
        Clock.Now = Anchor + Hour + 20;
        context.SetOverride("exp_sb", 0);
        context.GetTreatment("exp_sb").Should().Be(0);
        var exposures = PublishExposures(context);
        exposures.Last().Should().Match<Exposure>(e => e.Unit == "user_id" && e.ExposedAt == Anchor + Hour + 20);
    }

    // The Collector replays an overridden exposure from its cluster too.
    [Test]
    public void OverriddenExposureProtectsItsClusterRoot()
    {
        var experiment = Linked();
        experiment.SwitchbackClusterValues = new[] { "Faro" };
        var context = CreateContext(DataOf(experiment));
        context.SetAttribute("city", "Faro");
        context.SetOverride("exp_sb", 1);
        Clock.Now = Anchor + Hour + 20;
        context.GetTreatment("exp_sb").Should().Be(1);
        Clock.Now = Anchor + Hour + 10;
        context.SetAttribute("city", "Porto");
        var published = PublishEvent(context);
        var exposure = published.Exposures!.Single();
        published.Attributes!.Last().SetAt.Should().Be(Anchor + Hour + 21);
        published.Attributes!.Where(a => a.Name == "city" && a.SetAt <= exposure.ExposedAt)
            .OrderBy(a => a.SetAt).Last().Value.Should().Be("Faro");
    }

    // A rebuild that reuses a read made before a linked unit was set protects only the roots that
    // read used, so another experiment's first value for the new unit's root stays visible.
    [Test]
    public void CachedRebuildProtectsOnlyTheRootsItsReadUsed()
    {
        var a = NewSwitchback(1, "exp_a", 2, Hour); a.SwitchbackClusters = Clusters(UnitType, "city", "user_id", "user_city");
        var b = NewSwitchback(2, "exp_b", 2, Hour); b.SwitchbackClusters = Clusters("user_id", "user_city"); b.FullOnVariant = 1;
        var context = CreateContext(DataOf(a, b));
        context.SetAttribute("city", "Faro");
        Clock.Now = Anchor + Hour + 20;
        context.GetTreatment("exp_a");
        context.SetUnit("user_id", "u1");
        Clock.Now = Anchor + Hour + 30;
        context.SetOverride("exp_a", 1);
        context.GetTreatment("exp_a");
        Clock.Now = Anchor + Hour + 15;
        context.SetAttribute("user_city", "Faro");
        Clock.Now = Anchor + Hour + 16;
        context.GetTreatment("exp_b").Should().Be(1);
        context.Publish();
        Published.Last().Event.Attributes!.Single(x => x.Name == "user_city").SetAt.Should().Be(Anchor + Hour + 15);
        Published.SelectMany(p => p.Event.Exposures ?? Array.Empty<Exposure>()).Should().Contain(e => e.Name == "exp_b");
    }

    // A structured attribute is published, and read by later lookups, as it was when first read:
    // reusing the same dictionary next period cannot rewrite the earlier exposure's replay.
    [Test]
    public void ReusedAttributeObjectKeepsTheEarlierPeriodsValue()
    {
        var experiment = NewSwitchback(2, Hour);
        experiment.SwitchbackClusters = Clusters(UnitType, "store/region");
        experiment.SwitchbackClusterValues = new[] { "Faro" };
        var context = CreateContext(DataOf(experiment));
        var store = new Dictionary<string, object> { ["region"] = "Faro" };
        context.SetAttribute("store", store);
        Clock.Now = Anchor + 2 * Hour - 1;
        context.GetTreatment("exp_sb");
        store["region"] = "Porto";
        Clock.Now = Anchor + 2 * Hour + 1;
        context.SetAttribute("store", store);
        context.GetTreatment("exp_sb").Should().Be(0, "Porto is outside the allow-list");
        var published = PublishEvent(context);
        var exposure = published.Exposures!.Single();
        exposure.ExposedAt.Should().Be(Anchor + 2 * Hour - 1);
        var json = Encoding.UTF8.GetString(new DefaultContextEventSerializer().Serialize(published));
        json.Should().Contain("{\"name\":\"store\",\"value\":{\"region\":\"Faro\"},\"setAt\":" + Anchor + "}");
        json.Should().Contain("{\"name\":\"store\",\"value\":{\"region\":\"Porto\"},\"setAt\":" + (Anchor + 2 * Hour + 1) + "}");
        published.Attributes!.First(x => x.Name == "store").Value.Should().BeSameAs(store, "PublishEvent consumers still see the caller's object");
    }

    // A structured value is frozen when set: mutating the object afterwards, without setting it
    // again, changes neither the cluster a lookup reads nor what is published.
    [Test]
    public void MutatingASetObjectDoesNotChangeItsCluster()
    {
        var experiment = NewSwitchback(2, Hour);
        experiment.SwitchbackClusters = Clusters(UnitType, "store/region");
        var context = CreateContext(DataOf(experiment));
        var store = new Dictionary<string, object> { ["region"] = "Faro" };
        context.SetAttribute("store", store);
        store["region"] = "Porto";
        Clock.Now = Anchor + Hour + 1;
        context.GetTreatment("exp_sb").Should().Be(SwitchbackAssigner.Assign("Faro", 1, Seed, 2));
        var json = Encoding.UTF8.GetString(new DefaultContextEventSerializer().Serialize(PublishEvent(context)));
        json.Should().Contain("{\"name\":\"store\",\"value\":{\"region\":\"Faro\"},");
    }

    // A lookup reads the frozen value even when the published object was mutated after it was set
    // before the switchback data arrived (frozen at the first read instead).
    [Test]
    public void LookupReadsTheValueFrozenAtTheFirstRead()
    {
        var experiment = NewSwitchback(2, Hour);
        experiment.SwitchbackClusters = Clusters(UnitType, "store/region");
        var context = CreateContext(DataOf(AsUnit(NewSwitchback(2, Hour))));
        var store = new Dictionary<string, object> { ["region"] = "Faro" };
        context.SetAttribute("store", store);
        RefreshTo(context, experiment);
        Clock.Now = Anchor + Hour + 1;
        var served = context.GetTreatment("exp_sb");
        served.Should().Be(SwitchbackAssigner.Assign("Faro", 1, Seed, 2));
        store["region"] = "Porto";
        Clock.Now = Anchor + 2 * Hour + 1;
        context.GetTreatment("exp_sb").Should().Be(SwitchbackAssigner.Assign("Faro", 2, Seed, 2));
    }

    [Test]
    public void ReusedListKeepsItsPublishedClusterValue()
    {
        var experiment = NewSwitchback(2, Hour);
        experiment.SwitchbackClusters = Clusters(UnitType, "stores/0/region");
        var context = CreateContext(DataOf(experiment));
        var row = new Dictionary<string, object> { ["region"] = "Faro" };
        var stores = new List<Dictionary<string, object>> { row };
        context.SetAttribute("stores", stores);
        Clock.Now = Anchor + Hour + 1;
        context.GetTreatment("exp_sb");
        row["region"] = "Porto";
        var json = Encoding.UTF8.GetString(new DefaultContextEventSerializer().Serialize(PublishEvent(context)));
        json.Should().Contain("\"value\":[{\"region\":\"Faro\"}]");
    }

    [Test]
    public void AttributeBeforeSwitchbacksIsFrozenOnlyAtItsFirstClusterRead()
    {
        var context = CreateContext(DataOf(AsUnit(NewSwitchback(2, Hour))));
        var store = new Dictionary<string, object> { ["region"] = "Faro" };
        context.SetAttribute("store", store);
        store["region"] = "Porto";
        var experiment = NewSwitchback(2, Hour);
        experiment.SwitchbackClusters = Clusters(UnitType, "store/region");
        experiment.SwitchbackClusterValues = new[] { "Porto" };
        RefreshTo(context, experiment);
        Clock.Now = Anchor + Hour + 1;
        context.GetTreatment("exp_sb");
        var json = Encoding.UTF8.GetString(new DefaultContextEventSerializer().Serialize(PublishEvent(context)));
        json.Should().Contain("\"value\":{\"region\":\"Porto\"}");
    }

    private sealed class ThrowingProfile
    {
        public string Lazy => throw new NotImplementedException("customer getter");
    }

    [Test]
    public void BestEffortFreezeDoesNotLeakCustomerGetterExceptions()
    {
        var context = CreateContext(DataOf(Linked()));
        var profile = new ThrowingProfile();
        FluentActions.Invoking(() => context.SetAttribute("profile", profile)).Should().NotThrow();
        // The first-read path also reaches Freeze when the value was set before switchbacks.
        var ordinary = CreateContext(DataOf(AsUnit(NewSwitchback(2, Hour))));
        ordinary.SetAttribute("profile", profile);
        var experiment = NewSwitchback(2, Hour);
        experiment.SwitchbackClusters = Clusters(UnitType, "profile/city");
        RefreshTo(ordinary, experiment);
        Clock.Now = Anchor + Hour + 1;
        FluentActions.Invoking(() => ordinary.GetTreatment("exp_sb")).Should().NotThrow();
        ordinary.PendingCount.Should().Be(0);
    }

    [Test]
    public void RefreshRetainsRootsWhoseExposuresCanStillBePending()
    {
        var context = CreateContext(DataOf(Linked()));
        context.SetAttribute("city", "Lisboa");
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

    // A lookup takes the context lock and then the data lock, so a refresh that took the context
    // lock while holding the data lock could deadlock with it. Refresh takes no context lock: it
    // completes while a setter holds it.
    [Test]
    [Timeout(20000)]
    public void RefreshCompletesWhileTheContextLockIsHeld()
    {
        var context = CreateContext(DataOf(Linked()));
        DataProvider.Setup(x => x.GetContextDataAsync()).ReturnsAsync(() => DataOf(Linked()));
        var refreshed = false;
        context.InsideAttributeAppend = () =>
        {
            var refresh = new Thread(() => context.Refresh()) { IsBackground = true };
            refresh.Start();
            refreshed = refresh.Join(5000);
        };
        context.SetAttribute("city", "Faro");
        refreshed.Should().BeTrue("a refresh must not wait for the context lock");
    }

    // The rewind decision and the attribute append are one write-locked step, so the appended value
    // is never dated before a read it missed.
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
