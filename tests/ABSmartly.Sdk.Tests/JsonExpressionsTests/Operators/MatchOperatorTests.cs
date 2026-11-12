using ABSmartly.JsonExpressions.Operators;

namespace ABSmartly.Sdk.Tests.JsonExpressionsTests.Operators;

[TestFixture]
public class MatchOperatorTests : OperatorTestBase
{
    private readonly MatchOperator _operator = new();

    [Test]
    public void TestEvaluate()
    {
        _operator.Evaluate(Evaluator, T.ListOf("abcdefghijk", "")).Should().Be(true);
        _operator.Evaluate(Evaluator, T.ListOf("abcdefghijk", "abc")).Should().Be(true);
        _operator.Evaluate(Evaluator, T.ListOf("abcdefghijk", "ijk")).Should().Be(true);
        _operator.Evaluate(Evaluator, T.ListOf("abcdefghijk", "^abc")).Should().Be(true);
        _operator.Evaluate(Evaluator, T.ListOf(",l5abcdefghijk", "ijk$")).Should().Be(true);
        _operator.Evaluate(Evaluator, T.ListOf("abcdefghijk", "def")).Should().Be(true);
        _operator.Evaluate(Evaluator, T.ListOf("abcdefghijk", "b.*j")).Should().Be(true);
        _operator.Evaluate(Evaluator, T.ListOf("abcdefghijk", "xyz")).Should().Be(false);

        _operator.Evaluate(Evaluator, T.ListOf(null, "abc")).Should().BeNull();
        _operator.Evaluate(Evaluator, T.ListOf("abcdefghijk", null)).Should().BeNull();
    }

    [Test]
    public void TestInvalidPatternReturnsNull()
    {
        _operator.Evaluate(Evaluator, T.ListOf("abc", "(")).Should().BeNull();
        _operator.Evaluate(Evaluator, T.ListOf("abc", "[a-")).Should().BeNull();
    }

    [Test]
    public void TestNoMatchTimeout()
    {
        // As 1.3.0 and the Java SDK there is no match timeout: a valid pattern that is
        // slow to evaluate still yields its result rather than null.
        var text = new string('a', 24) + "!";
        _operator.Evaluate(Evaluator, T.ListOf(text, "^(a+)+$")).Should().Be(false);
    }
}
