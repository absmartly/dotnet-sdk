using ABSmartly.JsonExpressions.Operators;

namespace ABSmartly.Sdk.Tests.JsonExpressionsTests.Operators;

[TestFixture]
public class MatchOperatorTests: OperatorTestBase
{
    private readonly MatchOperator _operator = new();

    [Test]
    public void TestEvaluate()
    {
        _operator.Evaluate(Evaluator,  T.ListOf("abcdefghijk", "")).Should().Be(true);
        _operator.Evaluate(Evaluator,  T.ListOf("abcdefghijk", "abc")).Should().Be(true);
        _operator.Evaluate(Evaluator,  T.ListOf("abcdefghijk", "ijk")).Should().Be(true);
        _operator.Evaluate(Evaluator,  T.ListOf("abcdefghijk", "^abc")).Should().Be(true);
        _operator.Evaluate(Evaluator,  T.ListOf(",l5abcdefghijk", "ijk$")).Should().Be(true);
        _operator.Evaluate(Evaluator,  T.ListOf("abcdefghijk", "def")).Should().Be(true);
        _operator.Evaluate(Evaluator,  T.ListOf("abcdefghijk", "b.*j")).Should().Be(true);
        _operator.Evaluate(Evaluator,  T.ListOf("abcdefghijk", "xyz")).Should().Be(false);
        
        _operator.Evaluate(Evaluator,  T.ListOf(null, "abc")).Should().BeNull();
        _operator.Evaluate(Evaluator,  T.ListOf("abcdefghijk", null)).Should().BeNull();
    }
}