using System.Text;
using System.Text.Json;
using ABSmartly.Models;
using ABSmartly.Services;
using ABSmartly.Services.Json;
using ABSmartly.Time;

namespace ABSmartly.Sdk.Tests.Switchback;

// The switchback payload fields on Experiment: decoding (the cluster map in its served order),
// forward compatibility, lenient typing, model equality, and the unchanged publish bytes for
// payloads without switchbacks.
[TestFixture]
public class SwitchbackPayloadTests
{
    private static string Resource(string name)
    {
        using var stream = typeof(SwitchbackPayloadTests).Assembly
            .GetManifestResourceStream("ABSmartly.Sdk.Tests.Resources." + name)!;
        return new StreamReader(stream).ReadToEnd();
    }

    private static ContextData Deserialize(string json) =>
        new DefaultContextDataDeserializer().Deserialize(new MemoryStream(Encoding.UTF8.GetBytes(json)));

    private static Experiment DeserializeExperiment(string fields) =>
        Deserialize("{\"experiments\":[{\"id\":1,\"name\":\"e\",\"unitType\":\"store_id\",\"split\":[0.5,0.5]," +
                    "\"trafficSplit\":[0,1],\"variants\":[{\"name\":\"A\"},{\"name\":\"B\"}]" + fields + "}]}")
            .Experiments.Single();

    // java-sdk DefaultContextDataDeserializerTest: every R33 field, with the abandoned Phase 1E
    // fields and an unknown nested field ignored.
    [Test]
    public void DeserializesTheSwitchbackFields()
    {
        var data = Deserialize(Resource("switchback_context.json"));
        var sb = data.Experiments[0];
        sb.RandomizationType.Should().Be("switchback");
        sb.SwitchbackClusters.Should().Equal(new KeyValuePair<string, string>("store_id", "store/key"),
            new KeyValuePair<string, string>("device_id", "geo/city"));
        sb.SwitchbackClusterValues.Should().Equal("porto", "lisbon");
        sb.SwitchbackStartAt.Should().Be(1767225600000L);
        sb.SwitchbackPeriodMs.Should().Be(3600000L);
        sb.SwitchbackCarryoverDurationMs.Should().Be(900000L);
        sb.SwitchbackBurnInDurationMs.Should().Be(600000L);
        sb.SwitchbackBurnOutDurationMs.Should().Be(0L);
        sb.SwitchbackDesignType.Should().Be("balanced_block");
        sb.SwitchbackEstimand.Should().Be("global_average_treatment_effect");

        var unit = data.Experiments[1];
        unit.RandomizationType.Should().Be("unit");
        unit.SwitchbackStartAt.Should().BeNull();
        unit.SwitchbackClusters.Should().BeNull();
    }

    [Test]
    public void PayloadWithoutSwitchbackFieldsLeavesThemNull()
    {
        var data = Deserialize(Resource("context.json"));
        data.Experiments.Should().OnlyContain(e =>
            e.RandomizationType == null && e.SwitchbackClusters == null && e.SwitchbackClusterValues == null &&
            e.SwitchbackStartAt == null && e.SwitchbackPeriodMs == null && e.SwitchbackDesignType == null);
    }

    // The cluster map keeps the served key order, which is not alphabetical, nor the order a
    // Dictionary of these keys would enumerate in; a repeated key keeps its first position and takes
    // the last value, as a LinkedHashMap.
    [Test]
    public void ClusterMapKeepsTheServedOrder()
    {
        var experiment = DeserializeExperiment(
            ",\"switchbackClusters\":{\"zeta_id\":\"z\",\"alpha_id\":\"a\",\"mid_id\":\"m\",\"beta_id\":\"b\",\"alpha_id\":\"a2\"}");
        experiment.SwitchbackClusters!.Select(c => c.Key).Should().Equal("zeta_id", "alpha_id", "mid_id", "beta_id");
        experiment.SwitchbackClusters!.Select(c => c.Value).Should().Equal("z", "a2", "m", "b");

        var json = JsonSerializer.Serialize(experiment, JsonOptionsProvider.Default.SerializerOptions);
        json.Should().Contain("\"switchbackClusters\":{\"zeta_id\":\"z\",\"alpha_id\":\"a2\",\"mid_id\":\"m\",\"beta_id\":\"b\"}");
        DeserializeExperiment(",\"switchbackClusters\":" +
                              JsonDocument.Parse(json).RootElement.GetProperty("switchbackClusters").GetRawText())
            .SwitchbackClusters.Should().Equal(experiment.SwitchbackClusters);
    }

    // A wrongly typed switchback field loses only that field: the context still decodes. A path
    // that is not a string keeps its source as a null entry, so the map is unreadable (legacy path)
    // rather than silently skipping a source.
    // A 64-bit field takes the JSON number's exact decimal value: a fraction too small for binary64
    // to keep is still a fraction (read as null, so the schedule is unservable), while an integral
    // number in any notation (3.6e6, 3600000.0, 36e5) is read as that integer.
    [TestCase("3600000.0000000001", null)]
    [TestCase("3600000.5", null)]
    [TestCase("1e-1", null)]
    [TestCase("3.6000000000000001e6", null)]
    [TestCase("3.6e6", 3_600_000L)]
    [TestCase("3600000.0", 3_600_000L)]
    [TestCase("36e5", 3_600_000L)]
    [TestCase("3600000", 3_600_000L)]
    [TestCase("-1", -1L)]
    [TestCase("9223372036854775807", long.MaxValue)]
    [TestCase("9223372036854775808", null)]
    [TestCase("1e19", null)]
    public void SixtyFourBitFieldsReadTheExactDecimalValue(string number, long? expected)
    {
        DeserializeExperiment(",\"switchbackPeriodMs\":" + number).SwitchbackPeriodMs.Should().Be(expected);
    }

    [Test]
    public void WronglyTypedFieldsAreReadAsNull()
    {
        var experiment = DeserializeExperiment(
            ",\"randomizationType\":{\"x\":1},\"switchbackClusters\":[\"store_id\"],\"switchbackClusterValues\":\"porto\"," +
            "\"switchbackStartAt\":\"1767225600000\",\"switchbackPeriodMs\":1.5,\"switchbackCarryoverDurationMs\":[1]," +
            "\"switchbackBurnInDurationMs\":true,\"switchbackBurnOutDurationMs\":1e300," +
            "\"switchbackDesignType\":7,\"switchbackEstimand\":null");
        (experiment.RandomizationType, experiment.SwitchbackClusters, experiment.SwitchbackClusterValues)
            .Should().Be(((string?)null, (KeyValuePair<string, string>[]?)null, (string[]?)null));
        (experiment.SwitchbackStartAt, experiment.SwitchbackPeriodMs, experiment.SwitchbackCarryoverDurationMs,
                experiment.SwitchbackBurnInDurationMs, experiment.SwitchbackBurnOutDurationMs)
            .Should().Be(((long?)null, (long?)null, (long?)null, (long?)null, (long?)null));
        (experiment.SwitchbackDesignType, experiment.SwitchbackEstimand).Should().Be(((string?)null, (string?)null));

        var mixed = DeserializeExperiment(
            ",\"switchbackClusters\":{\"store_id\":\"store\",\"user_id\":7}," +
            "\"switchbackClusterValues\":[\"a\",1,null,\"b\"],\"switchbackStartAt\":3.6e6,\"switchbackPeriodMs\":-1");
        mixed.SwitchbackClusters.Should().Equal(new KeyValuePair<string, string>("store_id", "store"),
            new KeyValuePair<string, string>("user_id", null!));
        // As Jackson reads a String[].
        mixed.SwitchbackClusterValues.Should().Equal("a", "1", null, "b");
        mixed.SwitchbackStartAt.Should().Be(3_600_000L);
        mixed.SwitchbackPeriodMs.Should().Be(-1);

        DeserializeExperiment(",\"switchbackStartAt\":9223372036854775807").SwitchbackStartAt.Should().Be(long.MaxValue);
        DeserializeExperiment(",\"switchbackStartAt\":-9223372036854775808").SwitchbackStartAt.Should().Be(long.MinValue);
        DeserializeExperiment(",\"switchbackStartAt\":9223372036854775808").SwitchbackStartAt.Should().BeNull();
        DeserializeExperiment(",\"switchbackStartAt\":9.3e18").SwitchbackStartAt.Should().BeNull();
    }

    // A non-string allow-list still filters: integers are their decimal text, and an element Jackson
    // would reject keeps the list non-empty (it is never the allow-all empty list).
    [TestCase("[7,8]", new[] { "7", "8" })]
    [TestCase("[1.5,true,false]", new[] { "1.5", "true", "false" })]
    [TestCase("[{\"a\":1}]", new string?[] { null })]
    [TestCase("[[\"x\"]]", new string?[] { null })]
    public void NonStringAllowListElementsAreReadAsJacksonReadsThem(string json, string?[] expected)
    {
        DeserializeExperiment(",\"switchbackClusterValues\":" + json).SwitchbackClusterValues.Should().Equal(expected);
    }

    // A numeric allow-list filters cluster keys by their decimal text; a list of null or rejected
    // elements filters every key (it is never the allow-all empty list).
    [TestCase("[7,8]", 42, 0)]
    [TestCase("[7,8]", 7, 1)]
    [TestCase("[42]", 42, 1)]
    [TestCase("[null]", 42, 0)]
    [TestCase("[{}]", 42, 0)]
    public void NumericAllowListFiltersIntegerClusterKeys(string allowList, int value, int exposures)
    {
        var data = Deserialize(
            "{\"experiments\":[{\"id\":1,\"name\":\"exp_sb\",\"unitType\":\"store_id\",\"iteration\":1," +
            "\"seedHi\":-5,\"seedLo\":-7,\"split\":[0.5,0.5],\"trafficSeedHi\":1,\"trafficSeedLo\":2," +
            "\"trafficSplit\":[0,1],\"variants\":[{\"name\":\"A\"},{\"name\":\"B\"}]," +
            "\"randomizationType\":\"switchback\",\"switchbackClusters\":{\"store_id\":\"store_number\"}," +
            "\"switchbackClusterValues\":" + allowList + ",\"switchbackStartAt\":1767225600000,\"switchbackPeriodMs\":3600000}]}");
        var context = Context.CreateWithData(
            new ContextConfig { PublishDelay = TimeSpan.FromMinutes(10) }.SetUnit("store_id", "u1")
                .SetAttribute("store_number", value),
            data, Clock.Fixed(1_767_225_600_000L + 10), Mock.Of<IContextDataProvider>(), Mock.Of<IContextPublisher>(),
            null, new DefaultVariableParser(), new AudienceMatcher(new DefaultAudienceDeserializer()), null);
        context.GetTreatment("exp_sb");
        context.PendingCount.Should().Be(exposures);
    }

    // Forward compatibility: unknown scalar, object and array keys inside a switchback experiment
    // (and the abandoned Phase 1E fields) are ignored, and both experiments still assign.
    [Test]
    public void UnknownKeysInsideSwitchbackExperimentsAreIgnored()
    {
        var data = Deserialize(
            "{\"futureRoot\":{\"switchbacksV2\":[1]},\"experiments\":[" +
            "{\"id\":1,\"name\":\"exp_sb\",\"unitType\":\"store_id\",\"iteration\":1,\"seedHi\":-5,\"seedLo\":-7," +
            "\"split\":[0.5,0.5],\"trafficSeedHi\":1,\"trafficSeedLo\":2,\"trafficSplit\":[0,1],\"fullOnVariant\":0," +
            "\"variants\":[{\"name\":\"A\",\"config\":null},{\"name\":\"B\",\"config\":null}]," +
            "\"randomizationType\":\"switchback\",\"switchbackClusters\":{\"store_id\":\"store_key\"}," +
            "\"switchbackStartAt\":1767225600000,\"switchbackPeriodMs\":3600000," +
            "\"switchbackClusterUnitType\":\"store_id\",\"switchbackClusterCount\":50,\"switchbackClusterAttribute\":\"x\"," +
            "\"switchbackFutureField\":{\"nested\":[1,{\"a\":null}]},\"switchbackSchedule\":[[1,2],[3]],\"futureFlag\":true}," +
            "{\"id\":2,\"name\":\"exp_unit\",\"unitType\":\"store_id\",\"iteration\":1,\"seedHi\":100,\"seedLo\":200," +
            "\"split\":[0.5,0.5],\"trafficSeedHi\":1,\"trafficSeedLo\":2,\"trafficSplit\":[0,1],\"fullOnVariant\":0," +
            "\"variants\":[{\"name\":\"A\",\"config\":null},{\"name\":\"B\",\"config\":null}],\"randomizationType\":\"unit\"," +
            "\"switchbackFutureField\":7}]}");

        data.Should().NotBeNull();
        var context = Context.CreateWithData(
            new ContextConfig { PublishDelay = TimeSpan.FromMinutes(10) }.SetUnit("store_id", "u1")
                .SetAttribute("store_key", "store-42"),
            data, Clock.Fixed(1_767_225_600_000L + 2 * 3_600_000L), Mock.Of<IContextDataProvider>(),
            Mock.Of<IContextPublisher>(), null, new DefaultVariableParser(),
            new AudienceMatcher(new DefaultAudienceDeserializer()), null);
        context.GetTreatment("exp_sb").Should().Be(ABSmartly.Internal.SwitchbackAssigner.Assign("store-42", 2,
            ((long)-5 << 32) | (-7 & 0xffffffffL), 2));
        context.PendingCount.Should().Be(1);
        context.GetTreatment("exp_unit");
        context.PendingCount.Should().Be(2);
    }

    private static Experiment Switchback() => new()
    {
        Id = 1,
        Name = "exp",
        UnitType = "store_id",
        RandomizationType = "switchback",
        SwitchbackClusters = new[]
        {
            new KeyValuePair<string, string>("store_id", "store/key"), new KeyValuePair<string, string>("device_id", "geo/city")
        },
        SwitchbackClusterValues = new[] { "porto", "lisbon" },
        SwitchbackStartAt = 1767225600000L,
        SwitchbackPeriodMs = 3600000L,
        SwitchbackCarryoverDurationMs = 900000L,
        SwitchbackBurnInDurationMs = 600000L,
        SwitchbackBurnOutDurationMs = 0L,
        SwitchbackDesignType = "balanced_block",
        SwitchbackEstimand = "global_average_treatment_effect"
    };

    // java-sdk ExperimentSwitchbackFieldsTest: Equals and GetHashCode cover every switchback field,
    // the cluster map by order.
    [Test]
    public void EqualsAndHashCodeCoverEverySwitchbackField()
    {
        Switchback().Should().Be(Switchback());
        Switchback().GetHashCode().Should().Be(Switchback().GetHashCode());

        var mutations = new Action<Experiment>[]
        {
            e => e.RandomizationType = "unit",
            e => e.SwitchbackClusters = e.SwitchbackClusters!.Reverse().ToArray(),
            e => e.SwitchbackClusters = new[] { new KeyValuePair<string, string>("store_id", "store/key") },
            e => e.SwitchbackClusters = new[]
            {
                new KeyValuePair<string, string>("store_id", "other"), new KeyValuePair<string, string>("device_id", "geo/city")
            },
            e => e.SwitchbackClusters = null,
            e => e.SwitchbackClusterValues = new[] { "lisbon", "porto" },
            e => e.SwitchbackClusterValues = null,
            e => e.SwitchbackStartAt = 0L,
            e => e.SwitchbackPeriodMs = 1L,
            e => e.SwitchbackCarryoverDurationMs = 1L,
            e => e.SwitchbackBurnInDurationMs = 1L,
            e => e.SwitchbackBurnOutDurationMs = 1L,
            e => e.SwitchbackDesignType = "other",
            e => e.SwitchbackEstimand = "other"
        };
        foreach (var mutation in mutations)
        {
            var changed = Switchback();
            mutation(changed);
            changed.Should().NotBe(Switchback());
            Switchback().Should().NotBe(changed);
            changed.GetHashCode().Should().NotBe(Switchback().GetHashCode());
        }
    }

    [Test]
    public void ToStringIncludesSwitchbackFieldsOnlyWhenSet()
    {
        var s = Switchback().ToString();
        s.Should().Contain("randomizationType=switchback")
            .And.Contain("switchbackClusters={store_id=store/key,device_id=geo/city}")
            .And.Contain("switchbackClusterValues=porto,lisbon")
            .And.Contain("switchbackStartAt=1767225600000")
            .And.Contain("switchbackPeriodMs=3600000")
            .And.Contain("switchbackCarryoverDurationMs=900000")
            .And.Contain("switchbackBurnInDurationMs=600000")
            .And.Contain("switchbackBurnOutDurationMs=0")
            .And.Contain("switchbackDesignType=balanced_block")
            .And.Contain("switchbackEstimand=global_average_treatment_effect");
        new Experiment { Id = 1, Name = "e" }.ToString().Should().NotContain("switchback").And.NotContain("randomization");
    }

    // No cost without switchbacks: unit experiments carry no switchback state and no epoch is kept.
    // (HoldoutPayloadTests.PublishBytesWithoutHoldoutsAreUnchanged pins the unchanged publish bytes.)
    [Test]
    public void ExperimentsWithoutSwitchbacksAllocateNoSwitchbackState()
    {
        var data = Deserialize(Resource("context.json"));
        var context = Context.CreateWithData(
            new ContextConfig { PublishDelay = TimeSpan.FromMinutes(10) }.SetUnit("session_id", "u1").SetUnit("user_id", "u2"),
            data, Clock.Fixed(1_620_000_000_000L), Mock.Of<IContextDataProvider>(), Mock.Of<IContextPublisher>(), null,
            new DefaultVariableParser(), new AudienceMatcher(new DefaultAudienceDeserializer()), null);
        foreach (var e in data.Experiments)
        {
            context.GetTreatment(e.Name);
            context.CachedAssignmentForTest(e.Name)!.Switchback.Should().BeNull();
        }

        var epochs = (System.Collections.ICollection)typeof(Context)
            .GetField("_switchbackEpochs", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
            .GetValue(context)!;
        epochs.Count.Should().Be(0);
    }
}
