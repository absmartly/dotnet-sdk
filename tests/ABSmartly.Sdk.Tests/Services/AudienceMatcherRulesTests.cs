using ABSmartly.Services;

namespace ABSmartly.Sdk.Tests.Services;

// AudienceMatcher.EvaluateRules, ported from the javascript-sdk and java-sdk matcher tests.
[TestFixture]
public class AudienceMatcherRulesTests
{
    private const string Us = "{\"and\":[{\"eq\":[{\"var\":\"country\"},{\"value\":\"US\"}]}]}";
    private const string Gb = "{\"and\":[{\"eq\":[{\"var\":\"country\"},{\"value\":\"GB\"}]}]}";
    private static readonly string Fallback = Assign(null, "[]", "2");

    private readonly AudienceMatcher _matcher = new(new DefaultAudienceDeserializer());
    private readonly Dictionary<string, object> _us = new() { ["country"] = "US" };

    private static string Rules(params string[] rules) => "{\"rules\":[" + string.Join(",", rules) + "]}";

    private static string Assign(string? conditions, string? environments, string variant)
    {
        var parts = new List<string> { "\"name\":\"assign\"", "\"type\":\"assign\"", "\"variant\":" + variant };
        if (conditions != null) parts.Add("\"conditions\":" + conditions);
        if (environments != null) parts.Add("\"environments\":" + environments);
        return "{" + string.Join(",", parts) + "}";
    }

    private static string Split(string percentages) =>
        "{\"name\":\"split\",\"type\":\"split\",\"conditions\":" + Us + ",\"environments\":[],\"percentages\":" +
        percentages + "}";

    private AudienceMatcher.RuleAction? EvaluateUs(params string[] rules) =>
        _matcher.EvaluateRules(Rules(rules), "production", _us);

    private static void ShouldAssign(AudienceMatcher.RuleAction? action, int variant)
    {
        action.Should().NotBeNull();
        action!.IsSplit.Should().BeFalse();
        action.Variant.Should().Be(variant);
    }

    private static void ShouldSplit(AudienceMatcher.RuleAction? action, params double[] split)
    {
        action.Should().NotBeNull();
        action!.IsSplit.Should().BeTrue();
        action.Split.Should().Equal(split);
    }

    [Test]
    public void ReturnsNullWithoutRules()
    {
        _matcher.EvaluateRules("{}", "production", new Dictionary<string, object>()).Should().BeNull();
        _matcher.EvaluateRules("{\"filter\":[]}", "production", new Dictionary<string, object>()).Should().BeNull();
        _matcher.EvaluateRules("{\"rules\":[]}", "production", new Dictionary<string, object>()).Should().BeNull();
        _matcher.EvaluateRules(null!, "production", new Dictionary<string, object>()).Should().BeNull();
    }

    [Test]
    public void MatchesConditions()
    {
        var rules = Rules(Assign(Us, "[]", "1"));
        ShouldAssign(_matcher.EvaluateRules(rules, "production", _us), 1);
        _matcher.EvaluateRules(rules, "production", new Dictionary<string, object> { ["country"] = "GB" })
            .Should().BeNull();
    }

    [Test]
    public void Environments()
    {
        var both = Rules(Assign(Us, "[\"production\",\"staging\"]", "2"));
        ShouldAssign(_matcher.EvaluateRules(both, "production", _us), 2);
        ShouldAssign(_matcher.EvaluateRules(both, "staging", _us), 2);
        _matcher.EvaluateRules(both, "development", _us).Should().BeNull();

        var all = Rules(Assign("{\"value\":true}", "[]", "1"));
        ShouldAssign(_matcher.EvaluateRules(all, "production", new Dictionary<string, object>()), 1);
        ShouldAssign(_matcher.EvaluateRules(all, null!, new Dictionary<string, object>()), 1);

        _matcher.EvaluateRules(Rules(Assign("{\"value\":true}", "[\"production\"]", "1")), null!,
            new Dictionary<string, object>()).Should().BeNull();
    }

    [Test]
    public void EnvironmentNamesAreCaseSensitive()
    {
        var rules = Rules(Assign(Us, "[\"Production\"]", "1"));
        _matcher.EvaluateRules(rules, "production", _us).Should().BeNull();
        ShouldAssign(_matcher.EvaluateRules(rules, "Production", _us), 1);
    }

    [Test]
    public void SkipsNonListEnvironments()
    {
        ShouldAssign(EvaluateUs(Assign(null, "\"production\"", "1"), Fallback), 2);
        ShouldAssign(EvaluateUs(Assign(null, "null", "1")), 1);
        ShouldAssign(EvaluateUs(Assign(null, null, "1")), 1);
    }

    [Test]
    public void FirstMatchWins()
    {
        ShouldAssign(EvaluateUs(Assign(Us, "[]", "1"), Assign(Us, "[]", "2")), 1);
        ShouldAssign(EvaluateUs(Assign(Gb, "[]", "1"), Assign(Us, "[]", "2")), 2);
    }

    [Test]
    public void RuleWithoutConditionsMatchesAll()
    {
        ShouldAssign(EvaluateUs(Assign("null", "[]", "3")), 3);
        ShouldAssign(EvaluateUs(Assign(null, "[]", "3")), 3);
    }

    [Test]
    public void Malformed()
    {
        var empty = new Dictionary<string, object>();
        _matcher.EvaluateRules("not json", "production", empty).Should().BeNull();
        _matcher.EvaluateRules("", "production", empty).Should().BeNull();
        _matcher.EvaluateRules("{\"rules\":\"not an array\"}", "production", empty).Should().BeNull();
        _matcher.EvaluateRules("{\"rules\":[null]}", "production", empty).Should().BeNull();
        _matcher.EvaluateRules("{\"rules\":[5, \"x\"]}", "production", empty).Should().BeNull();
    }

    [Test]
    public void InvalidVariantIsSkipped()
    {
        ShouldAssign(EvaluateUs(Assign(null, "[]", "\"not a number\""), Fallback), 2);
        ShouldAssign(EvaluateUs(Assign(null, "[]", "null"), Fallback), 2);
        ShouldAssign(EvaluateUs(Assign(null, "[]", "1.5"), Fallback), 2);
        ShouldAssign(EvaluateUs(Assign(null, "[]", "1e100"), Fallback), 2);
        ShouldAssign(EvaluateUs(Assign(null, "[]", "\"1\""), Fallback), 2);
        ShouldAssign(EvaluateUs(Assign(null, "[]", "true"), Fallback), 2);
        ShouldAssign(EvaluateUs(Assign(null, "[]", "1.0")), 1);
    }

    [Test]
    public void UnknownTypeIsSkipped()
    {
        ShouldAssign(EvaluateUs("{\"type\":\"other\",\"variant\":1}", "{\"variant\":1}", Fallback), 2);
    }

    [Test]
    public void VariantZeroAndNegativeAreReturned()
    {
        ShouldAssign(EvaluateUs(Assign("{\"value\":true}", "[]", "0")), 0);
        ShouldAssign(EvaluateUs(Assign("null", "[]", "-1")), -1);
    }

    [Test]
    public void SkipsNonObjectConditions()
    {
        ShouldAssign(EvaluateUs(Assign("\"invalid\"", "[]", "1"), Fallback), 2);
        ShouldAssign(EvaluateUs(Assign("[{\"value\":true}]", "[]", "1"), Fallback), 2);
        ShouldAssign(EvaluateUs(Assign("[]", "[]", "1"), Fallback), 2);
    }

    [Test]
    public void SkipsConditionsThatDoNotEvaluateToTrue()
    {
        ShouldAssign(EvaluateUs(Assign("{\"value\":false}", "[]", "1"), Fallback), 2);
        ShouldAssign(EvaluateUs(Assign("{\"value\":0}", "[]", "1"), Fallback), 2);
        ShouldAssign(EvaluateUs(Assign("{\"var\":\"missing\"}", "[]", "1"), Fallback), 2);
    }

    [Test]
    public void SplitReturnsFractionsWhenConditionsMatch()
    {
        ShouldSplit(EvaluateUs(Split("\"20/30/50\"")), 0.2, 0.3, 0.5);
        ShouldSplit(EvaluateUs(Split("\"33.33/33.33/33.33\"")), 0.3333, 0.3333, 0.3333);
        _matcher.EvaluateRules(Rules(Split("\"50/50\"")), "production",
            new Dictionary<string, object> { ["country"] = "GB" }).Should().BeNull();
    }

    [TestCase("[50, 50]")]
    [TestCase("\"50/abc\"")]
    [TestCase("\"150/-50\"")]
    [TestCase("\"50/49\"")]
    [TestCase("\"\"")]
    [TestCase("\"50//50\"")]
    [TestCase("\"Infinity/0\"")]
    [TestCase("null")]
    public void InvalidSplitIsSkipped(string percentages)
    {
        ShouldAssign(EvaluateUs(Split(percentages), Fallback), 2);
    }

    // The backend's check: the gap from 100 is rounded to two decimals before being compared
    // with 0.01, so gaps up to 0.0149... are accepted.
    [TestCase("50/50.014", 50.0, 50.014)]
    [TestCase("50/49.986", 50.0, 49.986)]
    [TestCase("100", 100.0)]
    [TestCase(" 20/30%/50px", 20.0, 30.0, 50.0)]
    [TestCase("2e1/30/50", 20.0, 30.0, 50.0)]
    public void SplitWithinToleranceIsAccepted(string percentages, params double[] values)
    {
        // Each value divided by 100 with no normalisation, as JavaScript's parseFloat(value) / 100.
        ShouldSplit(EvaluateUs(Split("\"" + percentages + "\""), Fallback), values.Select(v => v / 100).ToArray());
    }

    [TestCase("50/50.015")]
    [TestCase("50/49.985")]
    public void SplitOutsideToleranceIsSkipped(string percentages)
    {
        ShouldAssign(EvaluateUs(Split("\"" + percentages + "\""), Fallback), 2);
    }

    [TestCase("12.5", 12.5)]
    [TestCase("  7", 7.0)]
    [TestCase("+3", 3.0)]
    [TestCase(".5", 0.5)]
    [TestCase("5.", 5.0)]
    [TestCase("1e2", 100.0)]
    [TestCase("1e", 1.0)]
    [TestCase("1e+", 1.0)]
    [TestCase("3abc", 3.0)]
    public void ParseFloatMatchesJavaScript(string value, double expected)
    {
        AudienceMatcher.ParseFloat(value).Should().Be(expected);
    }

    [TestCase("")]
    [TestCase("abc")]
    [TestCase(".")]
    [TestCase("-")]
    [TestCase("e5")]
    public void ParseFloatIsNaNWithoutANumber(string value)
    {
        double.IsNaN(AudienceMatcher.ParseFloat(value)).Should().BeTrue();
    }

    [Test]
    public void ParseFloatParsesInfinity()
    {
        AudienceMatcher.ParseFloat("Infinity").Should().Be(double.PositiveInfinity);
        AudienceMatcher.ParseFloat("-Infinity").Should().Be(double.NegativeInfinity);
    }

    [Test]
    public void ParseRulesReportsInvalidJson()
    {
        _matcher.ParseRules(null!).Should().BeNull();
        _matcher.ParseRules("").Should().BeNull();
        _matcher.ParseRules("not json")!.IsInvalid.Should().BeTrue();
        _matcher.ParseRules("{\"rules\":5}")!.IsInvalid.Should().BeFalse();
    }
}
