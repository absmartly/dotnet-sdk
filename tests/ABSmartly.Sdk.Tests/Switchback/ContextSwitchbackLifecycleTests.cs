using System.Text;
using ABSmartly.Internal;
using ABSmartly.Models;
using ABSmartly.Services;

namespace ABSmartly.Sdk.Tests.Switchback;

// The rest of java-sdk ContextSwitchbackTest: the exposure-flags table, exposedAt stamping, the
// bounded per-period cache, epochs across refreshes, rules on switchback slots, and concurrency.
[TestFixture]
public class ContextSwitchbackLifecycleTests : SwitchbackTestBase
{
    // The branches of the SWITCHBACK.md "Exposure flags" table: every branch an eligible-cluster
    // switchback shares with a unit experiment.
    public enum ParityBranch
    {
        Override,
        OverrideOverStrictMismatch,
        Rule,
        RuleOverStrictMismatch,
        StrictMismatch,
        StrictMismatchOverFullOn,
        StrictMismatchOverTrafficMiss,
        NonStrictMismatch,
        FullOn,
        TrafficMiss,
        Custom,
        Draw
    }

    private static void Configure(ParityBranch branch, Experiment experiment)
    {
        switch (branch)
        {
            case ParityBranch.OverrideOverStrictMismatch:
            case ParityBranch.RuleOverStrictMismatch:
            case ParityBranch.StrictMismatch:
                experiment.Audience = StrictGoldAudience;
                experiment.AudienceStrict = true;
                break;
            case ParityBranch.StrictMismatchOverFullOn:
                experiment.Audience = StrictGoldAudience;
                experiment.AudienceStrict = true;
                experiment.FullOnVariant = 2;
                break;
            case ParityBranch.StrictMismatchOverTrafficMiss:
                experiment.Audience = StrictGoldAudience;
                experiment.AudienceStrict = true;
                experiment.TrafficSplit = new[] { 1.0, 0.0 };
                break;
            case ParityBranch.NonStrictMismatch:
                experiment.Audience = StrictGoldAudience;
                break;
            case ParityBranch.FullOn:
                experiment.FullOnVariant = 2;
                break;
            case ParityBranch.TrafficMiss:
                experiment.TrafficSplit = new[] { 1.0, 0.0 };
                break;
        }

        if (branch is ParityBranch.Rule or ParityBranch.RuleOverStrictMismatch) experiment.AssignmentRules = AssignVariant2;
    }

    private static void Configure(ParityBranch branch, Context context)
    {
        if (branch is ParityBranch.Override or ParityBranch.OverrideOverStrictMismatch) context.SetOverride("exp_parity", 2);
        else if (branch == ParityBranch.Custom) context.SetCustomAssignment("exp_parity", 2);
    }

    // Columns: assigned, eligible, overridden, fullOn, custom, audienceMismatch, ruleOverride.
    private static readonly Dictionary<ParityBranch, bool[]> ExposureFlags = new()
    {
        [ParityBranch.Override] = new[] { false, true, true, false, false, false, false },
        [ParityBranch.OverrideOverStrictMismatch] = new[] { false, true, true, false, false, false, false },
        [ParityBranch.Rule] = new[] { false, true, false, false, false, false, true },
        [ParityBranch.RuleOverStrictMismatch] = new[] { false, true, false, false, false, false, true },
        [ParityBranch.StrictMismatch] = new[] { false, true, false, false, false, true, false },
        [ParityBranch.StrictMismatchOverFullOn] = new[] { false, true, false, false, false, true, false },
        [ParityBranch.StrictMismatchOverTrafficMiss] = new[] { false, true, false, false, false, true, false },
        [ParityBranch.NonStrictMismatch] = new[] { true, true, false, false, false, true, false },
        [ParityBranch.FullOn] = new[] { true, true, false, true, false, false, false },
        [ParityBranch.TrafficMiss] = new[] { true, false, false, false, false, false, false },
        [ParityBranch.Custom] = new[] { true, true, false, false, true, false, false },
        [ParityBranch.Draw] = new[] { true, true, false, false, false, false, false }
    };

    // The variant each branch serves at Anchor + 2 * Hour + 3 (period 2, whose 3-arm draw is 1).
    // Override, rule, full-on and custom force arm 2, which differs from both control and the draw.
    private static readonly Dictionary<ParityBranch, int> ExpectedVariant = new()
    {
        [ParityBranch.Override] = 2,
        [ParityBranch.OverrideOverStrictMismatch] = 2,
        [ParityBranch.Rule] = 2,
        [ParityBranch.RuleOverStrictMismatch] = 2,
        [ParityBranch.StrictMismatch] = 0,
        [ParityBranch.StrictMismatchOverFullOn] = 0,
        [ParityBranch.StrictMismatchOverTrafficMiss] = 0,
        [ParityBranch.NonStrictMismatch] = Expected3Arms[2],
        [ParityBranch.FullOn] = 2,
        [ParityBranch.TrafficMiss] = 0,
        [ParityBranch.Custom] = 2,
        [ParityBranch.Draw] = Expected3Arms[2]
    };

    private static bool[] Flags(Exposure e) =>
        new[] { e.Assigned, e.Eligible, e.Overridden, e.FullOn, e.Custom, e.AudienceMismatch, e.RuleOverride };

    private Exposure ExposeOnce(Experiment experiment, ParityBranch branch)
    {
        Configure(branch, experiment);
        var context = CreateContext(DataOf(experiment));
        Configure(branch, context);
        context.GetTreatment("exp_parity");
        var exposures = PublishExposures(context);
        exposures.Should().ContainSingle(branch.ToString());
        return exposures[0];
    }

    // The same branch run on a unit experiment and on an eligible-cluster switchback publishes the
    // same exposure apart from the unit and the variant, and both match the SWITCHBACK.md table.
    [Test]
    public void ExposureFlagsMatchAUnitExperimentForEveryBranch([Values] ParityBranch branch)
    {
        Clock.Now = Anchor + 2 * Hour + 3;
        Expected3Arms[2].Should().Be(1);

        var unitExposure = ExposeOnce(AsUnit(NewSwitchback(1, "exp_parity", 3, Hour)), branch);
        var switchbackExposure = ExposeOnce(NewSwitchback(1, "exp_parity", 3, Hour), branch);

        Flags(unitExposure).Should().Equal(ExposureFlags[branch], "unit {0}", branch);
        Flags(switchbackExposure).Should().Equal(ExposureFlags[branch], "switchback {0}", branch);
        switchbackExposure.Variant.Should().Be(ExpectedVariant[branch], "switchback variant {0}", branch);
        if (branch is not (ParityBranch.Draw or ParityBranch.NonStrictMismatch))
            unitExposure.Variant.Should().Be(ExpectedVariant[branch], "unit variant {0}", branch);
        switchbackExposure.Id.Should().Be(unitExposure.Id);
        switchbackExposure.Name.Should().Be(unitExposure.Name);
        switchbackExposure.ExposedAt.Should().Be(unitExposure.ExposedAt);
    }

    // A traffic miss on an eligible cluster is recorded: eligible=false, variant 0, under the
    // serving unit. An ineligible cluster, by contrast, records nothing.
    [Test]
    public void TrafficMissOnAnEligibleClusterPublishesAnIneligibleExposure()
    {
        var miss = NewSwitchback(1, "exp_miss", 2, Hour);
        miss.TrafficSplit = new[] { 1.0, 0.0 };
        var ineligibleCluster = NewSwitchback(2, "exp_cluster_ineligible", 2, Hour);
        ineligibleCluster.TrafficSplit = new[] { 1.0, 0.0 };
        ineligibleCluster.SwitchbackClusterValues = new[] { "other-store" };
        var context = CreateContext(DataOf(miss, ineligibleCluster));
        Clock.Now = Anchor + 2 * Hour + 3;
        context.GetTreatment("exp_miss").Should().Be(0);
        context.GetTreatment("exp_cluster_ineligible").Should().Be(0);
        PublishExposures(context).Should().Equal(NewExposure(1, "exp_miss", 0, Anchor + 2 * Hour + 3, true, false));
    }

    [Test]
    public void CustomFieldValuesAndUnitLookupsWork()
    {
        var context = CreateContext(DataOf(NewSwitchback(2, Hour)));
        context.Units.Should().Equal(new Dictionary<string, string> { [UnitType] = Uid });
        context.GetCustomFieldKeys().Should().BeEmpty();
        context.GetCustomFieldValue("exp_sb", "missing").Should().BeNull();
    }

    // Cluster paths are parsed once, when the data is installed: a cached lookup does not re-parse
    // them (re-parsing allocates ~536 bytes a lookup for this path; a cached peek ~40).
    [Test]
    public void CachedReadsDoNotReparseClusterPaths()
    {
        var experiment = NewSwitchback(2, Hour);
        experiment.SwitchbackClusters = Clusters(UnitType, "location/stores/0/city");
        var context = CreateContext(DataOf(experiment));
        context.SetAttribute("location", new { stores = new[] { new { city = "Faro" } } });
        Clock.Now = Anchor + 5;
        for (var i = 0; i < 10000; ++i) context.PeekTreatment("exp_sb");
        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 10000; ++i) context.PeekTreatment("exp_sb");
        (GC.GetAllocatedBytesForCurrentThread() - before).Should().BeLessThan(2_000_000);
    }

    [Test]
    public void PerPeriodCacheIsBounded()
    {
        var context = CreateContext(DataOf(NewSwitchback(2, Hour)));
        for (var i = 0; i < 50; ++i)
        {
            Clock.Now = Anchor + i * Hour;
            context.GetTreatment("exp_sb");
        }

        AssignmentCacheSize(context).Should().Be(2);
        context.PendingCount.Should().Be(50);
    }

    // unitType on a switchback payload is never used for assignment: the cluster map decides.
    [Test]
    public void UnitTypeIsNotUsedForSwitchbackAssignment()
    {
        var experiment = NewSwitchback(2, Hour);
        experiment.UnitType = "user_id";
        var context = CreateContext(new Dictionary<string, string> { [UnitType] = Uid, ["user_id"] = "u1" }, DataOf(experiment));
        for (var i = 0; i < 3; ++i)
        {
            Clock.Now = Anchor + i * Hour;
            context.GetTreatment("exp_sb").Should().Be(Expected2Arms[i]);
        }

        var exposures = PublishExposures(context);
        exposures.Should().HaveCount(3);
        exposures.Should().OnlyContain(e => e.Unit == UnitType);
    }

    [Test]
    public void PublishPayloadHasNoNewFields()
    {
        var context = CreateContext(DataOf(NewSwitchback(2, Hour)));
        Clock.Now = Anchor + 5;
        context.GetTreatment("exp_sb");
        Clock.Now = Anchor + Hour + 7;
        context.GetTreatment("exp_sb");
        Clock.Now = Anchor + Hour + 9;

        var json = Encoding.UTF8.GetString(new DefaultContextEventSerializer().Serialize(PublishEvent(context)));
        json.Should().Be("{\"hashed\":true,\"units\":[{\"type\":\"store_id\",\"uid\":\"" + UidHash + "\"}]," +
                         "\"publishedAt\":" + (Anchor + Hour + 9) + "," +
                         "\"exposures\":[" +
                         "{\"id\":1,\"name\":\"exp_sb\",\"unit\":\"store_id\",\"variant\":" + Expected2Arms[0] +
                         ",\"exposedAt\":" + (Anchor + 5) +
                         ",\"assigned\":true,\"eligible\":true,\"overridden\":false,\"fullOn\":false,\"custom\":false," +
                         "\"audienceMismatch\":false,\"ruleOverride\":false}," +
                         "{\"id\":1,\"name\":\"exp_sb\",\"unit\":\"store_id\",\"variant\":" + Expected2Arms[1] +
                         ",\"exposedAt\":" + (Anchor + Hour + 7) +
                         ",\"assigned\":true,\"eligible\":true,\"overridden\":false,\"fullOn\":false,\"custom\":false," +
                         "\"audienceMismatch\":false,\"ruleOverride\":false}" +
                         "]," +
                         // The cluster attribute is published timestamped, as before: the Collector
                         // re-resolves the cluster at exposedAt from it.
                         "\"attributes\":[{\"name\":\"" + ClusterPath + "\",\"value\":\"" + ClusterKey + "\",\"setAt\":" + Anchor + "}]}");
        Expected2Arms[0].Should().NotBe(Expected2Arms[1]);
    }

    [Test]
    public void PeriodsPublishedAcrossFlushesStayOnePerPeriod()
    {
        var context = CreateContext(DataOf(NewSwitchback(2, Hour)));
        Clock.Now = Anchor;
        context.GetTreatment("exp_sb");
        context.Publish();
        Clock.Now = Anchor + 1;
        context.GetTreatment("exp_sb");
        context.PendingCount.Should().Be(0);
        Clock.Now = Anchor + Hour;
        context.GetTreatment("exp_sb");
        context.Publish();

        Published.Should().HaveCount(2);
        Published.SelectMany(p => p.Event.Exposures).Select(e => e.Variant).Should().Equal(Expected2Arms[0], Expected2Arms[1]);
    }

    // The clock crosses into period 1 between resolution (first read) and any later read; the
    // exposure must still carry the instant that selected period 0.
    [Test]
    public void TreatmentExposureIsStampedWithTheResolutionInstant()
    {
        var context = CreateContext(DataOf(NewSwitchback(2, Hour)));
        Clock.Now = Anchor + Hour;
        Clock.NextRead = Anchor + Hour - 1;

        context.GetTreatment("exp_sb").Should().Be(Expected2Arms[0]);
        PublishExposures(context).Should().Equal(NewExposure(1, "exp_sb", Expected2Arms[0], Anchor + Hour - 1, true, true));
    }

    [Test]
    public void VariableExposureIsStampedWithTheResolutionInstant()
    {
        var covered = NewSwitchback(2, Hour);
        covered.HoldoutIds = new[] { 11 };
        var context = CreateContext(new ContextData(new[] { covered }, new[] { Holdout(new[] { 0.0, 1.0 }) }));
        Clock.Now = Anchor + Hour;
        Clock.NextRead = Anchor + Hour - 1;

        context.GetVariableValue("sb.arm", -1).Should().Be(Expected2Arms[0]);
        PublishExposures(context).Should().Equal(NewExposure(1, "exp_sb", Expected2Arms[0], Anchor + Hour - 1, true, true));
    }

    // One GetVariableValue reads the clock once for every candidate, as Java: the first candidate
    // (linked only to an unset unit) does not apply, and the second still resolves the period that
    // first reading selected, not the one a later reading would.
    [Test]
    public void VariableCandidatesShareOneClockReading()
    {
        var unlinked = NewSwitchback(1, "exp_sb_unlinked", 2, Hour);
        unlinked.SwitchbackClusters = Clusters("device_id", ClusterPath);
        var served = NewSwitchback(2, "exp_sb", 2, Hour);
        var context = CreateContext(DataOf(unlinked, served));
        Clock.Now = Anchor + Hour;
        Clock.NextRead = Anchor + Hour - 1;

        context.GetVariableValue("sb.arm", -1).Should().Be(Expected2Arms[0]);
        PublishExposures(context).Should().Equal(NewExposure(2, "exp_sb", Expected2Arms[0], Anchor + Hour - 1, true, true));
    }

    // A peek caches the period's assignment without exposing; the later GetTreatment exposes it
    // stamped with the instant the slot read its cluster (the peek's), so the Collector, which
    // re-resolves the cluster from the published attributes at exposedAt, sees what served.
    [Test]
    public void ExposureAfterPeekIsStampedWhenTheClusterWasRead()
    {
        var context = CreateContext(DataOf(NewSwitchback(2, Hour)));
        Clock.Now = Anchor + 10;
        context.PeekTreatment("exp_sb").Should().Be(Expected2Arms[0]);
        Clock.Now = Anchor + 20 * 60_000L;
        context.GetTreatment("exp_sb").Should().Be(Expected2Arms[0]);
        PublishExposures(context).Should().Equal(NewExposure(1, "exp_sb", Expected2Arms[0], Anchor + 10, true, true));
    }

    [Test]
    public void SamePeriodClusterChangeAfterAPeekReplaysTheServedCluster()
    {
        var experiment = NewSwitchback(2, Hour);
        experiment.SwitchbackClusters = Clusters(UnitType, "city");
        experiment.SwitchbackClusterValues = new[] { "Faro" };
        var context = CreateContext(DataOf(experiment));
        Clock.Now = Anchor + Hour + 1;
        context.SetAttribute("city", "Faro");
        var served = context.PeekTreatment("exp_sb");
        served.Should().Be(SwitchbackAssigner.Assign("Faro", 1, Seed, 2));
        Clock.Now = Anchor + Hour + 2;
        context.SetAttribute("city", "Porto");
        context.GetTreatment("exp_sb").Should().Be(served);

        var published = PublishEvent(context);
        var exposure = published.Exposures.Should().ContainSingle().Subject;
        exposure.Should().Be(NewExposure(1, "exp_sb", served, Anchor + Hour + 1, true, true));
        var replayed = published.Attributes.Where(a => a.Name == "city" && a.SetAt <= exposure.ExposedAt)
            .OrderBy(a => a.SetAt).Last();
        replayed.Value.Should().Be("Faro");
        ((exposure.ExposedAt - Anchor) / Hour).Should().Be(1);
    }

    // Documented limit, deliberately kept: a cluster attribute changed in the same millisecond as the
    // read that served it publishes with the same setAt, and the Collector (last set wins on a tie)
    // replays the new value. Shifting such an update by 1 ms (tried in 63fa716, reverted in 3560924)
    // moved unrelated attributes past their goals and exposures and broke last-write-wins for other
    // switchbacks, so this pins the limit as documented in the README.
    [Test]
    public void SameMillisecondUpdateAfterAReadKeepsItsTimestamp()
    {
        var experiment = NewSwitchback(3, Hour);
        experiment.SwitchbackClusters = Clusters(UnitType, "city");
        var context = CreateContext(DataOf(experiment));
        Clock.Now = Anchor + Hour + 1;
        context.SetAttribute("city", "Faro");
        var served = context.PeekTreatment("exp_sb");
        context.SetAttribute("city", "Porto");
        context.GetTreatment("exp_sb").Should().Be(served);
        var published = PublishEvent(context);
        published.Exposures.Single().ExposedAt.Should().Be(Anchor + Hour + 1);
        published.Attributes.Where(a => a.Name == "city").Select(a => a.SetAt)
            .Should().Equal(Anchor + Hour + 1, Anchor + Hour + 1);
    }

    // A clock moved back after the cluster was read must not put the exposure after
    // publishedAt, which the Collector rejects along with the whole request (goals included).
    [Test]
    public void ClockRollbackKeepsTheRequestValid()
    {
        var context = CreateContext(DataOf(NewSwitchback(2, Hour)));
        Clock.Now = Anchor + Hour + 20;
        var served = context.PeekTreatment("exp_sb");
        Clock.Now = Anchor + Hour + 15;
        context.GetTreatment("exp_sb").Should().Be(served);
        context.Track("goal", null!);
        var published = PublishEvent(context);
        published.Exposures.Single().ExposedAt.Should().Be(Anchor + Hour + 20);
        published.Goals.Single().AchievedAt.Should().Be(Anchor + Hour + 15);
        published.PublishedAt.Should().Be(Anchor + Hour + 20);

        Clock.Now = Anchor + Hour + 40;
        context.Track("later", null!);
        PublishEvent(context).PublishedAt.Should().Be(Anchor + Hour + 40);
    }

    [Test]
    public void ClockRollbackClampsToTheLatestExposureOfTheBatch()
    {
        var context = CreateContext(DataOf(NewSwitchback(1, "exp_a", 2, Hour), NewSwitchback(2, "exp_b", 2, Hour)));
        Clock.Now = Anchor + Hour + 10;
        context.PeekTreatment("exp_a");
        Clock.Now = Anchor + Hour + 20;
        context.PeekTreatment("exp_b");
        Clock.Now = Anchor + Hour + 5;
        context.GetTreatment("exp_a");
        context.GetTreatment("exp_b");
        var published = PublishEvent(context);
        published.Exposures.Select(e => e.ExposedAt).Should().Equal(Anchor + Hour + 10, Anchor + Hour + 20);
        published.PublishedAt.Should().Be(Anchor + Hour + 20);
    }

    // The Collector replays the cluster at exposedAt from the published
    // attributes, latest setAt at or before it. After a clock rewind, a later cluster change must not
    // be dated before the exposure it follows, or the replay would see it instead of the served value.
    [Test]
    public void AttributeWrittenAfterAClockRewindKeepsTheServedClusterOnReplay()
    {
        var experiment = NewSwitchback(2, Hour);
        experiment.SwitchbackClusters = Clusters(UnitType, "city");
        experiment.SwitchbackClusterValues = new[] { "Faro" };
        var context = CreateContext(DataOf(experiment));
        Clock.Now = Anchor + Hour + 1;
        context.SetAttribute("city", "Faro");
        Clock.Now = Anchor + Hour + 20;
        var served = context.PeekTreatment("exp_sb");
        Clock.Now = Anchor + Hour + 15;
        context.GetTreatment("exp_sb").Should().Be(served);
        Clock.Now = Anchor + Hour + 17;
        context.SetAttribute("city", "Porto");
        Clock.Now = Anchor + Hour + 19;
        var published = PublishEvent(context);

        var exposure = published.Exposures.Single();
        exposure.ExposedAt.Should().Be(Anchor + Hour + 20);
        published.PublishedAt.Should().BeGreaterThanOrEqualTo(exposure.ExposedAt);
        Replay(published, "city", exposure.ExposedAt).Should().Be("Faro");
        published.Attributes.Last().SetAt.Should().Be(Anchor + Hour + 21);
    }

    // The rewind protection keeps the latest read of a root, not the first: a cluster read in two
    // periods, then changed after the clock went back between them, is dated after the later read,
    // so both periods' exposures still replay the served value.
    [Test]
    public void RewindProtectsTheLatestReadOfARoot()
    {
        var experiment = NewSwitchback(2, Hour);
        experiment.SwitchbackClusters = Clusters(UnitType, "city");
        var context = CreateContext(DataOf(experiment));
        Clock.Now = Anchor + Hour + 1;
        context.SetAttribute("city", "Faro");
        Clock.Now = Anchor + Hour + 20;
        context.GetTreatment("exp_sb");
        Clock.Now = Anchor + 2 * Hour + 5;
        context.GetTreatment("exp_sb");
        Clock.Now = Anchor + 2 * Hour + 1;
        context.SetAttribute("city", "Porto");
        Clock.Now = Anchor + 2 * Hour + 10;
        var published = PublishEvent(context);

        published.Exposures.Should().HaveCount(2);
        foreach (var exposure in published.Exposures)
            Replay(published, "city", exposure.ExposedAt).Should().Be("Faro");
        published.Attributes.Last().SetAt.Should().Be(Anchor + 2 * Hour + 6);
    }

    [Test]
    public void AttributeSetAtIsTheClockReadingWithoutARewind()
    {
        var context = CreateContext(DataOf(NewSwitchback(2, Hour)));
        Clock.Now = Anchor + Hour + 20;
        context.PeekTreatment("exp_sb");
        Clock.Now = Anchor + Hour + 30;
        context.SetAttribute("later", 1);
        context.Attributes.Should().ContainKey("later");
        context.Track("g", null!);
        PublishEvent(context).Attributes.Last().SetAt.Should().Be(Anchor + Hour + 30);
    }

    // A switchback exposure queued before another linked unit is set
    // (a login) is published with the units present when its cluster was served, in a request of its
    // own, so the Collector replays the cluster that served instead of a disagreement.
    [Test]
    public void ExposureQueuedBeforeALinkedUnitIsAddedReplaysItsServingUnits()
    {
        var experiment = NewSwitchback(2, Hour);
        experiment.SwitchbackClusters = Clusters(UnitType, "city", "user_id", "user_city");
        var context = CreateContext(DataOf(experiment));
        Clock.Now = Anchor + Hour + 1;
        context.SetAttribute("city", "Faro");
        context.SetAttribute("user_city", "Porto");
        var served = context.GetTreatment("exp_sb");
        served.Should().Be(SwitchbackAssigner.Assign("Faro", 1, Seed, 2));
        context.SetUnit("user_id", "u1");
        context.Track("purchase", null!);
        context.Publish();

        var requests = Published.Select(p => p.Event).ToList();
        requests.Should().HaveCount(2);
        requests[0].Units.Select(u => u.Type).Should().Equal(UnitType);
        requests[0].Exposures.Should().ContainSingle().Which.Variant.Should().Be(served);
        requests[0].Goals.Should().BeNull();
        requests[1].Units.Select(u => u.Type).Should().Equal(UnitType, "user_id");
        requests[1].Exposures.Should().BeNull();
        requests[1].Goals.Should().ContainSingle().Which.Name.Should().Be("purchase");
        context.PendingCount.Should().Be(0);
    }

    [Test]
    public void ExposureServedWithEveryUnitIsPublishedInOneRequest()
    {
        var experiment = NewSwitchback(2, Hour);
        experiment.SwitchbackClusters = Clusters(UnitType, "city", "user_id", "user_city");
        var context = CreateContext(new Dictionary<string, string> { [UnitType] = Uid, ["user_id"] = "u1" },
            DataOf(experiment));
        Clock.Now = Anchor + Hour + 1;
        context.SetAttribute("city", "Faro");
        context.SetAttribute("user_city", "Faro");
        context.GetTreatment("exp_sb");
        context.Track("purchase", null!);
        context.Publish();
        Published.Should().ContainSingle();
        Published[0].Event.Units.Should().HaveCount(2);
        Published[0].Event.Exposures.Should().ContainSingle();
        Published[0].Event.Goals.Should().ContainSingle();
    }

    [Test]
    public void SplitPublishRetriesOnlyTheUndeliveredRequest()
    {
        var experiment = NewSwitchback(2, Hour);
        experiment.SwitchbackClusters = Clusters(UnitType, "city", "user_id", "user_city");
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
        var context = CreateContext(DataOf(experiment));
        Clock.Now = Anchor + Hour + 1;
        context.SetAttribute("city", "Faro");
        context.GetTreatment("exp_sb");
        context.SetUnit("user_id", "u1");
        context.Track("purchase", null!);

        FluentActions.Invoking(() => context.Publish()).Should().Throw<InvalidOperationException>();
        sent.Should().ContainSingle().Which.Exposures.Should().ContainSingle();
        context.PendingCount.Should().Be(1);

        context.Publish();
        sent.Should().HaveCount(2);
        sent[1].Goals.Should().ContainSingle();
        sent[1].Exposures.Should().BeNull();
        context.PendingCount.Should().Be(0);
    }

    // Owner decision (php-sdk#13 Q2): mid-period cluster attribute changes are not supported.
    [Test]
    public void MidPeriodClusterChangeBeforeARequeueKeepsTheCachedArm()
    {
        var experiment = NewSwitchback(2, Hour);
        experiment.SwitchbackClusters = Clusters(UnitType, "city");
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
        var faro = SwitchbackAssigner.Assign("Faro", 1, Seed, 2);
        var porto = Enumerable.Range(2, 64).Select(i => (Period: (long)i, Arm: SwitchbackAssigner.Assign("Porto", i, Seed, 2)))
            .First(p => SwitchbackAssigner.Assign("Faro", p.Period, Seed, 2) != p.Arm);
        var context = CreateContext(DataOf(experiment));
        Clock.Now = Anchor + Hour + 1;
        context.SetAttribute("city", "Faro");
        context.GetTreatment("exp_sb").Should().Be(faro);
        FluentActions.Invoking(() => context.Publish()).Should().Throw<InvalidOperationException>();

        Clock.Now = Anchor + Hour + 2;
        context.SetAttribute("city", "Porto");
        context.GetTreatment("exp_sb").Should().Be(faro, "the period keeps the arm cached at its first read");
        context.PeekTreatment("exp_sb").Should().Be(faro);
        context.Publish();
        var exposure = sent.Should().ContainSingle().Which.Exposures.Should().ContainSingle().Subject;
        exposure.Should().Be(NewExposure(1, "exp_sb", faro, Anchor + Hour + 1, true, true));
        Replay(sent[0], "city", exposure.ExposedAt).Should().Be("Faro");

        Clock.Now = Anchor + porto.Period * Hour + 1;
        context.GetTreatment("exp_sb").Should().Be(porto.Arm, "the change takes effect in the next period");
    }

    // Mirrors the Collector's attribute replay.
    private static object? Replay(PublishEvent published, string name, long at)
    {
        ABSmartly.Models.Attribute? selected = null;
        foreach (var attribute in published.Attributes)
            if (attribute.Name == name && attribute.SetAt <= at && (selected == null || attribute.SetAt >= selected.SetAt))
                selected = attribute;
        return selected?.Value;
    }

    [Test]
    public void PublishedAtIsTheClockReadingWithoutSwitchbacks()
    {
        var context = CreateContext(DataOf(AsUnit(NewSwitchback(2, Hour))));
        Clock.Now = Anchor + 100;
        context.GetTreatment("exp_sb");
        context.Track("purchase", null!);
        Clock.Now = Anchor + 50;
        PublishEvent(context).PublishedAt.Should().Be(Anchor + 50);
    }

    // Unit experiments keep stamping at enqueue time: their lookup does not read the clock, so the
    // exposure carries the single (enqueue) reading.
    [Test]
    public void UnitExposureTimingIsUnchanged()
    {
        var unit = NewSwitchback(2, Hour);
        unit.RandomizationType = "unit";
        var context = CreateContext(DataOf(unit));
        Clock.Now = Anchor + Hour;
        context.PeekTreatment("exp_sb");
        Clock.NextRead = Anchor + Hour - 1;

        var variant = context.GetTreatment("exp_sb");
        PublishExposures(context).Should().Equal(NewExposure(1, "exp_sb", variant, Anchor + Hour - 1, true, true));
    }

    // A grid change that lowers the current index below the retained ones must not evict the entry
    // it just installed.
    [Test]
    public void GridRefreshToALowerIndexExposesOnce()
    {
        var context = CreateContext(DataOf(NewSwitchback(2, Hour)));
        Clock.Now = Anchor + 99 * Hour;
        context.GetTreatment("exp_sb");
        Clock.Now = Anchor + 100 * Hour;
        context.GetTreatment("exp_sb");
        context.PendingCount.Should().Be(2);

        var restarted = NewSwitchback(2, Hour);
        restarted.SwitchbackStartAt = Anchor + 100 * Hour;
        RefreshTo(context, restarted);
        for (var i = 0; i < 3; ++i) context.GetTreatment("exp_sb").Should().Be(Expected2Arms[0]);
        context.PendingCount.Should().Be(3);
    }

    [Test]
    public void PeriodLengthRefreshToALowerIndexExposesOnce()
    {
        var context = CreateContext(DataOf(NewSwitchback(2, Hour)));
        Clock.Now = Anchor + 100 * Hour;
        context.GetTreatment("exp_sb");
        Clock.Now = Anchor + 101 * Hour;
        context.GetTreatment("exp_sb");

        RefreshTo(context, NewSwitchback(2, 200 * Hour));
        context.GetTreatment("exp_sb");
        context.GetTreatment("exp_sb");
        context.PendingCount.Should().Be(3);
    }

    // Rewinding the clock across retained periods re-exposes each re-entered evicted period at most
    // once, never once per call.
    [Test]
    public void ClockRewindReexposesAnEvictedPeriodAtMostOnce()
    {
        var context = CreateContext(DataOf(NewSwitchback(2, Hour)));
        for (var i = 0; i < 3; ++i)
        {
            Clock.Now = Anchor + i * Hour;
            context.GetTreatment("exp_sb");
        }

        context.PendingCount.Should().Be(3);

        Clock.Now = Anchor;
        for (var i = 0; i < 3; ++i) context.GetTreatment("exp_sb").Should().Be(Expected2Arms[0]);
        context.PendingCount.Should().Be(4);

        Clock.Now = Anchor + 2 * Hour;
        context.GetTreatment("exp_sb");
        context.PendingCount.Should().Be(4);
        Clock.Now = Anchor + Hour;
        for (var i = 0; i < 3; ++i) context.GetTreatment("exp_sb").Should().Be(Expected2Arms[1]);
        context.PendingCount.Should().Be(5);

        var exposures = PublishExposures(context);
        exposures[3].Should().Be(NewExposure(1, "exp_sb", Expected2Arms[0], Anchor, true, true));
        exposures[4].Should().Be(NewExposure(1, "exp_sb", Expected2Arms[1], Anchor + Hour, true, true));
    }

    // An overridden switchback slot is re-resolved on a grid change like a normal one; the override
    // still wins and the holdout never fires.
    [Test]
    public void OverrideIsReresolvedWhenTheGridChanges()
    {
        var holdout = Holdout(new[] { 0.0, 1.0 });
        var original = NewSwitchback(3, Hour);
        original.HoldoutIds = new[] { 11 };
        var context = CreateContext(new ContextData(new[] { original }, new[] { holdout }));
        context.SetOverride("exp_sb", 2);

        Clock.Now = Anchor + 10;
        context.GetTreatment("exp_sb").Should().Be(2);
        context.GetVariableValue("sb.arm", -1).Should().Be(2);
        context.PendingCount.Should().Be(1);

        var regridded = NewSwitchback(3, 2 * Hour);
        regridded.HoldoutIds = new[] { 11 };
        regridded.Variants[2] = new ExperimentVariant { Name = "C", Config = "{\"sb.arm\":22}" };
        RefreshTo(context, new ContextData(new[] { regridded }, new[] { holdout }));

        context.GetTreatment("exp_sb").Should().Be(2);
        context.GetVariableValue("sb.arm", -1).Should().Be(22);
        context.PendingCount.Should().Be(2);

        var exposures = PublishExposures(context);
        exposures.Should().Equal(
            NewExposure(1, "exp_sb", 2, Anchor + 10, false, true, overridden: true),
            NewExposure(1, "exp_sb", 2, Anchor + 10, false, true, overridden: true));
    }

    // Each identity transition is a new epoch: returning to an identical grid re-resolves instead of
    // resurrecting the slot from before the transition.
    [Test]
    public void SwitchbackToUnitToIdenticalSwitchbackReresolves()
    {
        var context = CreateContext(DataOf(NewSwitchback(2, Hour)));
        Clock.Now = Anchor + 10;
        context.GetTreatment("exp_sb").Should().Be(Expected2Arms[0]);
        context.PendingCount.Should().Be(1);

        var toUnit = NewSwitchback(2, Hour);
        toUnit.RandomizationType = "unit";
        RefreshTo(context, toUnit);
        context.GetTreatment("exp_sb");
        context.PendingCount.Should().Be(2);

        RefreshTo(context, NewSwitchback(2, Hour));
        context.GetTreatment("exp_sb").Should().Be(Expected2Arms[0]);
        context.GetTreatment("exp_sb").Should().Be(Expected2Arms[0]);
        context.PendingCount.Should().Be(3);
    }

    [Test]
    public void RemovedAndRestoredSwitchbackReresolves()
    {
        var context = CreateContext(DataOf(NewSwitchback(2, Hour)));
        Clock.Now = Anchor + 10;
        context.GetTreatment("exp_sb");

        RefreshTo(context);
        context.GetTreatment("exp_sb").Should().Be(0);
        context.PendingCount.Should().Be(2);

        RefreshTo(context, NewSwitchback(2, Hour));
        context.GetTreatment("exp_sb").Should().Be(Expected2Arms[0]);
        context.PendingCount.Should().Be(3);
    }

    [Test]
    public void PreAnchorSlotIsDroppedWhenTheGridChanges()
    {
        var gridA = NewSwitchback(2, Hour);
        gridA.SwitchbackStartAt = Anchor + 10 * Hour;
        var context = CreateContext(DataOf(gridA));
        Clock.Now = Anchor + 10;
        context.GetTreatment("exp_sb").Should().Be(0);
        context.PendingCount.Should().Be(0);

        RefreshTo(context, NewSwitchback(2, Hour));
        context.GetTreatment("exp_sb").Should().Be(Expected2Arms[0]);
        context.PendingCount.Should().Be(1);

        RefreshTo(context, gridA);
        context.GetTreatment("exp_sb").Should().Be(0);
        context.PendingCount.Should().Be(1);
        context.GetVariableValue("sb.arm", -1).Should().Be(0);
        context.PendingCount.Should().Be(1);
    }

    // Epochs are reconciled lazily; an intermediate state that is never looked up must still end the
    // epoch.
    [Test]
    public void TransitionsWithoutAnInterveningLookupStillReresolve()
    {
        var context = CreateContext(DataOf(NewSwitchback(2, Hour)));
        Clock.Now = Anchor + 10;
        context.GetTreatment("exp_sb");
        context.PendingCount.Should().Be(1);

        var toUnit = NewSwitchback(2, Hour);
        toUnit.RandomizationType = "unit";
        RefreshTo(context, toUnit);
        RefreshTo(context, NewSwitchback(2, Hour));
        context.GetTreatment("exp_sb").Should().Be(Expected2Arms[0]);
        context.PendingCount.Should().Be(2);

        RefreshTo(context);
        RefreshTo(context, NewSwitchback(2, Hour));
        context.GetTreatment("exp_sb").Should().Be(Expected2Arms[0]);
        context.PendingCount.Should().Be(3);

        var regridded = NewSwitchback(2, Hour);
        regridded.SwitchbackDesignType = "other";
        RefreshTo(context, regridded);
        RefreshTo(context, NewSwitchback(2, Hour));
        context.GetTreatment("exp_sb").Should().Be(Expected2Arms[0]);
        context.GetTreatment("exp_sb").Should().Be(Expected2Arms[0]);
        context.PendingCount.Should().Be(4);

        RefreshTo(context, NewSwitchback(2, Hour));
        context.GetTreatment("exp_sb");
        context.PendingCount.Should().Be(4);
    }

    // Each identity field, changed and changed back between two lookups, still starts a new epoch:
    // the restored definition must not resurrect the exposed slot of the first one.
    [TestCase("allowList")]
    [TestCase("startAt")]
    [TestCase("periodMs")]
    [TestCase("iteration")]
    [TestCase("clusters")]
    [TestCase("designType")]
    public void IdentityChangeAndBackWithoutALookupReresolves(string field)
    {
        var context = CreateContext(DataOf(NewSwitchback(2, Hour)));
        Clock.Now = Anchor + 10;
        context.GetTreatment("exp_sb");
        context.PendingCount.Should().Be(1);

        var changed = NewSwitchback(2, Hour);
        switch (field)
        {
            case "allowList": changed.SwitchbackClusterValues = new[] { ClusterKey }; break;
            case "startAt": changed.SwitchbackStartAt = Anchor - Hour; break;
            case "periodMs": changed.SwitchbackPeriodMs = 2 * Hour; break;
            case "iteration": changed.Iteration = 2; break;
            case "clusters": changed.SwitchbackClusters = Clusters(UnitType, "'store_key'"); break;
            case "designType": changed.SwitchbackDesignType = "other"; break;
        }

        RefreshTo(context, changed);
        RefreshTo(context, NewSwitchback(2, Hour));
        context.GetTreatment("exp_sb").Should().Be(Expected2Arms[0]);
        context.PendingCount.Should().Be(2, field);
    }

    // A cached unit assignment is never reused once a refresh makes the experiment a switchback:
    // before the anchor that switchback is a silent control.
    [Test]
    public void UnitExperimentBecomingAPreAnchorSwitchbackIsASilentControl()
    {
        Context context = null!;
        foreach (var uid in new[] { Uid, "u1", "u2", "u3", "u4", "u5", "u6", "u7" })
        {
            context = CreateContext(new Dictionary<string, string> { [UnitType] = uid }, DataOf(AsUnit(NewSwitchback(3, Hour))));
            if (context.PeekTreatment("exp_sb") != 0) break;
        }

        Clock.Now = Anchor + 10;
        context.GetTreatment("exp_sb").Should().NotBe(0);
        var pending = context.PendingCount;
        var preAnchor = NewSwitchback(3, Hour);
        preAnchor.SwitchbackStartAt = Anchor + Day;
        RefreshTo(context, preAnchor);
        context.GetTreatment("exp_sb").Should().Be(0);
        context.GetVariableValue("sb.arm", -1).Should().Be(0);
        context.PendingCount.Should().Be(pending);
    }

    [Test]
    public void IterationChangeReexposesInTheSamePeriod()
    {
        var context = CreateContext(DataOf(NewSwitchback(2, Hour)));
        Clock.Now = Anchor + 10;
        context.GetTreatment("exp_sb");
        var restarted = NewSwitchback(2, Hour);
        restarted.Iteration = 2;
        RefreshTo(context, restarted);
        context.GetTreatment("exp_sb").Should().Be(Expected2Arms[0]);
        context.PendingCount.Should().Be(2);
    }

    private const string AssignVariant1ToUs = "{\"rules\":[{\"type\":\"assign\",\"environments\":[]," +
                                              "\"conditions\":{\"and\":[{\"eq\":[{\"var\":\"probe/country\"},{\"value\":\"US\"}]}]}," +
                                              "\"variant\":1}]}";

    // An attribute value that counts how often a rule condition reads it.
    private sealed class CountingAttribute : Dictionary<string, object>, IDictionary<string, object>
    {
        public int Lookups;

        bool IDictionary<string, object>.TryGetValue(string key, out object value)
        {
            Interlocked.Increment(ref Lookups);
            return TryGetValue(key, out value!);
        }
    }

    // A legacy (missing or unreadable cluster map) switchback serves under its declared unitType, so
    // a unitType change on refresh is identity for it, unlike for a readable map.
    [Test]
    public void LegacySwitchbackUnitTypeChangeIsIdentity()
    {
        var legacy = NewSwitchback(2, Hour);
        legacy.SwitchbackClusters = null;
        var context = CreateContext(new Dictionary<string, string> { [UnitType] = Uid, ["user_id"] = "u1" }, DataOf(legacy));
        Clock.Now = Anchor + 10;
        context.GetTreatment("exp_sb").Should().Be(0);
        context.GetVariableValue("sb.arm", -1).Should().Be(0);
        context.CachedAssignmentForTest("exp_sb")!.UnitType.Should().Be(UnitType);

        var other = NewSwitchback(2, Hour);
        other.SwitchbackClusters = null;
        other.UnitType = "other";
        RefreshTo(context, other);
        context.GetTreatment("exp_sb").Should().Be(0);
        context.GetVariableValue("sb.arm", -1).Should().Be(-1);

        var user = NewSwitchback(2, Hour);
        user.SwitchbackClusters = null;
        user.UnitType = "user_id";
        RefreshTo(context, user);
        context.GetTreatment("exp_sb").Should().Be(0);
        context.GetVariableValue("sb.arm", -1).Should().Be(0);
        var resolution = context.CachedAssignmentForTest("exp_sb")!;
        resolution.UnitType.Should().Be("user_id");
        resolution.Eligible.Should().BeFalse();
        resolution.Switchback.Silent.Should().BeTrue();
        context.PendingCount.Should().Be(0);
    }

    // An ineligible or absent switchback slot never consulted its rules, so neither a set attribute
    // nor a rules refresh re-evaluates them or changes its result.
    [Test]
    public void RulesAreNotReevaluatedForAnIneligibleOrAbsentSlot()
    {
        var ineligible = NewSwitchback(1, "exp_ineligible", 2, Hour);
        ineligible.AssignmentRules = AssignVariant1ToUs;
        ineligible.SwitchbackClusterValues = new[] { "other-store" };
        var absent = NewSwitchback(2, "exp_absent", 2, Hour);
        absent.AssignmentRules = AssignVariant1ToUs;
        absent.SwitchbackClusters = Clusters("device_id", ClusterPath);
        var context = CreateContext(DataOf(ineligible, absent));
        var probe = new CountingAttribute { ["country"] = "US" };
        context.SetAttribute("probe", probe);
        Clock.Now = Anchor + 10;
        context.GetTreatment("exp_ineligible").Should().Be(0);
        context.GetTreatment("exp_absent").Should().Be(0);
        probe.Lookups.Should().Be(0);

        context.SetAttribute("x", 1);
        var ineligibleRefreshed = NewSwitchback(1, "exp_ineligible", 2, Hour);
        ineligibleRefreshed.AssignmentRules = AssignVariant1ToUs.Replace("\"variant\":1", "\"variant\":1,\"name\":\"changed\"");
        ineligibleRefreshed.SwitchbackClusterValues = ineligible.SwitchbackClusterValues;
        var absentRefreshed = NewSwitchback(2, "exp_absent", 2, Hour);
        absentRefreshed.AssignmentRules = ineligibleRefreshed.AssignmentRules;
        absentRefreshed.SwitchbackClusters = absent.SwitchbackClusters;
        RefreshTo(context, ineligibleRefreshed, absentRefreshed);

        context.GetTreatment("exp_ineligible").Should().Be(0);
        context.GetTreatment("exp_absent").Should().Be(0);
        probe.Lookups.Should().Be(0);
        context.PendingCount.Should().Be(0);
    }

    // An audience deserializer that counts how often an audience (or rules string) is parsed, which
    // AudienceMatcher does on every evaluation.
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

    // A switchback slot without an eligible cluster never evaluated its audience, so a later
    // SetAttribute does not make every read evaluate it (the result could not change anyway).
    [Test]
    public void AudienceIsNotReevaluatedForAnIneligibleOrAbsentSlot()
    {
        var ineligible = NewSwitchback(1, "exp_ineligible", 2, Hour);
        ineligible.Audience = StrictGoldAudience;
        ineligible.SwitchbackClusterValues = new[] { "other-store" };
        var absent = NewSwitchback(2, "exp_absent", 2, Hour);
        absent.Audience = StrictGoldAudience;
        absent.SwitchbackClusters = Clusters("device_id", ClusterPath);
        var counter = new CountingAudienceDeserializer();
        var context = CreateContext(new Dictionary<string, string> { [UnitType] = Uid }, DataOf(ineligible, absent),
            new AudienceMatcher(counter));
        Clock.Now = Anchor + 10;
        context.GetTreatment("exp_ineligible").Should().Be(0);
        context.GetTreatment("exp_absent").Should().Be(0);

        context.SetAttribute("tier", "gold");
        for (var i = 0; i < 3; ++i)
        {
            context.GetTreatment("exp_ineligible").Should().Be(0);
            context.GetTreatment("exp_absent").Should().Be(0);
        }

        counter.Calls.Should().Be(0);
        context.PendingCount.Should().Be(0);
    }

    // An eligible slot re-evaluates its rules after a SetAttribute, once.
    [Test]
    public void StaleRuleTransitionReevaluatesOnce()
    {
        var experiment = NewSwitchback(2, Hour);
        experiment.AssignmentRules = AssignVariant1ToUs;
        var context = CreateContext(DataOf(experiment));
        var probe = new CountingAttribute { ["country"] = "GB" };
        context.SetAttribute("probe", probe);
        Clock.Now = Anchor + Hour + 10;
        context.PeekTreatment("exp_sb").Should().Be(Expected2Arms[1]);

        probe["country"] = "US";
        context.SetAttribute("probe", probe);
        context.PeekTreatment("exp_sb").Should().Be(1);
        probe.Lookups = 0;
        context.PeekTreatment("exp_sb").Should().Be(1);
        context.GetTreatment("exp_sb").Should().Be(1);
        probe.Lookups.Should().Be(0);
        PublishExposures(context).Should().Equal(RuleExposure("exp_sb", 1, Anchor + Hour + 10));
    }

    [Test]
    public void InPeriodRuleRefreshKeepsTheVariantAndExposesOnce()
    {
        var experiment = NewSwitchback(2, Hour);
        experiment.AssignmentRules = AssignVariant1;
        var context = CreateContext(DataOf(experiment));
        Clock.Now = Anchor + 5;
        context.GetTreatment("exp_sb").Should().Be(1);

        context.SetAttribute("tier", "x");
        var refreshed = NewSwitchback(2, Hour);
        refreshed.AssignmentRules = "{\"rules\":[{\"type\":\"assign\",\"environments\":[],\"variant\":1," +
                                    "\"conditions\":{\"and\":[{\"eq\":[{\"var\":\"tier\"},{\"value\":\"x\"}]}]}}]}";
        RefreshTo(context, refreshed);
        Clock.Now = Anchor + 6;
        context.GetTreatment("exp_sb").Should().Be(1);
        PublishExposures(context).Should().Equal(RuleExposure("exp_sb", 1, Anchor + 5));
    }

    // SDK choice: before the anchor nothing is recorded, a traffic miss included, because the
    // pre-period check precedes traffic. From the anchor the same miss is recorded.
    [Test]
    public void PreAnchorTrafficMissIsSilent()
    {
        var experiment = NewSwitchback(2, Hour);
        experiment.TrafficSplit = new[] { 1.0, 0.0 };
        Clock.Now = Anchor - 1;
        var context = CreateContext(DataOf(experiment));
        context.GetTreatment("exp_sb").Should().Be(0);
        context.PendingCount.Should().Be(0);

        Clock.Now = Anchor;
        context.GetTreatment("exp_sb").Should().Be(0);
        PublishExposures(context).Should().Equal(NewExposure(1, "exp_sb", 0, Anchor, true, false));
    }

    // An override on a switchback with no serving unit is exposed under the declared unitType when
    // the cluster map is missing or unreadable, never under null or a cluster map type.
    [Test]
    public void OverrideWithoutAServingUnitOnALegacySwitchbackUsesTheDeclaredUnitType()
    {
        var missing = NewSwitchback(1, "exp_missing_map", 2, Hour);
        missing.UnitType = "user_id";
        missing.SwitchbackClusters = null;
        var unreadable = NewSwitchback(2, "exp_unreadable_map", 2, Hour);
        unreadable.UnitType = "user_id";
        unreadable.SwitchbackClusters = Clusters(UnitType, "'unterminated");
        var noUnit = NewSwitchback(3, "exp_no_linked_unit", 2, Hour);
        noUnit.SwitchbackClusters = Clusters("device_id", ClusterPath, "user_id", ClusterPath);
        var context = CreateContext(DataOf(missing, unreadable, noUnit));
        context.SetOverride("exp_missing_map", 1);
        context.SetOverride("exp_unreadable_map", 1);
        context.SetOverride("exp_no_linked_unit", 1);
        Clock.Now = Anchor + 5;
        context.GetTreatment("exp_missing_map").Should().Be(1);
        context.GetTreatment("exp_unreadable_map").Should().Be(1);
        context.GetTreatment("exp_no_linked_unit").Should().Be(1);

        PublishExposures(context).Should().Equal(
            NewExposure(1, "exp_missing_map", 1, Anchor + 5, false, true, overridden: true, unit: "user_id"),
            NewExposure(2, "exp_unreadable_map", 1, Anchor + 5, false, true, overridden: true, unit: "user_id"),
            NewExposure(3, "exp_no_linked_unit", 1, Anchor + 5, false, true, overridden: true, unit: "device_id"));
    }

    // Exact wire form of a rule-won and a traffic-miss switchback exposure.
    [Test]
    public void RuleWonAndTrafficMissExposuresSerializeExactly()
    {
        var rule = NewSwitchback(1, "exp_rule", 2, Hour);
        rule.AssignmentRules = AssignVariant1;
        var miss = NewSwitchback(2, "exp_miss", 2, Hour);
        miss.TrafficSplit = new[] { 1.0, 0.0 };
        var context = CreateContext(DataOf(rule, miss));
        Clock.Now = Anchor + 5;
        context.GetTreatment("exp_rule");
        context.GetTreatment("exp_miss");
        Clock.Now = Anchor + 9;

        var json = Encoding.UTF8.GetString(new DefaultContextEventSerializer().Serialize(PublishEvent(context)));
        json.Should().Be("{\"hashed\":true,\"units\":[{\"type\":\"store_id\",\"uid\":\"" + UidHash + "\"}]," +
                         "\"publishedAt\":" + (Anchor + 9) + "," +
                         "\"exposures\":[" +
                         "{\"id\":1,\"name\":\"exp_rule\",\"unit\":\"store_id\",\"variant\":1,\"exposedAt\":" + (Anchor + 5) +
                         ",\"assigned\":false,\"eligible\":true,\"overridden\":false,\"fullOn\":false,\"custom\":false," +
                         "\"audienceMismatch\":false,\"ruleOverride\":true}," +
                         "{\"id\":2,\"name\":\"exp_miss\",\"unit\":\"store_id\",\"variant\":0,\"exposedAt\":" + (Anchor + 5) +
                         ",\"assigned\":true,\"eligible\":false,\"overridden\":false,\"fullOn\":false,\"custom\":false," +
                         "\"audienceMismatch\":false,\"ruleOverride\":false}" +
                         "]," +
                         "\"attributes\":[{\"name\":\"" + ClusterPath + "\",\"value\":\"" + ClusterKey + "\",\"setAt\":" + Anchor + "}]}");
    }

    // Two callers resolving the same period at once, after an audience change made the cached slot
    // stale, expose it once. With the cluster attribute rewritten meanwhile, the rebuilt slot keeps
    // the key read at first resolution (Lisboa: variant 1 in period 1; Faro is 0 there).
    [TestCase(null, 1)]
    [TestCase("Faro", 1)]
    public void ConcurrentResolutionOfTheSamePeriodExposesOnce(string? concurrentCity, int expectedVariant)
    {
        SwitchbackAssigner.Assign("Lisboa", 1, Seed, 2).Should().Be(1);
        SwitchbackAssigner.Assign("Faro", 1, Seed, 2).Should().Be(0);
        var experiment = NewSwitchback(2, Hour);
        experiment.Audience = "{\"filter\":[{\"eq\":[{\"var\":\"country\"},{\"value\":\"PT\"}]}]}";
        experiment.SwitchbackClusters = Clusters(UnitType, "city");
        var context = CreateContext(DataOf(experiment));
        Clock.Now = Anchor + Hour + 10;
        context.SetAttribute("city", "Lisboa");

        context.PeekTreatment("exp_sb").Should().Be(expectedVariant);
        context.SetAttribute("country", "PT");
        if (concurrentCity != null) context.SetAttribute("city", concurrentCity);

        using var start = new Barrier(3);
        var variants = new int[2];
        var readers = Enumerable.Range(0, 2).Select(i => Task.Run(() =>
        {
            start.SignalAndWait();
            variants[i] = context.GetTreatment("exp_sb");
        })).ToArray();
        var writer = Task.Run(() =>
        {
            start.SignalAndWait();
            for (var i = 0; i < 2000 && concurrentCity != null; ++i) context.SetAttribute("city", concurrentCity);
        });
        Task.WaitAll(readers.Append(writer).ToArray(), TimeSpan.FromSeconds(10)).Should().BeTrue();

        variants.Should().Equal(expectedVariant, expectedVariant);
        context.PendingCount.Should().Be(1);
        PublishExposures(context).Should().Equal(
            NewExposure(1, "exp_sb", expectedVariant, Anchor + Hour + 10, true, true));
    }

    // Many threads crossing period boundaries together: one exposure per period, each stamped inside
    // the period whose variant it carries.
    [Test]
    public void ConcurrentCallersAcrossPeriodsExposeOncePerPeriod()
    {
        var context = CreateContext(DataOf(NewSwitchback(2, Hour)));
        for (var period = 0; period < 6; ++period)
        {
            Clock.Now = Anchor + period * Hour + 1;
            Parallel.For(0, 16, _ => context.GetTreatment("exp_sb").Should().Be(Expected2Arms[(int)((Clock.Now - Anchor) / Hour)]));
        }

        var exposures = PublishExposures(context);
        exposures.Select(e => ((e.ExposedAt - Anchor) / Hour, e.Variant)).Should()
            .Equal(Enumerable.Range(0, 6).Select(i => ((long)i, Expected2Arms[i])));
    }
}
