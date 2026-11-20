using System.Text.Json;
using ABSmartly.Internal;

namespace ABSmartly.Sdk.Tests.Switchback;

// Ported from java-sdk SwitchbackAttributePathTest, plus SDK attribute values read through their
// JSON encoding (ResolveAttribute).
[TestFixture]
public class SwitchbackAttributePathTests
{
    private static JsonElement Json(string json) => JsonDocument.Parse(json).RootElement;

    private static object? Resolve(string path, string attributes) =>
        SwitchbackAttributePath.ParseSupported(path)!.Resolve(Json(attributes));

    private static string? Text(object? value) => value is JsonElement e ? e.GetRawText() : value?.ToString();

    [Test]
    public void MatchesEveryPathCase()
    {
        var fixture = SwitchbackFixtures.Json("attribute-path-cases.json");
        var attributes = fixture.GetProperty("attributes");
        var cases = fixture.GetProperty("cases");
        cases.GetArrayLength().Should().Be(7);

        foreach (var c in cases.EnumerateArray())
        {
            var path = SwitchbackAttributePath.ParseSupported(c.GetProperty("path").GetString()!);
            if (!c.GetProperty("supported").GetBoolean())
            {
                path.Should().BeNull(c.ToString());
                continue;
            }

            path.Should().NotBeNull(c.ToString());
            var expected = c.GetProperty("value");
            var actual = path!.Resolve(attributes);
            if (expected.ValueKind == JsonValueKind.Null)
                actual.Should().BeNull(c.ToString());
            else
                Text(actual).Should().Be(expected.GetRawText(), c.ToString());
        }
    }

    [Test]
    public void QuotedSegmentsAreLiteralKeys()
    {
        const string attributes = "{\"geo\":{\"0\":\"zero\",\"a/b\":\"slash\"}}";
        Text(Resolve("geo/'0'", attributes)).Should().Be("\"zero\"");
        Text(Resolve("geo/\"a/b\"", attributes)).Should().Be("\"slash\"");
    }

    // Malformed paths are unsupported, which makes the experiment unservable: an unterminated quote
    // anywhere, or a '.' anywhere, quoted or not ('/' nests).
    [TestCase("geo/'0")]
    [TestCase("'geo")]
    [TestCase("\"geo/city")]
    [TestCase("geo/\"")]
    [TestCase("geo.city")]
    [TestCase("geo/a.b")]
    [TestCase("a./b")]
    [TestCase(".")]
    [TestCase("items/0.5/city")]
    [TestCase("'geo.city'")]
    [TestCase("\"geo.city\"")]
    [TestCase("geo/'a.b'")]
    public void RejectsMalformedPaths(string path)
    {
        SwitchbackAttributePath.ParseSupported(path).Should().BeNull();
    }

    [Test]
    public void SeparatorsAreCollapsed()
    {
        const string attributes = "{\"geo\":{\"city\":\"Porto\"}}";
        Text(Resolve("/geo//city/", attributes)).Should().Be("\"Porto\"");
        Text(Resolve("geo", attributes)).Should().Be("{\"city\":\"Porto\"}");
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("/")]
    [TestCase("//")]
    [TestCase("0/city")]
    [TestCase("items/0")]
    [TestCase("items/0/1/city")]
    [TestCase("items/-1/city")]
    [TestCase("'")]
    public void RejectsUnsupportedPaths(string? path)
    {
        SwitchbackAttributePath.ParseSupported(path!).Should().BeNull();
    }

    [Test]
    public void IndexSegmentsReadOnlyListsAndKeySegmentsReadOnlyMaps()
    {
        const string attributes =
            "{\"items\":[{\"city\":\"Lisbon\"}],\"geo\":{\"0\":{\"city\":\"zero\"}},\"name\":\"scalar\"}";
        Resolve("geo/0/city", attributes).Should().BeNull();
        Resolve("items/1/city", attributes).Should().BeNull();
        Resolve("name/x", attributes).Should().BeNull();
        Resolve("missing/x", attributes).Should().BeNull();
        Text(Resolve("items/-0/city", attributes)).Should().Be("\"Lisbon\"");
    }

    [Test]
    public void TryParseIntFollowsGuava()
    {
        SwitchbackAttributePath.TryParseInt("0").Should().Be(0);
        SwitchbackAttributePath.TryParseInt("-0").Should().Be(0);
        SwitchbackAttributePath.TryParseInt("007").Should().Be(7);
        SwitchbackAttributePath.TryParseInt("2147483647").Should().Be(int.MaxValue);
        SwitchbackAttributePath.TryParseInt("-2147483648").Should().Be(int.MinValue);
        foreach (var s in new[] { "", "-", "+1", "1a", " 1", "2147483648", "-2147483649", "99999999999999999999", "٠", "1.0" })
            SwitchbackAttributePath.TryParseInt(s).Should().BeNull(s);
    }

    // SDK attribute values are read through their JSON encoding, as the Collector decodes the
    // published attribute.
    [Test]
    public void AttributeValuesAreReadThroughTheirJsonEncoding()
    {
        var city = SwitchbackAttributePath.ParseSupported("geo/city")!;
        Text(city.ResolveAttribute(new Dictionary<string, object> { ["city"] = "Porto" })).Should().Be("\"Porto\"");
        Text(city.ResolveAttribute(new Dictionary<string, object> { ["city"] = 42L })).Should().Be("42");
        Text(city.ResolveAttribute(new Dictionary<string, object> { ["city"] = 42m })).Should().Be("42");
        Text(city.ResolveAttribute(Json("{\"city\":\"Porto\"}"))).Should().Be("\"Porto\"");
        Text(city.ResolveAttribute(new { city = "Porto" })).Should().Be("\"Porto\"");

        var item = SwitchbackAttributePath.ParseSupported("items/1/city")!;
        Text(item.ResolveAttribute(new List<object>
        {
            new Dictionary<string, object> { ["city"] = "Lisbon" },
            new Dictionary<string, object> { ["city"] = "Faro" }
        })).Should().Be("\"Faro\"");

        var root = SwitchbackAttributePath.ParseSupported("city")!;
        root.ResolveAttribute("Porto").Should().Be("Porto");
        root.ResolveAttribute(7).Should().Be(7);
        root.ResolveAttribute('c').Should().Be("c");
        city.ResolveAttribute("Porto").Should().BeNull();
        city.ResolveAttribute(null!).Should().BeNull();
    }

    // A whole double or float nested in a value stays a float through the encoding (System.Text.Json
    // writes 3.0 as 3), so it never keys a cluster, as on the JVM.
    [Test]
    public void NestedWholeFloatsStayFloats()
    {
        var city = SwitchbackAttributePath.ParseSupported("geo/city")!;
        foreach (var value in new object[] { 3.0, 2.0f, 1e20, -0.0 })
        {
            var resolved = city.ResolveAttribute(new Dictionary<string, object> { ["city"] = value });
            SwitchbackClusterKeys.Normalize(resolved).Should().BeNull(value.ToString());
        }

        SwitchbackClusterKeys.Normalize(city.ResolveAttribute(new Dictionary<string, object> { ["city"] = double.NaN }))
            .Should().BeNull();
    }

    // Half is a floating-point number, so it never keys a cluster. An enum is read as its name, as
    // java-sdk's Jackson encodes it and as the context publishes it with switchbacks installed.
    [Test]
    public void HalfHasNoKeyAndAnEnumIsItsName()
    {
        var root = SwitchbackAttributePath.ParseSupported("city")!;
        var nested = SwitchbackAttributePath.ParseSupported("geo/city")!;
        foreach (var value in new object[] { (Half)3, (Half)2.5 })
        {
            SwitchbackClusterKeys.Normalize(root.ResolveAttribute(value)).Should().BeNull(value.ToString());
            SwitchbackClusterKeys.Normalize(nested.ResolveAttribute(new Dictionary<string, object> { ["city"] = value }))
                .Should().BeNull(value.ToString());
        }

        SwitchbackClusterKeys.Normalize(root.ResolveAttribute(DayOfWeek.Monday)).Should().Be("Monday");
        SwitchbackClusterKeys.Normalize(nested.ResolveAttribute(new Dictionary<string, object> { ["city"] = DayOfWeek.Monday }))
            .Should().Be("Monday");
    }

    // A value with no JSON encoding has no cluster key: a cycle, nesting deeper than the
    // serializer allows, or a type it rejects.
    [Test]
    public void CyclicTooDeepOrUnencodableValuesHaveNoKey()
    {
        var path = SwitchbackAttributePath.ParseSupported("geo/city")!;

        var self = new Dictionary<string, object> { ["city"] = "Porto" };
        self["self"] = self;
        path.ResolveAttribute(self).Should().BeNull();

        var outer = new Dictionary<string, object> { ["city"] = "Porto" };
        outer["items"] = new List<object> { new object[] { outer } };
        path.ResolveAttribute(outer).Should().BeNull();

        object deep = new Dictionary<string, object> { ["leaf"] = "x" };
        for (var i = 0; i < 70; ++i) deep = new Dictionary<string, object> { ["city"] = "Porto", ["next"] = deep };
        path.ResolveAttribute(deep).Should().BeNull();

        path.ResolveAttribute(new Dictionary<string, object> { ["city"] = typeof(string) }).Should().BeNull();

        var shared = new Dictionary<string, object> { ["a"] = "b" };
        Text(path.ResolveAttribute(new Dictionary<string, object> { ["city"] = "Porto", ["x"] = shared, ["y"] = shared }))
            .Should().Be("\"Porto\"");
    }
}
