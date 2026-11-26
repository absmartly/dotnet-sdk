using System.Text;
using ABSmartly.Internal;
using ABSmartly.Internal.Hashing;
using ABSmartly.Models;
using ABSmartly.Services;

namespace ABSmartly.Sdk.Tests.Switchback;

// Ported case by case from java-sdk ContextSwitchbackTest (java-sdk#15, SWITCHBACK.md): the period
// grid, its boundaries and anchor, per-period caching and exposure, precedence and exposure flags,
// and holdouts never applying to a switchback.
[TestFixture]
public class ContextSwitchbackTests : SwitchbackTestBase
{
    // The cluster identity is the raw attribute value, neither hashed nor the unit uid.
    [Test]
    public void ClusterIdentityIsTheRawClusterValue()
    {
        Md5.Hash(Uid).Should().Be(UidHash);

        var context = CreateContext(DataOf(NewSwitchback(3, Hour)));
        var served = PeekSchedule(context, "exp_sb", Hour, 12);

        served.Should().Equal(Expected3Arms);
        served.Should().NotEqual(UidHash3Arms);
        served.Should().NotEqual(Uid3Arms);
        var hashedKey = Md5.Hash(ClusterKey);
        var hashedDiffers = false;
        for (var i = 0; i < 12; ++i)
        {
            served[i].Should().Be(SwitchbackAssigner.Assign(ClusterKey, i, Seed, 3));
            hashedDiffers |= SwitchbackAssigner.Assign(hashedKey, i, Seed, 3) != served[i];
        }

        hashedDiffers.Should().BeTrue();
    }

    [Test]
    public void ServesTwoThreeAndTenArmPermutations()
    {
        var context = CreateContext(DataOf(NewSwitchback(1, "exp_2", 2, Hour), NewSwitchback(2, "exp_3", 3, Hour),
            NewSwitchback(3, "exp_10", 10, Hour)));

        PeekSchedule(context, "exp_2", Hour, 12).Should().Equal(Expected2Arms);
        PeekSchedule(context, "exp_3", Hour, 12).Should().Equal(Expected3Arms);
        PeekSchedule(context, "exp_10", Hour, 12).Should().Equal(Expected10Arms);
    }

    [Test]
    public void OneHourBoundaryAtExactEdgeMilliseconds()
    {
        var context = CreateContext(DataOf(NewSwitchback(2, Hour)));

        Clock.Now = Anchor - 1;
        context.PeekTreatment("exp_sb").Should().Be(0);
        Clock.Now = Anchor;
        context.PeekTreatment("exp_sb").Should().Be(Expected2Arms[0]);
        Clock.Now = Anchor + Hour - 1;
        context.PeekTreatment("exp_sb").Should().Be(Expected2Arms[0]);
        Clock.Now = Anchor + Hour;
        context.PeekTreatment("exp_sb").Should().Be(Expected2Arms[1]);
        Clock.Now = Anchor + 3 * Hour - 1;
        context.PeekTreatment("exp_sb").Should().Be(Expected2Arms[2]);
        Clock.Now = Anchor + 3 * Hour;
        context.PeekTreatment("exp_sb").Should().Be(Expected2Arms[3]);
    }

    [Test]
    public void TwentyFourHourBoundaryAtExactEdgeMilliseconds()
    {
        var context = CreateContext(DataOf(NewSwitchback(2, Day)));

        Clock.Now = Anchor - 1;
        context.PeekTreatment("exp_sb").Should().Be(0);
        Clock.Now = Anchor;
        context.PeekTreatment("exp_sb").Should().Be(Expected2Arms[0]);
        Clock.Now = Anchor + Hour;
        context.PeekTreatment("exp_sb").Should().Be(Expected2Arms[0]);
        Clock.Now = Anchor + Day - 1;
        context.PeekTreatment("exp_sb").Should().Be(Expected2Arms[0]);
        Clock.Now = Anchor + Day;
        context.PeekTreatment("exp_sb").Should().Be(Expected2Arms[1]);
        Clock.Now = Anchor + 4 * Day - 1;
        context.PeekTreatment("exp_sb").Should().Be(Expected2Arms[3]);
        Clock.Now = Anchor + 4 * Day;
        context.PeekTreatment("exp_sb").Should().Be(Expected2Arms[4]);
    }

    // The Collector serves switchbackStartAt as the anchor without rounding it; before it the
    // experiment is an ineligible control that records no exposure. An anchor that is not hour
    // aligned is unservable (Registry), never served as is and never rounded.
    [Test]
    public void UnalignedStartIsUnservableAndPreAnchorIsIneligible()
    {
        var aligned = NewSwitchback(1, "exp_aligned", 2, Hour);
        aligned.SwitchbackStartAt = Anchor + Hour;
        var unaligned = NewSwitchback(2, "exp_unaligned", 2, Hour);
        const long start = Anchor + 1_234_567L;
        unaligned.SwitchbackStartAt = start;
        var context = CreateContext(DataOf(aligned, unaligned));

        foreach (var now in new[] { Anchor, start - 1, start, Anchor + Hour - 1 })
        {
            Clock.Now = now;
            context.GetTreatment("exp_aligned").Should().Be(0);
            context.GetTreatment("exp_unaligned").Should().Be(0);
        }

        context.PendingCount.Should().Be(0);
        for (var i = 1; i < 4; ++i)
        {
            Clock.Now = Anchor + i * Hour;
            context.GetTreatment("exp_aligned").Should().Be(Expected2Arms[i - 1]);
            context.GetTreatment("exp_unaligned").Should().Be(0);
            Clock.Now = start + i * Hour;
            context.GetTreatment("exp_unaligned").Should().Be(0);
        }

        PublishExposures(context).Should().Equal(
            NewExposure(1, "exp_aligned", Expected2Arms[0], Anchor + Hour, true, true),
            NewExposure(1, "exp_aligned", Expected2Arms[1], Anchor + 2 * Hour, true, true),
            NewExposure(1, "exp_aligned", Expected2Arms[2], Anchor + 3 * Hour, true, true));
    }

    // Collector anchorNotOnAnHourBoundaryIsUnservableOnGetAndPost, plus the Registry's other anchor
    // bounds: startAt < 0 or >= 10000-01-01T00:00:00Z.
    [Test]
    public void AnchorNotOnAnHourBoundaryIsUnservableOnGetAndPost()
    {
        const long maxAnchor = 253_402_300_800_000L;
        long[] unservable =
        {
            Anchor + 1, Anchor - 1, Anchor + 30 * 60_000L, Anchor + Hour - 1, -Hour, -1, maxAnchor, maxAnchor + Hour,
            long.MaxValue - long.MaxValue % Hour, long.MinValue
        };
        foreach (var startAt in unservable)
        {
            var experiment = NewSwitchback(2, Hour);
            experiment.SwitchbackStartAt = startAt;
            Clock.Now = 0;
            var context = CreateContext(DataOf(experiment));
            foreach (var now in new[] { 0, Anchor, Anchor + 2 * Hour + 1, startAt, startAt + Hour, maxAnchor + Day })
            {
                Clock.Now = now;
                context.GetTreatment("exp_sb").Should().Be(0, "startAt {0} now {1}", startAt, now);
                context.GetVariableValue("sb.arm", -1).Should().Be(0);
            }

            context.PendingCount.Should().Be(0, "startAt {0}", startAt);
            PublishExposures(context).Should().BeEmpty();
        }

        Published.Should().BeEmpty();

        // In-range hour-aligned anchors, including 0 and the last hour before the bound, serve.
        foreach (var startAt in new[] { 0, Anchor, maxAnchor - Hour })
        {
            var experiment = NewSwitchback(2, Hour);
            experiment.SwitchbackStartAt = startAt;
            Clock.Now = startAt + Hour;
            var context = CreateContext(DataOf(experiment));
            context.GetTreatment("exp_sb").Should().Be(Expected2Arms[1], "startAt {0}", startAt);
            context.PendingCount.Should().Be(1);
        }
    }

    [Test]
    public void NullStartAtIsIneligibleAndNeverFallsBackToUnitAssignment()
    {
        var experiment = NewSwitchback(2, Hour);
        experiment.SwitchbackStartAt = null;
        var context = CreateContext(DataOf(experiment));

        for (var i = 0; i < 4; ++i)
        {
            Clock.Now = Anchor + i * Hour;
            context.GetTreatment("exp_sb").Should().Be(0);
            context.GetVariableValue("sb.arm", -1).Should().Be(0);
        }

        context.PendingCount.Should().Be(0);
    }

    // The period length must be positive (a null, zero or negative one is unservable), and a split
    // needs two arms; the grid then never draws, so nothing is recorded.
    [TestCase(null)]
    [TestCase(0L)]
    [TestCase(-Hour)]
    [TestCase(long.MinValue)]
    public void NonPositivePeriodIsUnservable(long? periodMs)
    {
        var experiment = NewSwitchback(2, Hour);
        experiment.SwitchbackPeriodMs = periodMs;
        var oneArm = NewSwitchback(2, "exp_one_arm", 2, Hour);
        oneArm.Split = new[] { 1.0 };
        var context = CreateContext(DataOf(experiment, oneArm));
        for (var i = 0; i < 3; ++i)
        {
            Clock.Now = Anchor + i * Hour + 7;
            context.GetTreatment("exp_sb").Should().Be(0);
            context.GetTreatment("exp_one_arm").Should().Be(0);
        }

        context.PendingCount.Should().Be(0);
    }

    // The period index is floorDiv(now - startAt, periodMs) in 64 bits: far from the anchor, with a
    // 1 ms period, the index exceeds the 32-bit range and the draw still matches the assigner.
    [Test]
    public void PeriodIndexIsSixtyFourBit()
    {
        var experiment = NewSwitchback(3, 1);
        experiment.SwitchbackStartAt = 0;
        var context = CreateContext(DataOf(experiment));
        foreach (var now in new[] { Anchor, Anchor + 1, Anchor + uint.MaxValue, 253_402_300_799_999L })
        {
            Clock.Now = now;
            context.PeekTreatment("exp_sb").Should().Be(SwitchbackAssigner.Assign(ClusterKey, now, Seed, 3), "now {0}", now);
        }

        // Seeds with the sign bit set in either half combine without sign extension of seedLo.
        foreach (var (hi, lo) in new[] { (-1, -1), (int.MinValue, 0), (0, int.MinValue), (int.MaxValue, -1) })
        {
            var seeded = NewSwitchback(3, Hour);
            seeded.SeedHi = hi;
            seeded.SeedLo = lo;
            Clock.Now = Anchor + 5 * Hour;
            var expected = SwitchbackAssigner.Assign(ClusterKey, 5, ((long)hi << 32) | (uint)lo, 3);
            CreateContext(DataOf(seeded)).PeekTreatment("exp_sb").Should().Be(expected, "seed {0}/{1}", hi, lo);
        }
    }

    [Test]
    public void SamePeriodIsCachedAndNextPeriodRollsOverWithOneExposureEach()
    {
        var context = CreateContext(DataOf(NewSwitchback(2, Hour)));

        Clock.Now = Anchor + 10;
        context.GetTreatment("exp_sb").Should().Be(Expected2Arms[0]);
        context.PendingCount.Should().Be(1);
        Clock.Now = Anchor + Hour - 1;
        context.GetTreatment("exp_sb").Should().Be(Expected2Arms[0]);
        context.PeekTreatment("exp_sb").Should().Be(Expected2Arms[0]);
        context.PendingCount.Should().Be(1);

        Clock.Now = Anchor + Hour;
        context.PeekTreatment("exp_sb").Should().Be(Expected2Arms[1]);
        context.PendingCount.Should().Be(1);
        context.GetTreatment("exp_sb").Should().Be(Expected2Arms[1]);
        context.PendingCount.Should().Be(2);
        Clock.Now = Anchor + Hour + 30 * 60_000L;
        context.GetTreatment("exp_sb").Should().Be(Expected2Arms[1]);
        context.PendingCount.Should().Be(2);

        PublishExposures(context).Should().Equal(
            NewExposure(1, "exp_sb", Expected2Arms[0], Anchor + 10, true, true),
            NewExposure(1, "exp_sb", Expected2Arms[1], Anchor + Hour, true, true));
    }

    // A cached period is served from the read path: repeated reads in the period, and in the next
    // one once it is built, rebuild nothing.
    // A switchback period is cached apart from every experiment name: an unknown experiment whose
    // name looks like a period slot of an overridden switchback gets no variant from that slot.
    [Test]
    public void APeriodSlotNeverAnswersAnotherExperimentName()
    {
        var context = CreateContext(DataOf(NewSwitchback(2, Hour)));
        context.SetOverride("exp_sb", 1);
        Clock.Now = Anchor + 10;
        context.GetTreatment("exp_sb").Should().Be(1);

        foreach (var alias in new[] { "exp_sb\u001f0", "exp_sb\u001f1", "exp_sb\u0000", "exp_sb0" })
            context.PeekTreatment(alias).Should().Be(0, "'{0}' is not the switchback", alias);
    }

    [Test]
    public void RepeatedReadsInAPeriodAreServedFromTheCache()
    {
        var context = CreateContext(DataOf(NewSwitchback(2, Hour)));
        var rebuilds = 0;
        Clock.Now = Anchor + 10;
        context.GetTreatment("exp_sb");
        Clock.Now = Anchor + Hour + 10;
        context.GetTreatment("exp_sb");

        foreach (var now in new[] { Anchor + 20, Anchor + Hour - 1, Anchor + Hour + 20, Anchor + 2 * Hour - 1 })
        {
            Clock.Now = now;
            context.BeforeAssignmentRebuild = () => ++rebuilds;
            context.GetTreatment("exp_sb");
            context.PeekTreatment("exp_sb");
        }

        rebuilds.Should().Be(0);
    }

    [Test]
    public void VariableLookupsFollowThePeriodAndExposeOncePerPeriod()
    {
        var context = CreateContext(DataOf(NewSwitchback(3, Hour)));

        context.VariableKeys.Should().BeEquivalentTo(new Dictionary<string, List<string>> { ["sb.arm"] = new() { "exp_sb" } });
        context.Experiments.Should().Equal("exp_sb");

        for (var i = 0; i < 3; ++i)
        {
            Clock.Now = Anchor + i * Hour;
            context.PeekVariableValue("sb.arm", -1).Should().Be(Expected3Arms[i]);
            context.GetVariableValue("sb.arm", -1).Should().Be(Expected3Arms[i]);
            context.GetVariableValue("sb.arm", -1).Should().Be(Expected3Arms[i]);
            context.GetTreatment("exp_sb").Should().Be(Expected3Arms[i]);
        }

        context.PendingCount.Should().Be(3);
    }

    // No unit of a linked type: the experiment does not apply and records nothing. unitType is
    // never a fallback.
    [Test]
    public void MissingClusterUnitIsUnassignedWithoutFallback()
    {
        var experiment = NewSwitchback(2, Hour);
        experiment.UnitType = "user_id";
        var context = CreateContext(new Dictionary<string, string> { ["user_id"] = Uid }, DataOf(experiment));

        for (var i = 0; i < 3; ++i)
        {
            Clock.Now = Anchor + i * Hour;
            context.PeekTreatment("exp_sb").Should().Be(0);
            context.GetTreatment("exp_sb").Should().Be(0);
            context.GetVariableValue("sb.arm", -1).Should().Be(-1);
        }

        context.PendingCount.Should().Be(0);

        context.SetUnit(UnitType, Uid);
        context.GetTreatment("exp_sb").Should().Be(Expected2Arms[2]);
        PublishExposures(context).Should().Equal(NewExposure(1, "exp_sb", Expected2Arms[2], Anchor + 2 * Hour, true, true));
    }

    [Test]
    public void FullOnServesTheFullOnVariantInEveryPeriod()
    {
        var experiment = NewSwitchback(3, Hour);
        experiment.FullOnVariant = 2;
        var context = CreateContext(DataOf(experiment));

        for (var i = 0; i < 4; ++i)
        {
            Clock.Now = Anchor + i * Hour;
            context.GetTreatment("exp_sb").Should().Be(2);
        }

        var exposures = PublishExposures(context);
        exposures.Should().HaveCount(4);
        exposures.Should().OnlyContain(e => e.Variant == 2 && e.FullOn);
    }

    [Test]
    public void OverrideAndCustomAssignmentWinInEveryPeriod()
    {
        var context = CreateContext(DataOf(NewSwitchback(1, "exp_override", 3, Hour), NewSwitchback(2, "exp_custom", 3, Hour)));
        context.SetOverride("exp_override", 7);
        context.SetCustomAssignment("exp_custom", 2);

        for (var i = 0; i < 3; ++i)
        {
            Clock.Now = Anchor + i * Hour;
            context.GetTreatment("exp_override").Should().Be(7);
            context.GetTreatment("exp_custom").Should().Be(2);
        }

        var exposures = PublishExposures(context);
        exposures.Should().HaveCount(6);
        foreach (var exposure in exposures)
        {
            if (exposure.Name == "exp_override")
                (exposure.Overridden, exposure.Variant).Should().Be((true, 7));
            else
                (exposure.Custom, exposure.Variant).Should().Be((true, 2));
        }
    }

    [Test]
    public void CustomAssignmentChangeWithinAPeriodReresolves()
    {
        var context = CreateContext(DataOf(NewSwitchback(3, Hour)));
        Clock.Now = Anchor + 2 * Hour;
        context.GetTreatment("exp_sb").Should().Be(Expected3Arms[2]);
        context.SetCustomAssignment("exp_sb", 2);
        context.GetTreatment("exp_sb").Should().Be(2);
        context.SetCustomAssignment("exp_sb", 2);
        context.GetTreatment("exp_sb").Should().Be(2);
        context.PendingCount.Should().Be(2);
    }

    // A switchback whose variant differs from its custom assignment (a traffic miss, full-on) stays
    // current while the custom assignment is unchanged: repeated reads do not re-resolve or
    // re-expose it.
    [Test]
    public void UnchangedCustomAssignmentKeepsTheSlot()
    {
        var miss = NewSwitchback(1, "exp_miss", 3, Hour);
        miss.TrafficSplit = new[] { 1.0, 0.0 };
        var fullOn = NewSwitchback(2, "exp_full_on", 3, Hour);
        fullOn.FullOnVariant = 1;
        var context = CreateContext(DataOf(miss, fullOn));
        context.SetCustomAssignment("exp_miss", 2);
        context.SetCustomAssignment("exp_full_on", 2);
        Clock.Now = Anchor + 2 * Hour;
        for (var i = 0; i < 3; ++i)
        {
            context.GetTreatment("exp_miss").Should().Be(0);
            context.GetTreatment("exp_full_on").Should().Be(1);
            context.GetVariableValue("sb.arm", -1);
        }

        context.PendingCount.Should().Be(2);
    }

    // An override on a switchback with no linked unit is exposed once; setting a linked unit later
    // does not expose the same override again.
    [Test]
    public void ExposedOverrideWithoutALinkedUnitIsNotReexposedAfterSetUnit()
    {
        var experiment = NewSwitchback(2, Hour);
        experiment.SwitchbackClusters = Clusters("user_id", ClusterPath);
        var context = CreateContext(new Dictionary<string, string> { [UnitType] = Uid }, DataOf(experiment));
        context.SetOverride("exp_sb", 1);
        Clock.Now = Anchor + 10;
        context.GetTreatment("exp_sb").Should().Be(1);
        context.SetUnit("user_id", "u1");
        context.GetTreatment("exp_sb").Should().Be(1);
        context.PendingCount.Should().Be(1);
    }

    private sealed class CountingClock : ABSmartly.Time.Clock
    {
        public int Reads;
        public override long Millis()
        {
            Interlocked.Increment(ref Reads);
            return Anchor;
        }
    }

    [Test]
    public void LookupsWithoutSwitchbacksDoNotReadTheClock()
    {
        var unit = AsUnit(NewSwitchback(1, "exp_unit", 2, Hour));
        var clock = new CountingClock();
        var context = Context.CreateWithData(new ContextConfig { PublishDelay = TimeSpan.FromMinutes(10) }.SetUnit(UnitType, Uid),
            DataOf(unit), clock, DataProvider.Object, Publisher, null, new DefaultVariableParser(),
            new AudienceMatcher(new DefaultAudienceDeserializer()), null);
        context.GetTreatment("exp_unit");
        var reads = clock.Reads;
        for (var i = 0; i < 5; ++i)
        {
            context.GetTreatment("exp_unit");
            context.PeekTreatment("exp_unit");
            context.GetVariableValue("sb.arm", -1);
            context.PeekVariableValue("sb.arm", -1);
            context.PeekTreatment("missing");
        }

        clock.Reads.Should().Be(reads);

        var mixed = Context.CreateWithData(new ContextConfig { PublishDelay = TimeSpan.FromMinutes(10) }.SetUnit(UnitType, Uid),
            DataOf(NewSwitchback(2, Hour)), clock, DataProvider.Object, Publisher, null, new DefaultVariableParser(),
            new AudienceMatcher(new DefaultAudienceDeserializer()), null);
        mixed.GetTreatment("exp_sb");
        reads = clock.Reads;
        mixed.PeekTreatment("exp_sb");
        mixed.PeekVariableValue("sb.arm", -1);
        clock.Reads.Should().Be(reads + 2);
    }

    // Traffic eligibility uses the existing per-unit traffic hash, so it matches a unit-randomized
    // twin with the same traffic seed and stays fixed across periods.
    [Test]
    public void TrafficEligibilityIsClusterSticky()
    {
        var eligibleCount = 0;
        var ineligibleCount = 0;
        for (var u = 0; u < 24; ++u)
        {
            var sb = NewSwitchback(1, "exp_sb", 2, Hour);
            sb.TrafficSplit = new[] { 0.5, 0.5 };
            var twin = NewSwitchback(2, "exp_unit", 2, Hour);
            twin.TrafficSplit = new[] { 0.5, 0.5 };
            twin.RandomizationType = null;
            twin.SwitchbackStartAt = null;

            Clock.Now = Anchor;
            var context = CreateContext(new Dictionary<string, string> { [UnitType] = "store_" + u }, DataOf(sb, twin));
            context.GetTreatment("exp_unit");
            for (var i = 0; i < 4; ++i)
            {
                Clock.Now = Anchor + i * Hour;
                context.GetTreatment("exp_sb");
            }

            var exposures = PublishExposures(context);
            exposures.Should().HaveCount(5);
            var twinEligible = exposures[0].Eligible;
            for (var i = 1; i < exposures.Length; ++i)
            {
                exposures[i].Name.Should().Be("exp_sb");
                exposures[i].Eligible.Should().Be(twinEligible);
                if (!twinEligible) exposures[i].Variant.Should().Be(0);
            }

            if (twinEligible) ++eligibleCount;
            else ++ineligibleCount;
        }

        eligibleCount.Should().BePositive();
        ineligibleCount.Should().BePositive();
    }

    [Test]
    public void UnitExperimentsAreUnaffected()
    {
        var unit = NewSwitchback(2, Hour);
        unit.RandomizationType = "unit";
        var absent = NewSwitchback(2, "exp_absent", 2, Hour);
        absent.RandomizationType = null;
        var cased = NewSwitchback(3, "exp_cased", 2, Hour);
        cased.RandomizationType = "Switchback";

        var context = CreateContext(DataOf(unit, absent, cased));
        var variant = context.GetTreatment("exp_sb");
        var absentVariant = context.GetTreatment("exp_absent");
        var casedVariant = context.GetTreatment("exp_cased");
        for (var i = 1; i < 6; ++i)
        {
            Clock.Now = Anchor + i * Hour;
            context.GetTreatment("exp_sb").Should().Be(variant);
            context.GetTreatment("exp_absent").Should().Be(absentVariant);
            context.GetTreatment("exp_cased").Should().Be(casedVariant);
        }

        context.PendingCount.Should().Be(3);
    }

    [Test]
    public void RefreshInvalidatesWhenGridFieldsChangeButNotForAnalysisOnlyFields()
    {
        var context = CreateContext(DataOf(NewSwitchback(2, Hour)));
        Clock.Now = Anchor + 10;
        context.GetTreatment("exp_sb").Should().Be(Expected2Arms[0]);
        context.PendingCount.Should().Be(1);

        var burnsChanged = NewSwitchback(2, Hour);
        burnsChanged.SwitchbackBurnInDurationMs = 600_000L;
        burnsChanged.SwitchbackBurnOutDurationMs = 300_000L;
        burnsChanged.SwitchbackCarryoverDurationMs = 900_000L;
        burnsChanged.SwitchbackEstimand = "other";
        RefreshTo(context, burnsChanged);
        context.GetTreatment("exp_sb").Should().Be(Expected2Arms[0]);
        context.PendingCount.Should().Be(1);

        RefreshTo(context, NewSwitchback(2, 2 * Hour));
        context.GetTreatment("exp_sb").Should().Be(Expected2Arms[0]);
        context.PendingCount.Should().Be(2);

        var designChanged = NewSwitchback(2, 2 * Hour);
        designChanged.SwitchbackDesignType = "other_design";
        RefreshTo(context, designChanged);
        context.GetTreatment("exp_sb");
        context.PendingCount.Should().Be(3);

        var toUnit = NewSwitchback(2, 2 * Hour);
        toUnit.RandomizationType = "unit";
        RefreshTo(context, toUnit);
        context.GetTreatment("exp_sb");
        context.PendingCount.Should().Be(4);

        RefreshTo(context, NewSwitchback(2, 2 * Hour));
        context.GetTreatment("exp_sb");
        context.PendingCount.Should().Be(5);

        var pathChanged = NewSwitchback(2, 2 * Hour);
        pathChanged.SwitchbackClusters = Clusters(UnitType, "'store_key'");
        RefreshTo(context, pathChanged);
        context.GetTreatment("exp_sb").Should().Be(Expected2Arms[0]);
        context.PendingCount.Should().Be(6);

        var linkAdded = NewSwitchback(2, 2 * Hour);
        linkAdded.SwitchbackClusters = Clusters(UnitType, "'store_key'", "device_id", "city");
        RefreshTo(context, linkAdded);
        context.GetTreatment("exp_sb").Should().Be(Expected2Arms[0]);
        context.PendingCount.Should().Be(7);

        var linksReordered = NewSwitchback(2, 2 * Hour);
        linksReordered.SwitchbackClusters = Clusters("device_id", "city", UnitType, "'store_key'");
        RefreshTo(context, linksReordered);
        context.GetTreatment("exp_sb").Should().Be(Expected2Arms[0]);
        context.PendingCount.Should().Be(8);

        var valuesSet = NewSwitchback(2, 2 * Hour);
        valuesSet.SwitchbackClusters = linksReordered.SwitchbackClusters;
        valuesSet.SwitchbackClusterValues = new[] { ClusterKey };
        RefreshTo(context, valuesSet);
        context.GetTreatment("exp_sb").Should().Be(Expected2Arms[0]);
        context.PendingCount.Should().Be(9);

        // unitType is not identity: switchbacks never read it.
        var unitTypeChanged = NewSwitchback(2, 2 * Hour);
        unitTypeChanged.SwitchbackClusters = valuesSet.SwitchbackClusters;
        unitTypeChanged.SwitchbackClusterValues = valuesSet.SwitchbackClusterValues;
        unitTypeChanged.UnitType = "anything";
        RefreshTo(context, unitTypeChanged);
        context.GetTreatment("exp_sb").Should().Be(Expected2Arms[0]);
        context.PendingCount.Should().Be(9);

        var reseeded = NewSwitchback(2, 2 * Hour);
        reseeded.SwitchbackClusters = valuesSet.SwitchbackClusters;
        reseeded.SwitchbackClusterValues = valuesSet.SwitchbackClusterValues;
        reseeded.SeedHi = 99;
        reseeded.Split = new[] { 0.2, 0.8 };
        RefreshTo(context, reseeded);
        context.GetTreatment("exp_sb").Should().Be(Expected2Arms[0]);
        context.PendingCount.Should().Be(9);
    }

    // Burn-in, burn-out and carryover windows are analysis-only: exposures continue through them,
    // one per period, with the schedule's variant.
    [Test]
    public void ExposuresContinueDuringBurnAndCarryoverWindows()
    {
        var experiment = NewSwitchback(2, Hour);
        experiment.SwitchbackBurnInDurationMs = 20 * 60_000L;
        experiment.SwitchbackBurnOutDurationMs = 10 * 60_000L;
        experiment.SwitchbackCarryoverDurationMs = 30 * 60_000L;
        var context = CreateContext(DataOf(experiment));
        for (var i = 0; i < 3; ++i)
        {
            Clock.Now = Anchor + i * Hour + 60_000L;
            context.GetTreatment("exp_sb").Should().Be(Expected2Arms[i]);
            Clock.Now = Anchor + (i + 1) * Hour - 60_000L;
            context.GetTreatment("exp_sb").Should().Be(Expected2Arms[i]);
        }

        PublishExposures(context).Should().Equal(
            NewExposure(1, "exp_sb", Expected2Arms[0], Anchor + 60_000L, true, true),
            NewExposure(1, "exp_sb", Expected2Arms[1], Anchor + Hour + 60_000L, true, true),
            NewExposure(1, "exp_sb", Expected2Arms[2], Anchor + 2 * Hour + 60_000L, true, true));
    }

    [Test]
    public void RefreshSettingStartAtStartsTheGrid()
    {
        var pending = NewSwitchback(2, Hour);
        pending.SwitchbackStartAt = null;
        var context = CreateContext(DataOf(pending));
        Clock.Now = Anchor + 10;
        context.GetTreatment("exp_sb").Should().Be(0);

        RefreshTo(context, NewSwitchback(2, Hour));
        context.GetTreatment("exp_sb").Should().Be(Expected2Arms[0]);
        context.PendingCount.Should().Be(1);
    }

    // Holdouts never apply to a switchback: a holdout that would hold the unit out neither
    // suppresses the switchback nor records its own exposure through it.
    [Test]
    public void HoldoutsNeverApplyToASwitchback()
    {
        // split [0.1,0.9] -> variant 0 (held out) for Uid.
        var covered = NewSwitchback(2, Hour);
        covered.HoldoutIds = new[] { 11 };
        var data = new ContextData(new[] { covered }, new[] { Holdout(new[] { 0.1, 0.9 }) });

        var context = CreateContext(data);
        for (var i = 0; i < 3; ++i)
        {
            Clock.Now = Anchor + i * Hour;
            context.GetTreatment("exp_sb").Should().Be(Expected2Arms[i]);
            context.GetVariableValue("sb.arm", -1).Should().Be(Expected2Arms[i]);
        }

        PublishExposures(context).Should().Equal(
            NewExposure(1, "exp_sb", Expected2Arms[0], Anchor, true, true),
            NewExposure(1, "exp_sb", Expected2Arms[1], Anchor + Hour, true, true),
            NewExposure(1, "exp_sb", Expected2Arms[2], Anchor + 2 * Hour, true, true));
        context.CachedAssignmentForTest("exp_sb")!.Holdout.Should().BeNull();
    }

    // Collector assignmentRuleOverridesTheSchedule: rules are evaluated first, so a matching rule
    // assigns its variant even before the anchor, with ruleOverride set and the exposure recorded.
    [Test]
    public void AssignmentRuleOverridesTheSchedule()
    {
        var experiment = NewSwitchback(2, Hour);
        experiment.AssignmentRules = AssignVariant1;
        for (var u = 0; u < 10; ++u)
        {
            Clock.Now = Anchor - 1;
            var context = CreateContext(new Dictionary<string, string> { [UnitType] = "user-" + u }, DataOf(experiment));
            context.GetTreatment("exp_sb").Should().Be(1);
            context.GetVariableValue("sb.arm", -1).Should().Be(1);
            for (var i = 0; i < 4; ++i)
            {
                Clock.Now = Anchor + i * Hour;
                context.GetTreatment("exp_sb").Should().Be(1);
            }

            var exposures = PublishExposures(context);
            exposures.Should().HaveCount(5);
            exposures[0].Should().Be(RuleExposure("exp_sb", 1, Anchor - 1));
            for (var i = 0; i < 4; ++i) exposures[i + 1].Should().Be(RuleExposure("exp_sb", 1, Anchor + i * Hour));
        }
    }

    // Collector unservableOnlyReplacesTheVariantDrawNotFullOnOrRules.
    [Test]
    public void UnservableOnlyReplacesTheVariantDrawNotFullOnOrRules()
    {
        var fullOn = NewSwitchback(1, "sb_full_on", 2, Hour);
        fullOn.SwitchbackStartAt = null;
        fullOn.FullOnVariant = 1;
        var rules = NewSwitchback(2, "sb_rules", 2, Hour);
        rules.SwitchbackPeriodMs = 0L;
        rules.AssignmentRules = AssignVariant1;
        var unalignedRules = NewSwitchback(3, "sb_unaligned_rules", 2, Hour);
        unalignedRules.SwitchbackStartAt = Anchor + 1;
        unalignedRules.AssignmentRules = AssignVariant1;
        var context = CreateContext(new Dictionary<string, string> { [UnitType] = "15" },
            DataOf(fullOn, rules, unalignedRules));

        context.GetTreatment("sb_full_on").Should().Be(1);
        context.GetTreatment("sb_rules").Should().Be(1);
        context.GetTreatment("sb_unaligned_rules").Should().Be(1);
        PublishExposures(context).Should().Equal(
            NewExposure(1, "sb_full_on", 1, Anchor, true, true, fullOn: true),
            NewExposure(2, "sb_rules", 1, Anchor, false, true, ruleOverride: true),
            NewExposure(3, "sb_unaligned_rules", 1, Anchor, false, true, ruleOverride: true));
    }

    [Test]
    public void AssignmentRulesDoNotApplyToAnIneligibleCluster()
    {
        var experiment = NewSwitchback(2, Hour);
        experiment.AssignmentRules = AssignVariant1;
        experiment.SwitchbackClusterValues = new[] { "other-store" };
        var context = CreateContext(DataOf(experiment));
        context.GetTreatment("exp_sb").Should().Be(0);
        context.PendingCount.Should().Be(0);
    }

    // A split rule randomizes over the serving unit (the Collector resets its assigner to the
    // serving unit's hash), not over the payload's unitType.
    [Test]
    public void SplitRuleRandomizesOverTheServingUnit()
    {
        var experiment = NewSwitchback(2, Hour);
        experiment.UnitType = "user_id";
        experiment.AssignmentRules = "{\"rules\":[{\"type\":\"split\",\"environments\":[],\"percentages\":\"50/50\"}]}";
        var ones = 0;
        for (var u = 0; u < 32; ++u)
        {
            var uid = "store-" + u;
            var expected = new VariantAssigner(Md5.HashToUtf8Bytes(uid)).Assign(new[] { 0.5, 0.5 }, SeedHi, SeedLo);
            var context = CreateContext(new Dictionary<string, string> { [UnitType] = uid, ["user_id"] = "fixed-user" },
                DataOf(experiment));
            context.GetTreatment("exp_sb").Should().Be(expected, uid);
            PublishExposures(context).Should().Equal(new[] { RuleExposure("exp_sb", expected, Anchor) }, uid);
            ones += expected;
        }

        ones.Should().BeInRange(1, 31);
    }

    // A split rule's revalidation after a SetAttribute uses the serving unit too, not the payload's
    // unitType: with only the serving unit set, the slot stays current instead of being rebuilt.
    [Test]
    public void SplitRuleRevalidatesOverTheServingUnit()
    {
        var experiment = NewSwitchback(2, Hour);
        experiment.SwitchbackClusters = Clusters("device_id", ClusterPath);
        experiment.AssignmentRules = "{\"rules\":[{\"type\":\"split\",\"environments\":[],\"percentages\":\"50/50\"}]}";
        var context = CreateContext(new Dictionary<string, string> { ["device_id"] = "d-1" }, DataOf(experiment));
        Clock.Now = Anchor + 10;
        var variant = context.PeekTreatment("exp_sb");
        var cached = context.CachedAssignmentForTest("exp_sb");
        cached!.RuleOverride.Should().BeTrue();
        cached.UnitType.Should().Be("device_id");

        context.SetAttribute("unrelated", 1);
        context.PeekTreatment("exp_sb").Should().Be(variant);
        context.CachedAssignmentForTest("exp_sb").Should().BeSameAs(cached);
    }

    [Test]
    public void AudienceStrictMismatchIsRecordedAsControl()
    {
        var experiment = NewSwitchback(2, Hour);
        experiment.Audience = StrictGoldAudience;
        experiment.AudienceStrict = true;
        var context = CreateContext(DataOf(experiment));
        Clock.Now = Anchor + Hour;
        context.GetTreatment("exp_sb").Should().Be(0);
        PublishExposures(context).Should().Equal(StrictMismatchExposure(Anchor + Hour));
    }

    // Exact wire form of a switchback strict audience mismatch. Runs in period 1, whose draw is 1,
    // so the variant 0 below is the strict mismatch's control and not a draw of arm 0.
    [Test]
    public void AudienceStrictMismatchExposureSerializesExactly()
    {
        var experiment = NewSwitchback(2, Hour);
        experiment.Audience = StrictGoldAudience;
        experiment.AudienceStrict = true;
        Clock.Now = Anchor;
        var context = CreateContext(DataOf(experiment));
        Expected2Arms[1].Should().Be(1);
        Clock.Now = Anchor + Hour + 5;
        context.GetTreatment("exp_sb");
        Clock.Now = Anchor + Hour + 9;

        var json = Encoding.UTF8.GetString(new DefaultContextEventSerializer().Serialize(PublishEvent(context)));
        json.Should().Be("{\"hashed\":true,\"units\":[{\"type\":\"store_id\",\"uid\":\"" + UidHash + "\"}]," +
                         "\"publishedAt\":" + (Anchor + Hour + 9) + "," +
                         "\"exposures\":[" +
                         "{\"id\":1,\"name\":\"exp_sb\",\"unit\":\"store_id\",\"variant\":0,\"exposedAt\":" + (Anchor + Hour + 5) +
                         ",\"assigned\":false,\"eligible\":true,\"overridden\":false,\"fullOn\":false,\"custom\":false," +
                         "\"audienceMismatch\":true,\"ruleOverride\":false}" +
                         "]," +
                         "\"attributes\":[{\"name\":\"" + ClusterPath + "\",\"value\":\"" + ClusterKey + "\",\"setAt\":" + Anchor + "}]}");
    }

    // The audience check comes before the period check, so a strict mismatch before the anchor is
    // recorded.
    [Test]
    public void AudienceStrictMismatchBeforeTheAnchorIsRecorded()
    {
        var experiment = NewSwitchback(2, Hour);
        experiment.Audience = StrictGoldAudience;
        experiment.AudienceStrict = true;
        Clock.Now = Anchor - 1;
        var context = CreateContext(DataOf(experiment));
        context.GetTreatment("exp_sb").Should().Be(0);
        PublishExposures(context).Should().Equal(StrictMismatchExposure(Anchor - 1));
    }

    [Test]
    public void AudienceStrictMismatchWithAnUnservableScheduleIsRecorded()
    {
        var experiment = NewSwitchback(2, Hour);
        experiment.Audience = StrictGoldAudience;
        experiment.AudienceStrict = true;
        experiment.SwitchbackStartAt = null;
        Clock.Now = Anchor + Hour + 3;
        var context = CreateContext(DataOf(experiment));
        context.GetTreatment("exp_sb").Should().Be(0);
        PublishExposures(context).Should().Equal(StrictMismatchExposure(Anchor + Hour + 3));
    }

    // A missing or unreadable cluster map takes the legacy path, which returns an ineligible control
    // before the audience is evaluated, so a strict mismatch there records nothing.
    [Test]
    public void AudienceStrictMismatchWithAMissingOrUnreadableClusterMapRecordsNothing()
    {
        foreach (var clusters in new[] { null, Clusters(UnitType, "'geo") })
        {
            var experiment = NewSwitchback(2, Hour);
            experiment.Audience = StrictGoldAudience;
            experiment.AudienceStrict = true;
            experiment.SwitchbackClusters = clusters;
            Clock.Now = Anchor + Hour + 3;
            var context = CreateContext(DataOf(experiment));
            context.GetTreatment("exp_sb").Should().Be(0);
            context.PendingCount.Should().Be(0);
        }
    }

    // A strict-mismatched switchback is not assigned, so on a shared variable key it gives way to the
    // next experiment that declares the key, as a unit experiment does. Only that one is exposed.
    [Test]
    public void StrictMismatchedSwitchbackGivesWayOnASharedVariableKey()
    {
        var sb = NewSwitchback(2, Hour);
        sb.Audience = StrictGoldAudience;
        sb.AudienceStrict = true;
        var unit = AsUnit(NewSwitchback(2, "exp_unit", 2, Hour));
        unit.Variants[0] = new ExperimentVariant { Name = "A", Config = "{\"sb.arm\":10}" };
        unit.Variants[1] = new ExperimentVariant { Name = "B", Config = "{\"sb.arm\":11}" };
        unit.FullOnVariant = 1;
        Clock.Now = Anchor + Hour + 3;
        var context = CreateContext(DataOf(sb, unit));
        context.VariableKeys.Should().BeEquivalentTo(new Dictionary<string, List<string>>
            { ["sb.arm"] = new() { "exp_sb", "exp_unit" } });

        context.PeekVariableValue("sb.arm", -1).Should().Be(11);
        context.PendingCount.Should().Be(0);
        context.GetVariableValue("sb.arm", -1).Should().Be(11);
        PublishExposures(context).Should().Equal(NewExposure(2, "exp_unit", 1, Anchor + Hour + 3, true, true, fullOn: true));
    }

    [Test]
    public void StrictMismatchedSwitchbackAloneOnAVariableKeyReturnsTheDefault()
    {
        var sb = NewSwitchback(2, Hour);
        sb.Audience = StrictGoldAudience;
        sb.AudienceStrict = true;
        Clock.Now = Anchor + Hour + 3;
        var context = CreateContext(DataOf(sb));
        context.PeekVariableValue("sb.arm", -1).Should().Be(-1);
        context.GetVariableValue("sb.arm", -1).Should().Be(-1);
        context.PendingCount.Should().Be(0);
    }
}
