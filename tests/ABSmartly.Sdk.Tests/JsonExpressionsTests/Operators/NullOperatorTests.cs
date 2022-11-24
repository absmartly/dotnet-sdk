using ABSmartly.JsonExpressions.Operators;

namespace ABSmartly.Sdk.Tests.JsonExpressionsTests.Operators;

[TestFixture]
public class NullOperatorTests : OperatorTestBase
{
    private readonly NullOperator _operator = new();

    [Test]
    public void TestNull()
    {
        _operator.Evaluate(Evaluator, null).Should().Be(true);
        Mock.Get(Evaluator).Verify(x => x.Evaluate(null), Times.Once);
    }

    [Test]
    public void TestNotNull()
    {
        _operator.Evaluate(Evaluator, false).Should().Be(false);
        Mock.Get(Evaluator).Verify(x => x.Evaluate(false), Times.Once);

        _operator.Evaluate(Evaluator, true).Should().Be(false);
        Mock.Get(Evaluator).Verify(x => x.Evaluate(true), Times.Once);

        _operator.Evaluate(Evaluator, 0).Should().Be(false);
        Mock.Get(Evaluator).Verify(x => x.Evaluate(0), Times.Once);
    }
}