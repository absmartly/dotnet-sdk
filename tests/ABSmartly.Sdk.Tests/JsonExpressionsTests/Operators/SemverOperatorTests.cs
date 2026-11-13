using ABSmartly.JsonExpressions;

namespace ABSmartly.Sdk.Tests.JsonExpressionsTests.Operators;

[TestFixture]
public class SemverOperatorTests
{
    private static readonly Dictionary<string, object> Vars = new() { ["app_version"] = "2.4.1-beta.2" };

    private static Dictionary<string, object> Op(string op, string var, object value) => new()
    {
        [op] = new List<object>
        {
            new Dictionary<string, object> { ["var"] = new Dictionary<string, object> { ["path"] = var } },
            new Dictionary<string, object> { ["value"] = value }
        }
    };

    [TestCase("semver_gte", "2.4.0", true)]
    [TestCase("semver_gte", "2.4.1", false)]
    [TestCase("semver_lt", "2.4.1", true)]
    [TestCase("semver_lt", "2.4.1-beta.2", false)]
    [TestCase("semver_gt", "2.4.1-beta.1", true)]
    [TestCase("semver_gt", "2.4.1-beta.2", false)]
    [TestCase("semver_lte", "2.4.1-beta.2", true)]
    [TestCase("semver_lte", "2.4.1-beta.1", false)]
    [TestCase("semver_eq", "v2.4.1-beta.2+build.7", true)]
    [TestCase("semver_eq", "2.4.1", false)]
    public void EvaluatesSemverOperators(string op, string value, bool expected)
    {
        JsonExpr.EvaluateBooleanExpr(Op(op, "app_version", value), Vars).Should().Be(expected);
    }

    [TestCase("semver_eq")]
    [TestCase("semver_gt")]
    [TestCase("semver_gte")]
    [TestCase("semver_lt")]
    [TestCase("semver_lte")]
    public void MissingOperandIsFalse(string op)
    {
        JsonExpr.EvaluateBooleanExpr(Op(op, "missing", "2.4.1"), Vars).Should().BeFalse();
    }

    [Test]
    public void UnusableVersionIsFalseForEveryOperator()
    {
        foreach (var op in new[] { "semver_eq", "semver_gt", "semver_gte", "semver_lt", "semver_lte" })
            JsonExpr.EvaluateBooleanExpr(Op(op, "app_version", ""), Vars).Should().BeFalse(op);
    }
}
