using ABSmartly.Models;
using ABSmartly.Services;

namespace ABSmartly.Sdk.Tests.Services;

[TestFixture]
public class DefaultContextDataDeserializerTests
{
    private readonly DefaultContextDataDeserializer _deserializer = new();

    [Test]
    public void TestDeserializeStream()
    {
        using var jsonStream =
            GetType().Assembly.GetManifestResourceStream("ABSmartly.Sdk.Tests.Resources.context.json");

        var expected = ExpectedData();
        var actual = _deserializer.Deserialize(jsonStream);
        actual.Should().NotBeNull();
        actual.Should().BeEquivalentTo(expected);
    }

    [Test]
    public void TestDeserializeDoesNotThrow()
    {
        var act = () => _deserializer.Deserialize(null);
        act.Should().NotThrow();
    }

    private ContextData ExpectedData()
    {
        var experiment0 = new Experiment
        {
            Id = 1,
            Name = "exp_test_ab",
            UnitType = "session_id",
            Iteration = 1,
            SeedHi = 3603515,
            SeedLo = 233373850,
            Split = new[] { 0.5, 0.5 },
            TrafficSeedHi = 449867249,
            TrafficSeedLo = 455443629,
            TrafficSplit = new[] { 0.0, 1.0 },
            FullOnVariant = 0,
            Applications = new[] { new ExperimentApplication { Name = "website" } },
            Variants = new ExperimentVariant[]
            {
                new() { Name = "A", Config = null },
                new() { Name = "B", Config = "{\"banner.border\":1,\"banner.size\":\"large\"}" }
            },
            AudienceStrict = false,
            Audience = null
        };

        var experiment1 = new Experiment
        {
            Id = 2,
            Name = "exp_test_abc",
            UnitType = "session_id",
            Iteration = 1,
            SeedHi = 55006150,
            SeedLo = 47189152,
            Split = new[] { 0.34, 0.33, 0.33 },
            TrafficSeedHi = 705671872,
            TrafficSeedLo = 212903484,
            TrafficSplit = new[] { 0.0, 1.0 },
            FullOnVariant = 0,
            Applications = new[] { new ExperimentApplication { Name = "website" } },
            Variants = new ExperimentVariant[]
            {
                new() { Name = "A", Config = null },
                new() { Name = "B", Config = "{\"button.color\":\"blue\"}" },
                new() { Name = "C", Config = "{\"button.color\":\"red\"}" }
            },
            AudienceStrict = false,
            Audience = ""
        };

        var experiment2 = new Experiment
        {
            Id = 3,
            Name = "exp_test_not_eligible",
            UnitType = "user_id",
            Iteration = 1,
            SeedHi = 503266407,
            SeedLo = 144942754,
            Split = new[] { 0.34, 0.33, 0.33 },
            TrafficSeedHi = 87768905,
            TrafficSeedLo = 511357582,
            TrafficSplit = new[] { 0.99, 0.01 },
            FullOnVariant = 0,
            Applications = new[] { new ExperimentApplication { Name = "website" } },
            Variants = new ExperimentVariant[]
            {
                new() { Name = "A", Config = null },
                new() { Name = "B", Config = "{\"card.width\":\"80%\"}" },
                new() { Name = "C", Config = "{\"card.width\":\"75%\"}" }
            },
            AudienceStrict = false,
            Audience = "{}"
        };

        var experiment3 = new Experiment
        {
            Id = 4,
            Name = "exp_test_fullon",
            UnitType = "session_id",
            Iteration = 1,
            SeedHi = 856061641,
            SeedLo = 990838475,
            Split = new[] { 0.25, 0.25, 0.25, 0.25 },
            TrafficSeedHi = 360868579,
            TrafficSeedLo = 330937933,
            TrafficSplit = new[] { 0.0, 1.0 },
            FullOnVariant = 2,
            Applications = new[] { new ExperimentApplication { Name = "website" } },
            Variants = new ExperimentVariant[]
            {
                new() { Name = "A", Config = null },
                new() { Name = "B", Config = "{\"submit.color\":\"red\",\"submit.shape\":\"circle\"}" },
                new() { Name = "C", Config = "{\"submit.color\":\"blue\",\"submit.shape\":\"rect\"}" },
                new() { Name = "D", Config = "{\"submit.color\":\"green\",\"submit.shape\":\"square\"}" }
            },
            AudienceStrict = false,
            Audience = "null"
        };

        var experiments = new[]
        {
            experiment0,
            experiment1,
            experiment2,
            experiment3
        };

        var expected = new ContextData(experiments);

        return expected;
    }
}