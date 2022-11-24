using ABSmartly.JsonExpressions.Operators;

namespace ABSmartly.Sdk.Tests.JsonExpressionsTests.Operators;

[TestFixture]
public class NotOperatorTests : OperatorTestBase
{
    private readonly NotOperator _operator = new();

    [Test]
    public void TestFalse()
    {
        _operator.Evaluate(Evaluator, false).Should().Be(true);
        Mock.Get(Evaluator).Verify(x => x.Evaluate(false), Times.Once);
        Mock.Get(Evaluator).Verify(x => x.BooleanConvert(false), Times.Once);
    }

    [Test]
    public void TestTrue()
    {
        _operator.Evaluate(Evaluator, true).Should().Be(false);
        Mock.Get(Evaluator).Verify(x => x.Evaluate(true), Times.Once);
        Mock.Get(Evaluator).Verify(x => x.BooleanConvert(true), Times.Once);
    }

    [Test]
    public void TestNull()
    {
        _operator.Evaluate(Evaluator, null).Should().Be(true);
        Mock.Get(Evaluator).Verify(x => x.Evaluate(null), Times.Once);
        Mock.Get(Evaluator).Verify(x => x.BooleanConvert(null), Times.Once);
    }
}