using System.Text;
using ABSmartly.Models;
using ABSmartly.Services;
using ABSmartly.Time;

namespace ABSmartly.Sdk.Tests;

// The holdout payload fields (ContextData.Holdouts, Experiment.HoldoutIds/HoldoutType): decoding,
// forward compatibility, model equality, and the unchanged wire output for payloads without
// holdouts.
[TestFixture]
public class HoldoutPayloadTests
{
    private const string Uid = "e791e240fcd3df7d238cfc285f475e8152fcc0ec";

    private string Resource(string name)
    {
        using var stream = GetType().Assembly.GetManifestResourceStream("ABSmartly.Sdk.Tests.Resources." + name);
        return new StreamReader(stream!).ReadToEnd();
    }

    private static ContextData Deserialize(string json) =>
        new DefaultContextDataDeserializer().Deserialize(new MemoryStream(Encoding.UTF8.GetBytes(json)));

    private static Context CreateContext(ContextData data, IContextPublisher publisher, string uid = Uid) =>
        Context.CreateWithData(
            new ContextConfig { PublishDelay = TimeSpan.FromMinutes(10) }.SetUnit("session_id", uid).SetUnit("user_id", uid),
            data, Clock.Fixed(1_620_000_000_000L), Mock.Of<IContextDataProvider>(), publisher, null,
            new DefaultVariableParser(), new AudienceMatcher(new DefaultAudienceDeserializer()), null,
            new Dictionary<string, object> { ["application"] = "website", ["environment"] = "production" });

    private static (IContextPublisher Publisher, List<PublishEvent> Published) Publisher()
    {
        var published = new List<PublishEvent>();
        var publisher = new Mock<IContextPublisher>();
        publisher.Setup(x => x.PublishAsync(It.IsAny<IContext>(), It.IsAny<PublishEvent>()))
            .Callback<IContext, PublishEvent>((_, e) => published.Add(e)).Returns(Task.CompletedTask);
        return (publisher.Object, published);
    }

    [Test]
    public void DeserializesHoldoutsAndHoldoutIds()
    {
        var data = Deserialize(Resource("holdouts_context.json"));

        data.Experiments.Single().HoldoutIds.Should().Equal(11);
        data.Experiments.Single().HoldoutType.Should().BeNull();
        data.Holdouts.Should().HaveCount(2);

        var full = data.Holdouts![0];
        (full.Id, full.Name, full.UnitType, full.Iteration, full.SeedHi, full.SeedLo, full.HoldoutType)
            .Should().Be((11, "holdout_a", "session_id", 1, 13, 111, "full"));
        full.Split.Should().Equal(0.1, 0.9);

        // The slim shape the collector serves: no holdoutType, variants or traffic split.
        var slim = data.Holdouts[1];
        (slim.Id, slim.Name, slim.HoldoutType, slim.Variants, slim.TrafficSplit)
            .Should().Be((12, "holdout_b", (string?)null, (ExperimentVariant[]?)null, (double[]?)null));
        slim.Split.Should().Equal(0.05, 0.05, 0.9);
    }

    [Test]
    public void PayloadWithoutHoldoutFieldsLeavesThemNull()
    {
        var data = Deserialize(Resource("context.json"));

        data.Holdouts.Should().BeNull();
        data.Experiments.Should().OnlyContain(e => e.HoldoutIds == null && e.HoldoutType == null);
    }

    // Forward compatibility: unknown scalar, object and array keys inside holdout objects (and
    // future holdout fields at the root and on experiments) are ignored, malformed holdoutIds
    // entries are skipped, and both experiments still assign; the covered one is still held out.
    [Test]
    public void UnknownKeysInsideHoldoutObjectsAreIgnored()
    {
        const string json =
            "{" +
            "  \"futureRoot\": {\"holdoutsV2\": [1]}," +
            "  \"experiments\": [" +
            "    {\"id\":1,\"name\":\"exp_covered\",\"unitType\":\"session_id\",\"iteration\":1,\"seedHi\":100,\"seedLo\":200," +
            "     \"split\":[0.5,0.5],\"trafficSeedHi\":1,\"trafficSeedLo\":2,\"trafficSplit\":[0,1],\"fullOnVariant\":0," +
            "     \"variants\":[{\"name\":\"A\",\"config\":null},{\"name\":\"B\",\"config\":null}]," +
            "     \"holdoutIds\":[11.0,\"12\",null,{\"id\":13},[14],1e100,99]," +
            "     \"holdoutPriority\":{\"x\":[1]},\"futureHoldoutField\":[{\"a\":1}]}," +
            "    {\"id\":2,\"name\":\"exp_free\",\"unitType\":\"session_id\",\"iteration\":1,\"seedHi\":100,\"seedLo\":200," +
            "     \"split\":[0.5,0.5],\"trafficSeedHi\":1,\"trafficSeedLo\":2,\"trafficSplit\":[0,1],\"fullOnVariant\":0," +
            "     \"variants\":[{\"name\":\"A\",\"config\":null},{\"name\":\"B\",\"config\":null}],\"holdoutIds\":\"oops\"}" +
            "  ]," +
            "  \"holdouts\": [" +
            "    {\"id\":11,\"name\":\"holdout_a\",\"unitType\":\"session_id\",\"iteration\":1,\"seedHi\":13,\"seedLo\":111," +
            "     \"split\":[0.1,0.9],\"futureScalar\":7,\"futureObject\":{\"nested\":{\"deep\":[1,2]}}," +
            "     \"futureArray\":[{\"k\":\"v\"},3,null],\"holdoutType\":\"full\",\"schedule\":{\"start\":0}}," +
            "    {\"id\":12,\"name\":\"holdout_without_split\",\"iteration\":1,\"futureFlag\":true}" +
            "  ]," +
            "  \"futureArrayAtRoot\": [1, {\"a\": []}]" +
            "}";

        var data = Deserialize(json);

        data.Should().NotBeNull();
        data.Experiments[0].HoldoutIds.Should().Equal(11, 99);
        data.Experiments[1].HoldoutIds.Should().BeNull();
        data.Holdouts.Should().HaveCount(2);

        var (publisher, published) = Publisher();
        var context = CreateContext(data, publisher);
        context.GetTreatment("exp_covered").Should().Be(0);
        context.GetTreatment("exp_free").Should().Be(1);
        context.Publish();
        published.Single().Exposures.Select(e => (e.Name, e.Variant))
            .Should().Equal(("holdout_a", 0), ("exp_free", 1));
    }

    // A wrongly typed holdout field loses only that field or holdout: the context still decodes
    // and the unrelated experiment still assigns (1.3.0 ignored these fields altogether).
    [TestCase("\"holdouts\":{\"id\":11}", "\"holdoutType\":\"full\"", 0)]
    [TestCase("\"holdouts\":7", "\"holdoutType\":\"full\"", 0)]
    [TestCase("\"holdouts\":\"oops\"", "\"holdoutType\":\"full\"", 0)]
    [TestCase("\"holdouts\":[{\"id\":\"11\",\"name\":\"h\",\"split\":[0.1,0.9]},7,\"x\",null]", "\"holdoutType\":\"full\"", 0)]
    [TestCase("\"holdouts\":[{\"id\":11,\"name\":\"h\",\"seedHi\":13,\"seedLo\":111,\"split\":[0.1,0.9],\"holdoutType\":3}]", "\"holdoutType\":{\"a\":1}", 1)]
    // A null, decimal (11.0) or out-of-range id/iteration/seedHi/seedLo drops that holdout, so its
    // experiments are assigned normally (JS keeps it; the backend never sends these).
    [TestCase("\"holdouts\":[{\"id\":11,\"name\":\"h\",\"iteration\":1,\"seedHi\":null,\"seedLo\":111,\"split\":[0.1,0.9]}]", "\"holdoutType\":\"full\"", 0)]
    [TestCase("\"holdouts\":[{\"id\":11,\"name\":\"h\",\"iteration\":1,\"seedHi\":13,\"seedLo\":null,\"split\":[0.1,0.9]}]", "\"holdoutType\":\"full\"", 0)]
    [TestCase("\"holdouts\":[{\"id\":11,\"name\":\"h\",\"iteration\":null,\"seedHi\":13,\"seedLo\":111,\"split\":[0.1,0.9]}]", "\"holdoutType\":\"full\"", 0)]
    [TestCase("\"holdouts\":[{\"id\":null,\"name\":\"h\",\"iteration\":1,\"seedHi\":13,\"seedLo\":111,\"split\":[0.1,0.9]}]", "\"holdoutType\":\"full\"", 0)]
    [TestCase("\"holdouts\":[{\"id\":11.0,\"name\":\"h\",\"iteration\":1,\"seedHi\":13,\"seedLo\":111,\"split\":[0.1,0.9]}]", "\"holdoutType\":\"full\"", 0)]
    [TestCase("\"holdouts\":[{\"id\":11,\"name\":\"h\",\"iteration\":1,\"seedHi\":1099511627776,\"seedLo\":111,\"split\":[0.1,0.9]}]", "\"holdoutType\":\"full\"", 0)]
    public void WronglyTypedHoldoutFieldsDoNotFailTheContext(string holdouts, string holdoutType, int validHoldouts)
    {
        var json = "{" + holdouts + ",\"experiments\":[" +
                   "{\"id\":1,\"name\":\"exp_covered\",\"unitType\":\"session_id\",\"iteration\":1,\"seedHi\":100," +
                   "\"seedLo\":200,\"split\":[0.5,0.5],\"trafficSeedHi\":1,\"trafficSeedLo\":2,\"trafficSplit\":[0,1]," +
                   "\"fullOnVariant\":0,\"variants\":[{\"name\":\"A\"},{\"name\":\"B\"}],\"holdoutIds\":[11]," +
                   holdoutType + "}," +
                   "{\"id\":2,\"name\":\"exp_free\",\"unitType\":\"session_id\",\"iteration\":1,\"seedHi\":100," +
                   "\"seedLo\":200,\"split\":[0.5,0.5],\"trafficSeedHi\":1,\"trafficSeedLo\":2,\"trafficSplit\":[0,1]," +
                   "\"fullOnVariant\":0,\"variants\":[{\"name\":\"A\"},{\"name\":\"B\"}]}]}";

        var data = Deserialize(json);

        data.Should().NotBeNull();
        (data.Holdouts?.Length ?? 0).Should().Be(validHoldouts);
        data.Experiments.Should().HaveCount(2);
        var (publisher, _) = Publisher();
        var context = CreateContext(data, publisher);
        context.GetTreatment("exp_free").Should().Be(1);
        // Held out (variant 0) only when the holdout decoded; otherwise assigned normally.
        context.GetTreatment("exp_covered").Should().Be(validHoldouts == 1 ? 0 : 1);
    }

    [Test]
    public void HoldoutIdsSerializeBackAsAnArray()
    {
        var experiment = new Experiment { Id = 1, HoldoutIds = new[] { 11, 12 } };
        var json = System.Text.Json.JsonSerializer.Serialize(experiment,
            ABSmartly.Services.Json.JsonOptionsProvider.Default.SerializerOptions);

        json.Should().Contain("\"holdoutIds\":[11,12]").And.NotContain("holdoutType");
    }

    [Test]
    public void ContextDataEqualityAndHashCodeIncludeHoldouts()
    {
        Experiment Holdout(int id) => new() { Id = id, Name = "holdout_" + id, Split = new[] { 0.1, 0.9 } };
        var experiments = new[] { new Experiment { Id = 1, Name = "exp", HoldoutIds = new[] { 11 } } };

        var a = new ContextData(experiments, new[] { Holdout(11) });
        var b = new ContextData(experiments, new[] { Holdout(11) });
        var other = new ContextData(experiments, new[] { Holdout(22) });

        a.Should().Be(b);
        a.GetHashCode().Should().Be(b.GetHashCode());
        a.Should().NotBe(other);
        a.GetHashCode().Should().NotBe(other.GetHashCode());
        a.Should().NotBe(new ContextData(experiments));
        a.ToString().Should().Contain("holdouts=");

        // The 1.3.0 constructor is kept and leaves Holdouts null; output without holdouts is
        // unchanged.
        var legacy = new ContextData(experiments);
        legacy.Holdouts.Should().BeNull();
        legacy.ToString().Should().NotContain("holdouts");
        legacy.GetHashCode().Should().Be(experiments.GetHashCode());
    }

    [Test]
    public void ExperimentEqualityAndHashCodeIncludeHoldoutFields()
    {
        Experiment Make(int[]? ids, string? type) => new() { Id = 1, Name = "e", HoldoutIds = ids, HoldoutType = type };

        Make(new[] { 11 }, "full").Should().Be(Make(new[] { 11 }, "full"));
        Make(new[] { 11 }, "full").GetHashCode().Should().Be(Make(new[] { 11 }, "full").GetHashCode());
        Make(new[] { 11 }, null).Should().NotBe(Make(new[] { 12 }, null));
        Make(new[] { 11 }, null).GetHashCode().Should().NotBe(Make(new[] { 12 }, null).GetHashCode());
        Make(null, "full").Should().NotBe(Make(null, "all_full_on"));
        Make(null, null).Should().NotBe(Make(new[] { 11 }, null));
        Make(new[] { 11 }, "full").ToString().Should().Contain("holdoutIds=11").And.Contain("holdoutType=full");
        Make(null, null).ToString().Should().NotContain("holdout");
    }

    // The publish body of a payload without holdouts is byte-identical to main before holdouts
    // (the golden file was recorded on origin/main fd564d2 with this exact probe).
    [Test]
    public void PublishBytesWithoutHoldoutsAreUnchanged()
    {
        var data = Deserialize(Resource("context.json"));
        var (publisher, published) = Publisher();
        var sb = new StringBuilder();
        foreach (var uid in new[] { Uid, "123456789", "façade", "unicode_emoji_🚀_0", "0", "-1" })
        {
            var context = CreateContext(data, publisher, uid);
            context.SetAttribute("age", 30);
            foreach (var e in data.Experiments) sb.Append(e.Name).Append('=').Append(context.PeekTreatment(e.Name)).Append(';');
            foreach (var key in new[] { "banner.border", "banner.size", "button.color", "card.width", "submit.color", "submit.shape", "show-modal" })
                sb.Append(key).Append('=').Append(context.GetVariableValue(key, "d")).Append(';');
            foreach (var e in data.Experiments) context.GetTreatment(e.Name);
            context.Track("goal", new Dictionary<string, object> { ["amount"] = 1 });
            context.Publish();
            sb.Append(Encoding.UTF8.GetString(new DefaultContextEventSerializer().Serialize(published[^1]))).Append('\n');
        }

        // Records are LF-separated; a CRLF checkout of the fixture must not fail the comparison.
        sb.ToString().Should().Be(Resource("publish_without_holdouts.golden").Replace("\r\n", "\n"));
    }
}
