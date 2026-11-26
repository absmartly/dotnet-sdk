using System.Reflection;
using ABSmartly.Internal.Hashing;
using ABSmartly.Models;
using ABSmartly.Services;
using ABSmartly.Time;

namespace ABSmartly.Sdk.Tests.Switchback;

// A clock the tests move by hand. When NextRead is set, the next read returns it once, then reads
// fall back to Now.
internal sealed class MutableClock : Clock
{
    public long Now;
    public long? NextRead;
    public Exception? NextReadThrows;

    public MutableClock(long now)
    {
        Now = now;
    }

    public override long Millis()
    {
        if (NextReadThrows is { } error)
        {
            NextReadThrows = null;
            throw error;
        }

        if (NextRead is { } next)
        {
            NextRead = null;
            return next;
        }

        return Now;
    }
}

// Shared setup for the switchback context tests (java-sdk ContextSwitchbackTest and
// SwitchbackTestSupport): a store unit, its cluster attribute set at context creation, and a
// publisher capturing every published event.
public abstract class SwitchbackTestBase
{
    protected const string UnitType = "store_id";
    protected const string Uid = "e791e240fcd3df7d238cfc285f475e8152fcc0ec";
    // Md5.Hash(Uid): the MD5/base64url string the Collector's UnitHasher also produces.
    protected const string UidHash = "pAE3a1i5Drs5mKRNq56adA";

    protected const long Hour = 3_600_000L;
    protected const long Day = 24 * Hour;
    // 2026-01-01T00:00:00Z, hour aligned.
    protected const long Anchor = 1_767_225_600_000L;

    protected const int SeedHi = -5;
    protected const int SeedLo = -7;
    protected static readonly long Seed = ((long)SeedHi << 32) | (SeedLo & 0xffffffffL);

    protected const string ClusterPath = "store_key";
    protected const string ClusterKey = "store-42";
    // Periods 0..11 of the raw cluster key "store-42" under seed (SeedHi, SeedLo), from java-sdk
    // (an independent Python reference that reproduces every golden vector).
    protected static readonly int[] Expected2Arms = { 0, 1, 0, 1, 1, 0, 1, 0, 1, 0, 0, 1 };
    protected static readonly int[] Expected3Arms = { 0, 2, 1, 2, 1, 0, 1, 2, 0, 1, 0, 2 };
    protected static readonly int[] Expected10Arms = { 8, 3, 7, 4, 2, 0, 5, 1, 9, 6, 3, 7 };
    // Same reference fed UidHash and the raw Uid: what keying by the unit instead of the cluster
    // value would serve.
    protected static readonly int[] UidHash3Arms = { 1, 2, 0, 2, 1, 0, 2, 0, 1, 1, 2, 0 };
    protected static readonly int[] Uid3Arms = { 2, 0, 1, 0, 1, 2, 1, 2, 0, 0, 1, 2 };

    protected const string StrictGoldAudience = "{\"filter\":[{\"eq\":[{\"var\":\"tier\"},{\"value\":\"gold\"}]}]}";
    protected const string AssignVariant1 = "{\"rules\":[{\"type\":\"assign\",\"environments\":[],\"variant\":1}]}";
    protected const string AssignVariant2 = "{\"rules\":[{\"type\":\"assign\",\"environments\":[],\"variant\":2}]}";

    internal MutableClock Clock = null!;
    protected Mock<IContextDataProvider> DataProvider = null!;
    protected List<(IContext Context, PublishEvent Event)> Published = null!;
    protected IContextPublisher Publisher = null!;

    [SetUp]
    public void SetUpSwitchback()
    {
        Clock = new MutableClock(Anchor);
        DataProvider = new Mock<IContextDataProvider>();
        Published = new List<(IContext, PublishEvent)>();
        var publisher = new Mock<IContextPublisher>();
        publisher.Setup(x => x.PublishAsync(It.IsAny<IContext>(), It.IsAny<PublishEvent>()))
            .Callback<IContext, PublishEvent>((c, e) => Published.Add((c, e)))
            .Returns(Task.CompletedTask);
        Publisher = publisher.Object;
    }

    protected static KeyValuePair<string, string>[] Clusters(params string[] typeAndPath)
    {
        var clusters = new KeyValuePair<string, string>[typeAndPath.Length / 2];
        for (var i = 0; i < clusters.Length; i++)
            clusters[i] = new KeyValuePair<string, string>(typeAndPath[2 * i], typeAndPath[2 * i + 1]);
        return clusters;
    }

    protected static Experiment NewSwitchback(int id, string name, int armCount, long periodMs)
    {
        var split = new double[armCount];
        var variants = new ExperimentVariant[armCount];
        for (var i = 0; i < armCount; ++i)
        {
            split[i] = 1.0 / armCount;
            variants[i] = new ExperimentVariant { Name = ((char)('A' + i)).ToString(), Config = "{\"sb.arm\":" + i + "}" };
        }

        return new Experiment
        {
            Id = id,
            Name = name,
            UnitType = UnitType,
            Iteration = 1,
            SeedHi = SeedHi,
            SeedLo = SeedLo,
            Split = split,
            Variants = variants,
            TrafficSeedHi = 1,
            TrafficSeedLo = 2,
            TrafficSplit = new[] { 0.0, 1.0 },
            Applications = new[] { new ExperimentApplication { Name = "website" } },
            RandomizationType = "switchback",
            SwitchbackClusters = Clusters(UnitType, ClusterPath),
            SwitchbackStartAt = Anchor,
            SwitchbackPeriodMs = periodMs,
            SwitchbackCarryoverDurationMs = 0,
            SwitchbackBurnInDurationMs = 0,
            SwitchbackBurnOutDurationMs = 0,
            SwitchbackDesignType = "balanced_block",
            SwitchbackEstimand = "global_average_treatment_effect"
        };
    }

    protected static Experiment NewSwitchback(int armCount, long periodMs) => NewSwitchback(1, "exp_sb", armCount, periodMs);

    protected static Experiment AsUnit(Experiment experiment)
    {
        experiment.RandomizationType = null;
        experiment.SwitchbackClusters = null;
        experiment.SwitchbackStartAt = null;
        experiment.SwitchbackPeriodMs = null;
        return experiment;
    }

    protected static ContextData DataOf(params Experiment[] experiments) => new(experiments);

    // Holdout 11 for Uid under seed (13, 111); the split decides whether Uid is held out.
    protected static Experiment Holdout(double[] split) => new()
    {
        Id = 11,
        Name = "holdout_a",
        UnitType = UnitType,
        Iteration = 1,
        SeedHi = 13,
        SeedLo = 111,
        Split = split,
        TrafficSplit = new[] { 0.0, 1.0 },
        Variants = Array.Empty<ExperimentVariant>()
    };

    internal Context CreateContext(Dictionary<string, string>? units, ContextData data, AudienceMatcher? matcher = null,
        bool clusterAttribute = true)
    {
        var config = new ContextConfig { PublishDelay = TimeSpan.FromMinutes(10) };
        if (units != null) config.SetUnits(units);
        if (clusterAttribute) config.SetAttribute(ClusterPath, ClusterKey);
        return Context.CreateWithData(config, data, Clock, DataProvider.Object, Publisher, null,
            new DefaultVariableParser(), matcher ?? new AudienceMatcher(new DefaultAudienceDeserializer()), null,
            new Dictionary<string, object> { ["application"] = "website", ["environment"] = "production" });
    }

    internal Context CreateContext(ContextData data) =>
        CreateContext(new Dictionary<string, string> { [UnitType] = Uid }, data);

    internal void RefreshTo(Context context, params Experiment[] experiments) => RefreshTo(context, DataOf(experiments));

    internal void RefreshTo(Context context, ContextData data)
    {
        DataProvider.Setup(x => x.GetContextDataAsync()).ReturnsAsync(data);
        context.Refresh();
    }

    internal PublishEvent PublishEvent(Context context)
    {
        var before = Published.Count;
        context.Publish();
        Published.Count.Should().Be(before + 1, "one publish");
        return Published[^1].Event;
    }

    internal Exposure[] PublishExposures(Context context)
    {
        var before = Published.Count;
        context.Publish();
        if (Published.Count == before) return Array.Empty<Exposure>();
        Published.Count.Should().Be(before + 1);
        return Published[^1].Event.Exposures ?? Array.Empty<Exposure>();
    }

    protected static Exposure NewExposure(int id, string name, int variant, long exposedAt, bool assigned, bool eligible,
        bool overridden = false, bool fullOn = false, bool custom = false, bool audienceMismatch = false,
        bool ruleOverride = false, string unit = UnitType) => new()
    {
        Id = id,
        Name = name,
        Unit = unit,
        Variant = variant,
        ExposedAt = exposedAt,
        Assigned = assigned,
        Eligible = eligible,
        Overridden = overridden,
        FullOn = fullOn,
        Custom = custom,
        AudienceMismatch = audienceMismatch,
        RuleOverride = ruleOverride
    };

    protected static Exposure RuleExposure(string name, int variant, long exposedAt) =>
        NewExposure(1, name, variant, exposedAt, false, true, ruleOverride: true);

    protected static Exposure StrictMismatchExposure(long exposedAt) =>
        NewExposure(1, "exp_sb", 0, exposedAt, false, true, audienceMismatch: true);

    internal int[] PeekSchedule(Context context, string name, long periodMs, int periods)
    {
        var result = new int[periods];
        for (var i = 0; i < periods; ++i)
        {
            Clock.Now = Anchor + i * periodMs;
            result[i] = context.PeekTreatment(name);
        }

        return result;
    }

    // Every cached assignment: those by name and the switchback period slots.
    internal static int AssignmentCacheSize(Context context) =>
        CacheCount(context, "_assignmentCache") + CacheCount(context, "_switchbackSlots");

    private static int CacheCount(Context context, string field) =>
        ((System.Collections.IDictionary)typeof(Context)
            .GetField(field, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(context)!).Count;

    protected static string Hash(string uid) => Md5.Hash(uid);
}
