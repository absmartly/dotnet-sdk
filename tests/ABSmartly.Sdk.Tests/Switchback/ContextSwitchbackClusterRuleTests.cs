using ABSmartly.Internal;
using ABSmartly.Internal.Hashing;
using ABSmartly.Models;
using ABSmartly.Services;

namespace ABSmartly.Sdk.Tests.Switchback;

// Ported from java-sdk ContextSwitchbackClusterRuleTest, itself the Collector's
// ContextAPISwitchbackTest cluster-rule cases transcribed: "sbc" (id 900, "34/33/33", 1h) linked to
// user_id (unit type id 2) at "profile/store" and to session_id (unit type id 1) at "session_store",
// declared in unit type id order, so session_id comes first although the user_id link was added
// first. A context holds one uid per unit type, so the declared source order alone decides which
// unit serves. An ineligible response is checked through Context.CachedAssignmentForTest.
[TestFixture]
public class ContextSwitchbackClusterRuleTests
{
    private const long Hour = 3_600_000L;
    private const long Anchor = 1_767_225_600_000L;
    private const long Period = 5;
    private const long Now = Anchor + Period * Hour + 17 * 60_000L;
    private const int SeedHi = 0x1234;
    private const int SeedLo = 0x5678abcd;
    private static readonly long Seed = ((long)SeedHi << 32) | (SeedLo & 0xffffffffL);
    private const int TrafficSeedHi = 7;
    private const int TrafficSeedLo = 11;

    private const string Session = "session_id";
    private const string User = "user_id";

    private MutableClock _clock = null!;
    private Experiment _experiment = null!;
    private List<Exposure> _captured = null!;
    private List<PublishEvent> _published = null!;
    private readonly List<(string Type, string Path)> _links = new();

    [SetUp]
    public void AddClusteredSwitchback()
    {
        _clock = new MutableClock(Now);
        _experiment = SwitchbackExperiment("sbc", 900, User, "34/33/33");
        UnlinkAll();
        Link(User, "profile/store");
        Link(Session, "session_store");
    }

    private static Experiment SwitchbackExperiment(string name, int id, string unitType, string split)
    {
        var parts = split.Split('/');
        return new Experiment
        {
            Id = id,
            Name = name,
            UnitType = unitType,
            Iteration = 1,
            SeedHi = SeedHi,
            SeedLo = SeedLo,
            Split = parts.Select(p => int.Parse(p) / 100.0).ToArray(),
            Variants = parts.Select((_, i) => new ExperimentVariant
                { Name = i.ToString(), Config = "{\"sbc.config\":\"test config " + i + "\"}" }).ToArray(),
            TrafficSeedHi = TrafficSeedHi,
            TrafficSeedLo = TrafficSeedLo,
            TrafficSplit = new[] { 0.0, 1.0 },
            Applications = new[] { new ExperimentApplication { Name = "website" } },
            RandomizationType = "switchback",
            SwitchbackStartAt = Anchor,
            SwitchbackPeriodMs = Hour,
            SwitchbackDesignType = "balanced_block"
        };
    }

    private static Experiment UnitExperiment(string name, int id, string unitType)
    {
        var experiment = SwitchbackExperiment(name, id, unitType, "50/50");
        experiment.RandomizationType = null;
        experiment.SwitchbackStartAt = null;
        experiment.SwitchbackPeriodMs = null;
        experiment.SwitchbackDesignType = null;
        experiment.Variants = new[] { new ExperimentVariant { Name = "0" }, new ExperimentVariant { Name = "1" } };
        return experiment;
    }

    private void UnlinkAll()
    {
        _links.Clear();
        RefreshLinks();
    }

    private void Link(string unitType, string path)
    {
        _links.Add((unitType, path));
        RefreshLinks();
    }

    // switchbackClusters in declared (unit type id ascending) order: session_id (1) before user_id (2).
    private void RefreshLinks()
    {
        var clusters = new List<KeyValuePair<string, string>>();
        foreach (var type in new[] { Session, User, "device_id" })
        foreach (var link in _links.Where(l => l.Type == type))
            clusters.Add(new KeyValuePair<string, string>(link.Type, link.Path));
        _experiment.SwitchbackClusters = _links.Count == 0 ? null : clusters.ToArray();
    }

    private static int Scheduled(string clusterKey) => SwitchbackAssigner.Assign(clusterKey, Period, Seed, 3);

    private static (string, string) Unit(string type, string uid) => (type, uid);

    private static Dictionary<string, object> Attrs(params object[] entries)
    {
        var map = new Dictionary<string, object>();
        for (var i = 0; i < entries.Length; i += 2) map[(string)entries[i]] = entries[i + 1];
        return map;
    }

    private static Dictionary<string, object> Store(object value) => Attrs("store", value);

    private Context CreateContext(Dictionary<string, object>? attributes, Experiment[] experiments,
        params (string Type, string Uid)[] units)
    {
        _captured = new List<Exposure>();
        _published = new List<PublishEvent>();
        var logger = new Mock<IContextEventLogger>();
        logger.Setup(x => x.HandleEvent(It.IsAny<IContext>(), EventType.Exposure, It.IsAny<object>()))
            .Callback<IContext, EventType, object>((_, _, e) => _captured.Add((Exposure)e));
        var publisher = new Mock<IContextPublisher>();
        publisher.Setup(x => x.PublishAsync(It.IsAny<IContext>(), It.IsAny<PublishEvent>()))
            .Callback<IContext, PublishEvent>((_, e) => _published.Add(e)).Returns(Task.CompletedTask);

        var config = new ContextConfig { PublishDelay = TimeSpan.FromMinutes(10) };
        foreach (var unit in units) config.SetUnit(unit.Type, unit.Uid);
        var context = Context.CreateWithData(config, new ContextData(experiments), _clock,
            Mock.Of<IContextDataProvider>(), publisher.Object, logger.Object, new DefaultVariableParser(),
            new AudienceMatcher(new DefaultAudienceDeserializer()), null);
        if (attributes != null) context.SetAttributes(attributes);
        return context;
    }

    // The Collector's ExperimentAssignment for "sbc", or null when it lists none (not applicable).
    private sealed class Assigned
    {
        public string Unit = null!;
        public bool Eligible;
        public int Variant;
        public bool FullOn;
        public bool RuleOverride;
        public bool Silent;
        public string? Config;
        public List<Exposure> Exposures = null!;
    }

    private Assigned? Sbc(Dictionary<string, object>? attributes, params (string, string)[] units) =>
        SbcWith(attributes, new[] { _experiment }, units);

    // Every other experiment is resolved (and exposed) before "sbc", so the unit hash and assigner
    // caches it fills are in place when "sbc" resolves. Only "sbc" exposures are returned.
    private Assigned? SbcWith(Dictionary<string, object>? attributes, Experiment[] experiments,
        params (string, string)[] units)
    {
        var context = CreateContext(attributes, experiments, units);
        foreach (var other in experiments.Where(e => e.Name != "sbc")) context.GetTreatment(other.Name);
        var variant = context.GetTreatment("sbc");
        var resolution = context.CachedAssignmentForTest("sbc");
        resolution.Should().NotBeNull();
        var config = (string?)context.GetVariableValue("sbc.config", null!);
        var exposures = _captured.Where(e => e.Name == "sbc").ToList();
        // Keyed on the serving status, not on assigned: a strict audience mismatch is not assigned
        // yet is still an assignment the Collector lists.
        if (resolution!.Switchback.Serving.Status == SwitchbackServing.ServingStatus.Absent)
        {
            variant.Should().Be(0);
            config.Should().BeNull();
            exposures.Should().BeEmpty();
            return null;
        }

        return new Assigned
        {
            Unit = resolution.UnitType,
            Eligible = resolution.Eligible,
            Silent = resolution.Switchback.Silent,
            Variant = variant,
            FullOn = resolution.FullOn,
            RuleOverride = resolution.RuleOverride,
            Config = config,
            Exposures = exposures
        };
    }

    // An ineligible cluster resolution is still an assignment, as control, under the first
    // linked-type unit in declared order, and no exposure is recorded for it.
    private void AssertIneligible(Dictionary<string, object> attributes, string expectedUnit, params (string, string)[] units)
    {
        var assignment = Sbc(attributes, units);
        assignment.Should().NotBeNull();
        assignment!.Unit.Should().Be(expectedUnit);
        assignment.Eligible.Should().BeFalse();
        assignment.Silent.Should().BeTrue();
        assignment.Variant.Should().Be(0);
        assignment.FullOn.Should().BeFalse();
        assignment.RuleOverride.Should().BeFalse();
        assignment.Config.Should().Be("test config 0");
        assignment.Exposures.Should().BeEmpty();
    }

    [TestCase("null-cluster")]
    [TestCase("missing-links")]
    [TestCase("unreadable-path")]
    [TestCase("geo.city")]
    [TestCase("'geo.city'")]
    [TestCase("\"geo.city\"")]
    [TestCase("unknown-unit")]
    public void MissingOrUnreadableClusterConfigurationReturnsOnlyLegacyIneligibleControl(string broken)
    {
        UnlinkAll();
        if (broken == "unreadable-path")
        {
            Link(Session, "'geo");
        }
        else if (broken == "unknown-unit")
        {
            Link("device_id", "session_store");
            Sbc(Attrs("session_store", "s-1"), Unit(Session, "x1"), Unit(User, "u1")).Should().BeNull();
            return;
        }
        else if (broken == "missing-links")
        {
            _experiment.SwitchbackClusters = Array.Empty<KeyValuePair<string, string>>();
        }
        else if (broken != "null-cluster")
        {
            Link(Session, broken);
        }

        var attributes = Attrs("geo", Attrs("city", "Porto"), "geo.city", "Faro", "session_store", "s-1");
        // The declared unit (user_id) only identifies an ineligible control response.
        AssertIneligible(attributes, User, Unit(Session, "x1"), Unit(User, "u1"));
        // Without a unit of the declared type, there is no response at all.
        Sbc(attributes, Unit(Session, "x1")).Should().BeNull();
    }

    [Test]
    public void TrafficUsesTheServingUidNotTheExperimentUid()
    {
        _experiment.TrafficSplit = new[] { 0.6, 0.4 };
        var ordinary = UnitExperiment("ordinary", 901, User);
        var split = new[] { 0.6, 0.4 };
        var witnesses = new string?[2];
        for (var i = 0; i < 1000 && (witnesses[0] == null || witnesses[1] == null); ++i)
        {
            var uid = "traffic-" + i;
            witnesses[new VariantAssigner(Md5.HashToUtf8Bytes(uid)).Assign(split, TrafficSeedHi, TrafficSeedLo) == 0 ? 0 : 1] = uid;
        }

        witnesses.Should().NotContainNulls();
        const string key = "s-18";
        Scheduled(key).Should().NotBe(0);
        foreach (var servingEligible in new[] { false, true })
        {
            var assignment = SbcWith(Attrs("session_store", key, "profile", Store(key)), new[] { _experiment, ordinary },
                Unit(Session, witnesses[servingEligible ? 1 : 0]!), Unit(User, witnesses[servingEligible ? 0 : 1]!))!;
            assignment.Unit.Should().Be(Session);
            assignment.Eligible.Should().Be(servingEligible);
            assignment.Variant.Should().Be(servingEligible ? Scheduled(key) : 0);
            // A traffic miss on an eligible cluster is still recorded, under the serving unit.
            assignment.Exposures.Should().ContainSingle();
            assignment.Exposures[0].Unit.Should().Be(Session);
            assignment.Exposures[0].Eligible.Should().Be(servingEligible);
        }
    }

    [Test]
    public void OneMatchingUnitServesItsValue()
    {
        var assignment = Sbc(Attrs("profile", Store("s-17")), Unit(User, "u1"))!;
        assignment.Eligible.Should().BeTrue();
        assignment.Unit.Should().Be(User);
        assignment.Variant.Should().Be(Scheduled("s-17"));
    }

    // A strict audience mismatch on an eligible cluster is not assigned, yet the Collector still
    // lists it, as control under the serving unit, and publishes its exposure.
    [Test]
    public void StrictAudienceMismatchOnAnEligibleClusterIsListedAsControlAndRecorded()
    {
        _experiment.Audience = "{\"filter\":[{\"eq\":[{\"var\":\"tier\"},{\"value\":\"gold\"}]}]}";
        _experiment.AudienceStrict = true;
        var assignment = Sbc(Attrs("session_store", "s-17"), Unit(Session, "x1"))!;

        assignment.Unit.Should().Be(Session);
        assignment.Eligible.Should().BeTrue();
        assignment.Silent.Should().BeFalse();
        assignment.Variant.Should().Be(0);
        assignment.Exposures.Should().ContainSingle();
        assignment.Exposures[0].AudienceMismatch.Should().BeTrue();
        assignment.Exposures[0].Assigned.Should().BeFalse();
    }

    [Test]
    public void TwoAgreeingUnitsServeTheValue()
    {
        var assignment = Sbc(Attrs("profile", Store("s-17"), "session_store", "s-17"), Unit(User, "u1"), Unit(Session, "x1"))!;
        assignment.Eligible.Should().BeTrue();
        assignment.Variant.Should().Be(Scheduled("s-17"));
    }

    [Test]
    public void TwoDisagreeingUnitsAreIneligible()
    {
        // session_id (type id 1) is first in declared order whatever the unit order.
        AssertIneligible(Attrs("profile", Store("s-17"), "session_store", "s-18"), Session, Unit(User, "u1"),
            Unit(Session, "x1"));
    }

    // The Collector serves two session_id units agreeing on one value. A context holds one uid per
    // unit type, so the second is rejected and only the single-unit path is reproducible.
    [Test]
    public void TwoUnitsOfOneTypeWithOnePathAgreeOnTheSameValue()
    {
        const string key = "s-17";
        Scheduled(key).Should().NotBe(0);
        var context = CreateContext(Attrs("session_store", key), new[] { _experiment }, Unit(Session, "x1"));
        FluentActions.Invoking(() => context.SetUnit(Session, "x2")).Should().Throw<ArgumentException>();

        var assignment = Sbc(Attrs("session_store", key), Unit(Session, "x1"))!;
        assignment.Eligible.Should().BeTrue();
        assignment.Variant.Should().Be(Scheduled(key));
    }

    [Test]
    public void NoMatchingUnitIsNotApplicable()
    {
        Sbc(Attrs("session_store", "s-1"), Unit("device_id", "d1")).Should().BeNull();
        Sbc(Attrs("session_store", "s-1")).Should().BeNull();
    }

    [Test]
    public void MatchingUnitWithoutAValueIsIneligible()
    {
        AssertIneligible(Attrs("other", "s-1"), User, Unit(User, "u1"));
        AssertIneligible(Attrs("other", "s-1"), Session, Unit(User, "u1"), Unit(Session, "x1"));
    }

    [Test]
    public void NonStringNonIntegerValuesAreIneligible()
    {
        foreach (var value in new object[] { 1.5, 3.0, 3.0f, true, Attrs("a", 1), new List<object> { "a" }, "" })
            AssertIneligible(Attrs("session_store", value), Session, Unit(Session, "x1"));
    }

    [Test]
    public void WholeNumbersAreKeyedByTheirDecimalText()
    {
        Scheduled("17").Should().NotBe(0);
        var assignment = Sbc(Attrs("session_store", 17), Unit(Session, "x1"))!;

        assignment.Eligible.Should().BeTrue();
        assignment.Variant.Should().Be(Scheduled("17"));
        Sbc(Attrs("session_store", 17, "profile", Store("17")), Unit(Session, "x1"), Unit(User, "u1"))!.Eligible
            .Should().BeTrue();
    }

    [Test]
    public void NestedAndArrayPathsAreRead()
    {
        UnlinkAll();
        Link(Session, "items/1/store");

        var assignment = Sbc(Attrs("items", new List<object> { Store("a"), Store("b") }), Unit(Session, "x1"))!;
        assignment.Variant.Should().Be(Scheduled("b"));
        Scheduled("a").Should().NotBe(Scheduled("b"));
    }

    [Test]
    public void ExposureUnitIsTheFirstMatchingUnitInDeclaredOrderWhateverTheUnitOrder()
    {
        const string key = "s-18";
        Scheduled(key).Should().NotBe(0);
        var assignment = Sbc(Attrs("profile", Store(key), "session_store", key), Unit(User, "u1"), Unit(Session, "x1"))!;

        assignment.Unit.Should().Be(Session);
        assignment.Exposures[0].Unit.Should().Be(Session);
        assignment.Variant.Should().Be(Scheduled(key));
    }

    [Test]
    public void IneligibleClusterNeverPublishesAnExposure()
    {
        var context = CreateContext(Attrs("session_store", "a", "profile", Store("b")), new[] { _experiment },
            Unit(User, "u1"), Unit(Session, "x1"));
        context.GetTreatment("sbc").Should().Be(0);
        context.PendingCount.Should().Be(0);
        context.Publish();
        _published.Should().BeEmpty();
        _captured.Should().BeEmpty();
    }

    [Test]
    public void AUnitWithoutAValueDoesNotServeEvenIfFirstInDeclaredOrder()
    {
        const string key = "s-17";
        Scheduled(key).Should().NotBe(0);
        var assignment = Sbc(Attrs("profile", Store(key)), Unit(Session, "x1"), Unit(User, "u1"))!;

        assignment.Unit.Should().Be(User);
        assignment.Exposures[0].Unit.Should().Be(User);
        assignment.Variant.Should().Be(Scheduled(key));
    }

    [Test]
    public void ValueNotInTheFilterIsIneligible()
    {
        _experiment.SwitchbackClusterValues = new[] { "s-1", "s-2" };

        AssertIneligible(Attrs("session_store", "s-3"), Session, Unit(Session, "x1"));
        var allowed = Sbc(Attrs("session_store", "s-2"), Unit(Session, "x1"))!;
        allowed.Eligible.Should().BeTrue();
        allowed.Variant.Should().Be(Scheduled("s-2"));
    }

    [TestCase(true)]
    [TestCase(false)]
    public void NullOrEmptyFilterAssignsEveryValue(bool empty)
    {
        _experiment.SwitchbackClusterValues = empty ? Array.Empty<string>() : null;

        for (var i = 0; i < 20; ++i)
        {
            var assignment = Sbc(Attrs("session_store", "s-" + i), Unit(Session, "x1"))!;
            assignment.Eligible.Should().BeTrue();
            assignment.Variant.Should().Be(Scheduled("s-" + i));
        }
    }

    // A valid cluster map whose linked types are all absent does not fall back to the experiment's
    // declared unitType: no assignment, no exposure.
    [Test]
    public void ValidClusterWithoutLinkedUnitDoesNotFallBackToExperimentUnit()
    {
        UnlinkAll();
        Link(Session, "session_store");
        const string key = "s-17";
        Scheduled(key).Should().NotBe(0);

        Sbc(Attrs("session_store", key, "profile", Store(key)), Unit(User, "u1")).Should().BeNull();
        var linked = Sbc(Attrs("session_store", key), Unit(User, "u1"), Unit(Session, "x1"))!;
        linked.Unit.Should().Be(Session);
        linked.Variant.Should().Be(Scheduled(key));
    }

    // The Collector's wire-rule oracle SdkSwitchbackRule, transcribed: period index from the anchor
    // re-ceiled to the hour, traffic on the serving uid's hash, then the schedule draw over the
    // cluster key. A null cluster key has no draw.
    private static class SdkSwitchbackRule
    {
        public const long NoPeriod = long.MinValue;

        private static long FloorDiv(long a, long b) => SwitchbackAssigner.FloorDiv(a, b);

        public static long PeriodIndex(Experiment experiment, long nowMs)
        {
            if (experiment.SwitchbackStartAt is not { } startAt || experiment.SwitchbackPeriodMs is not { } periodMs ||
                periodMs <= 0)
                return NoPeriod;
            if (experiment.Split is not { Length: >= 2 }) return NoPeriod;
            var anchor = -FloorDiv(-startAt, Hour) * Hour;
            if (nowMs < anchor) return NoPeriod;
            return FloorDiv(nowMs - anchor, periodMs);
        }

        public static (int, bool) Evaluate(Experiment experiment, string uid, string? clusterKey, long nowMs)
        {
            var periodIndex = PeriodIndex(experiment, nowMs);
            if (clusterKey == null || periodIndex == NoPeriod) return (0, false);
            var eligible = new VariantAssigner(Md5.HashToUtf8Bytes(uid)).Assign(experiment.TrafficSplit,
                experiment.TrafficSeedHi, experiment.TrafficSeedLo) == 1;
            if (!eligible) return (0, false);
            return (SwitchbackAssigner.Assign(clusterKey, periodIndex,
                ((long)experiment.SeedHi << 32) | (experiment.SeedLo & 0xffffffffL), experiment.Split.Length), true);
        }
    }

    public enum ClusterCase
    {
        Single,
        DisagreeingSecondSource,
        MissingValue,
        FilteredValue
    }

    private Context GridContext(ClusterCase cluster, string uid, string key)
    {
        UnlinkAll();
        Link(Session, "session_store");
        _experiment.SwitchbackClusterValues = null;
        switch (cluster)
        {
            case ClusterCase.DisagreeingSecondSource:
                Link(User, "profile/store");
                return CreateContext(Attrs("session_store", key, "profile", Store(key + "-other")), new[] { _experiment },
                    Unit(Session, uid), Unit(User, "u-" + uid));
            case ClusterCase.MissingValue:
                return CreateContext(Attrs("other", key), new[] { _experiment }, Unit(Session, uid));
            case ClusterCase.FilteredValue:
                _experiment.SwitchbackClusterValues = new[] { key + "-allowed" };
                return CreateContext(Attrs("session_store", key), new[] { _experiment }, Unit(Session, uid));
            default:
                return CreateContext(Attrs("session_store", key), new[] { _experiment }, Unit(Session, uid));
        }
    }

    [Test]
    public void ContextMatchesTheSdkSwitchbackWireRuleParityGrid([Values] ClusterCase cluster)
    {
        int eligible = 0, ineligible = 0, trafficMisses = 0;
        foreach (var anchor in new[] { Anchor, Anchor + 7 * Hour })
        foreach (var periodMs in new[] { Hour, 2 * Hour, 24 * Hour })
        {
            _experiment.TrafficSplit = new[] { 0.3, 0.7 };
            _experiment.SwitchbackStartAt = anchor;
            _experiment.SwitchbackPeriodMs = periodMs;
            for (var u = 0; u < 6; ++u)
            {
                var uid = "uid-" + u;
                foreach (var key in new[] { "s-1", "s-2", "42", "Lisboa" })
                foreach (var now in new[]
                         {
                             anchor - Hour, anchor - 1, anchor, anchor + periodMs - 1, anchor + periodMs,
                             anchor + 3 * periodMs + 5, anchor + 11 * periodMs
                         })
                {
                    _clock.Now = anchor - Hour;
                    var context = GridContext(cluster, uid, key);
                    _clock.Now = now;
                    var oracleKey = cluster == ClusterCase.Single ? key : null;
                    var (variant, oracleEligible) = SdkSwitchbackRule.Evaluate(_experiment, uid, oracleKey, now);
                    var message = $"{cluster}/{anchor}/{periodMs}/{uid}/{key}/{now}";
                    context.GetTreatment("sbc").Should().Be(variant, message);
                    context.CachedAssignmentForTest("sbc")!.Eligible.Should().Be(oracleEligible, message);
                    // Before the anchor nothing is recorded; from it, a keyed cluster records one exposure
                    // whose eligibility is the oracle's (a traffic miss included).
                    var expectedExposures = oracleKey != null && now >= anchor ? 1 : 0;
                    _captured.Should().HaveCount(expectedExposures, message);
                    if (expectedExposures == 1) _captured[0].Eligible.Should().Be(oracleEligible, message);
                    if (cluster != ClusterCase.Single) continue;
                    if (oracleEligible) ++eligible;
                    else
                    {
                        ++ineligible;
                        if (now >= anchor) ++trafficMisses;
                    }
                }
            }
        }

        if (cluster != ClusterCase.Single) return;
        eligible.Should().BeGreaterThan(100);
        ineligible.Should().BeGreaterThan(100);
        trafficMisses.Should().BePositive();
    }

    // The deliberate divergence: an anchor off the hour is unservable in the SDK (as in the
    // Collector's Registry), while the transcribed oracle would re-ceil it and draw.
    [Test]
    public void UnalignedAnchorIsUnservableUnlikeTheTranscribedOracle()
    {
        const long unaligned = Anchor + 7 * Hour + 1;
        _experiment.TrafficSplit = new[] { 0.0, 1.0 };
        _experiment.SwitchbackStartAt = unaligned;
        _experiment.SwitchbackPeriodMs = Hour;
        _clock.Now = Anchor + 8 * Hour + 5;
        SdkSwitchbackRule.PeriodIndex(_experiment, _clock.Now).Should().NotBe(SdkSwitchbackRule.NoPeriod);
        SdkSwitchbackRule.Evaluate(_experiment, "uid-0", "s-1", _clock.Now).Item2.Should().BeTrue();
        var context = GridContext(ClusterCase.Single, "uid-0", "s-1");
        context.GetTreatment("sbc").Should().Be(0);
        context.CachedAssignmentForTest("sbc")!.Eligible.Should().BeFalse();
        _captured.Should().BeEmpty();
    }
}
