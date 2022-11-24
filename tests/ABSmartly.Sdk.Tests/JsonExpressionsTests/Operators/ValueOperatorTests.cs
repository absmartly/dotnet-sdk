using ABSmartly.JsonExpressions.Operators;

namespace ABSmartly.Sdk.Tests.JsonExpressionsTests.Operators;

[TestFixture]
public class ValueOperatorTests : OperatorTestBase
{
    private readonly ValueOperator _operator = new();

    [Test]
    public void TestEvaluate()
    {
        _operator.Evaluate(Evaluator, 0).Should().Be(0);
        _operator.Evaluate(Evaluator, 1).Should().Be(1);
        _operator.Evaluate(Evaluator, true).Should().Be(true);
        _operator.Evaluate(Evaluator, false).Should().Be(false);
        _operator.Evaluate(Evaluator, "").Should().Be("");
        _operator.Evaluate(Evaluator, "abc").Should().Be("abc");
        _operator.Evaluate(Evaluator, new List<object>()).Should().BeEquivalentTo(new List<object>());
        _operator.Evaluate(Evaluator, new Dictionary<string, object>()).Should()
            .BeEquivalentTo(new Dictionary<string, object>());
        _operator.Evaluate(Evaluator, null).Should().BeNull();

        Mock.Get(Evaluator).Verify(x => x.Evaluate(It.IsAny<object>()), Times.Never);
    }
}