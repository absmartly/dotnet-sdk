using System.Numerics;
using System.Text.Json;
using ABSmartly.Internal;

namespace ABSmartly.Sdk.Tests.Switchback;

// The cluster-value rule (owner decision, final) and the serving resolution, ported from java-sdk
// SwitchbackServingTest and the ClusterKeys cases: a non-empty string as is, a whole number of an
// integer type as its base-10 text (InvariantCulture); every float (3.0 included), boolean, object,
// array, empty string and null gives no value.
[TestFixture]
public class SwitchbackServingTests
{
    private static JsonElement Json(string json) => JsonDocument.Parse(json).RootElement;

    [Test]
    public void StringsAndWholeNumbersAreKeys()
    {
        SwitchbackClusterKeys.Normalize("porto").Should().Be("porto");
        SwitchbackClusterKeys.Normalize(" ").Should().Be(" ");
        SwitchbackClusterKeys.Normalize(17).Should().Be("17");
        SwitchbackClusterKeys.Normalize(-17L).Should().Be("-17");
        SwitchbackClusterKeys.Normalize((short)3).Should().Be("3");
        SwitchbackClusterKeys.Normalize((byte)3).Should().Be("3");
        SwitchbackClusterKeys.Normalize((sbyte)-3).Should().Be("-3");
        SwitchbackClusterKeys.Normalize((ushort)3).Should().Be("3");
        SwitchbackClusterKeys.Normalize(4000000002u).Should().Be("4000000002");
        SwitchbackClusterKeys.Normalize(long.MinValue).Should().Be("-9223372036854775808");
        SwitchbackClusterKeys.Normalize(ulong.MaxValue).Should().Be("18446744073709551615");
        SwitchbackClusterKeys.Normalize(BigInteger.Parse("123456789012345678901234567890"))
            .Should().Be("123456789012345678901234567890");
        SwitchbackClusterKeys.Normalize(Json("\"porto\"")).Should().Be("porto");
        SwitchbackClusterKeys.Normalize(Json("4000000002")).Should().Be("4000000002");
        SwitchbackClusterKeys.Normalize(Json("-0")).Should().Be("0");
        SwitchbackClusterKeys.Normalize(Json("123456789012345678901234567890"))
            .Should().Be("123456789012345678901234567890");
    }

    // Integer text is culture-independent: a culture with a different negative sign must not
    // change the key.
    [Test]
    public void IntegerTextUsesTheInvariantCulture()
    {
        var previous = System.Globalization.CultureInfo.CurrentCulture;
        try
        {
            var culture = (System.Globalization.CultureInfo)System.Globalization.CultureInfo.GetCultureInfo("fa-IR").Clone();
            culture.NumberFormat.NegativeSign = "~";
            System.Globalization.CultureInfo.CurrentCulture = culture;
            SwitchbackClusterKeys.Normalize(-17).Should().Be("-17");
            SwitchbackClusterKeys.Normalize(-17L).Should().Be("-17");
            SwitchbackClusterKeys.Normalize(new BigInteger(-17)).Should().Be("-17");
            SwitchbackClusterKeys.Normalize(Json("-17")).Should().Be("-17");
        }
        finally
        {
            System.Globalization.CultureInfo.CurrentCulture = previous;
        }
    }

    private static IEnumerable<TestCaseData> NoKeyValues()
    {
        yield return new TestCaseData(null).SetName("Null");
        yield return new TestCaseData("").SetName("EmptyString");
        yield return new TestCaseData(true).SetName("True");
        yield return new TestCaseData(false).SetName("False");
        yield return new TestCaseData(1.5).SetName("Fraction");
        yield return new TestCaseData(3.0).SetName("WholeDouble");
        yield return new TestCaseData(3.0f).SetName("WholeFloat");
        yield return new TestCaseData(3m).SetName("Decimal");
        yield return new TestCaseData('c').SetName("CharIsNotAString");
        yield return new TestCaseData(new Dictionary<string, object> { ["a"] = 1 }).SetName("Object");
        yield return new TestCaseData(new List<object> { "a" }).SetName("Array");
        yield return new TestCaseData(Json("3.0")).SetName("JsonWholeFloat");
        yield return new TestCaseData(Json("1e2")).SetName("JsonExponent");
        yield return new TestCaseData(Json("1.5")).SetName("JsonFraction");
        yield return new TestCaseData(Json("true")).SetName("JsonBool");
        yield return new TestCaseData(Json("null")).SetName("JsonNull");
        yield return new TestCaseData(Json("\"\"")).SetName("JsonEmptyString");
        yield return new TestCaseData(Json("{\"a\":1}")).SetName("JsonObject");
        yield return new TestCaseData(Json("[\"a\"]")).SetName("JsonArray");
    }

    [TestCaseSource(nameof(NoKeyValues))]
    public void OtherValuesAreNotKeys(object? value)
    {
        SwitchbackClusterKeys.Normalize(value).Should().BeNull();
    }

    private static KeyValuePair<string, SwitchbackAttributePath>[] Sources(params string[] typeAndPath)
    {
        var clusters = new List<KeyValuePair<string, string>>();
        for (var i = 0; i < typeAndPath.Length; i += 2)
            clusters.Add(new KeyValuePair<string, string>(typeAndPath[i], typeAndPath[i + 1]));
        return SwitchbackServing.Sources(clusters)!;
    }

    private static SwitchbackServing Resolve(KeyValuePair<string, SwitchbackAttributePath>[] sources,
        Dictionary<string, string> units, string attributes, string[]? allowed = null)
    {
        var json = Json(attributes);
        return SwitchbackServing.Resolve(sources, allowed!, units, path => path.Resolve(json));
    }

    // The declared source order decides, not the order of the units: with units listed user first
    // and sources declared session first, both agreeing, session serves.
    [Test]
    public void SourceOrderDecidesWhateverTheUnitOrder()
    {
        var units = new Dictionary<string, string> { ["user_id"] = "u1", ["session_id"] = "x1" };
        var serving = Resolve(Sources("session_id", "session_store", "user_id", "user_store"), units,
            "{\"session_store\":\"s-1\",\"user_store\":\"s-1\"}");

        serving.Status.Should().Be(SwitchbackServing.ServingStatus.Eligible);
        serving.UnitType.Should().Be("session_id");
        serving.ClusterKey.Should().Be("s-1");

        var reversed = Resolve(Sources("user_id", "user_store", "session_id", "session_store"), units,
            "{\"session_store\":\"s-1\",\"user_store\":\"s-1\"}");
        reversed.UnitType.Should().Be("user_id");
    }

    [Test]
    public void FirstValueServesAndADisagreementIsIneligible()
    {
        var units = new Dictionary<string, string> { ["a"] = "1", ["b"] = "2", ["c"] = "3" };
        var sources = Sources("a", "va", "b", "vb", "c", "vc");

        var skipped = Resolve(sources, units, "{\"vb\":\"k\",\"vc\":\"k\"}");
        (skipped.Status, skipped.UnitType, skipped.ClusterKey).Should().Be((SwitchbackServing.ServingStatus.Eligible, "b", "k"));

        var disagree = Resolve(sources, units, "{\"va\":\"k\",\"vc\":\"other\"}");
        (disagree.Status, disagree.UnitType, disagree.ClusterKey)
            .Should().Be((SwitchbackServing.ServingStatus.Ineligible, "a", (string?)null));

        var none = Resolve(sources, units, "{}");
        (none.Status, none.UnitType).Should().Be((SwitchbackServing.ServingStatus.Ineligible, "a"));

        Resolve(sources, units, "{\"va\":17,\"vb\":\"17\"}").ClusterKey.Should().Be("17");
    }

    [Test]
    public void OnlyLinkedUnitsTakePart()
    {
        var sources = Sources("a", "va", "b", "vb");
        var absent = Resolve(sources, new Dictionary<string, string> { ["z"] = "1" }, "{\"va\":\"k\"}");
        absent.Status.Should().Be(SwitchbackServing.ServingStatus.Absent);
        absent.UnitType.Should().BeNull();

        var onlyB = Resolve(sources, new Dictionary<string, string> { ["b"] = "1" }, "{\"va\":\"k\",\"vb\":\"j\"}");
        (onlyB.Status, onlyB.UnitType, onlyB.ClusterKey).Should().Be((SwitchbackServing.ServingStatus.Eligible, "b", "j"));
    }

    [Test]
    public void AllowList()
    {
        var sources = Sources("a", "va");
        var units = new Dictionary<string, string> { ["a"] = "1" };
        Resolve(sources, units, "{\"va\":\"k\"}", new[] { "k", "j" }).Status.Should().Be(SwitchbackServing.ServingStatus.Eligible);
        Resolve(sources, units, "{\"va\":\"x\"}", new[] { "k", "j" }).Status.Should().Be(SwitchbackServing.ServingStatus.Ineligible);
        Resolve(sources, units, "{\"va\":\"x\"}", Array.Empty<string>()).Status.Should().Be(SwitchbackServing.ServingStatus.Eligible);
        Resolve(sources, units, "{\"va\":\"x\"}").Status.Should().Be(SwitchbackServing.ServingStatus.Eligible);
        // A null entry (the decoder's placeholder for an element Jackson rejects) never matches, so
        // [null] filters every value rather than allowing all.
        SwitchbackServing.Allowed(new string[] { null! }, "42").Should().BeFalse();
        Resolve(sources, units, "{\"va\":\"x\"}", new string[] { null! }).Status
            .Should().Be(SwitchbackServing.ServingStatus.Ineligible);
        Resolve(sources, units, "{\"va\":\"K\"}", new[] { "k" }).Status.Should().Be(SwitchbackServing.ServingStatus.Ineligible);
    }

    [Test]
    public void UnreadableClusterMapsTakeTheLegacyPath()
    {
        SwitchbackServing.Sources(null!).Should().BeNull();
        SwitchbackServing.Sources(Array.Empty<KeyValuePair<string, string>>()).Should().BeNull();
        SwitchbackServing.Sources(new[] { new KeyValuePair<string, string>("a", null!) }).Should().BeNull();
        SwitchbackServing.Sources(new[]
        {
            new KeyValuePair<string, string>("a", "ok"), new KeyValuePair<string, string>("b", "geo.city")
        }).Should().BeNull();

        var units = new Dictionary<string, string> { ["store_id"] = "1" };
        var legacy = SwitchbackServing.Legacy("store_id", units);
        (legacy.Status, legacy.UnitType).Should().Be((SwitchbackServing.ServingStatus.Ineligible, "store_id"));
        SwitchbackServing.Legacy("user_id", units).Status.Should().Be(SwitchbackServing.ServingStatus.Absent);
        SwitchbackServing.Legacy(null!, units).Status.Should().Be(SwitchbackServing.ServingStatus.Absent);
    }
}
