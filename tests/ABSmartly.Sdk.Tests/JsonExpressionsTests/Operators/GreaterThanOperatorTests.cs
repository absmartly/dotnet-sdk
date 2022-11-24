using ABSmartly.JsonExpressions.Operators;

namespace ABSmartly.Sdk.Tests.JsonExpressionsTests.Operators;

[TestFixture]
public class GreaterThanOperatorTests : OperatorTestBase
{
    private readonly GreaterThanOperator _operator = new();

    [Test]
    public void TestEvaluate()
    {
        _operator.Evaluate(Evaluator, T.ListOf(0.0, 0.0)).Should().Be(false);
        Mock.Get(Evaluator).Verify(x => x.Evaluate(It.IsAny<object>()), Times.Exactly(2));
        Mock.Get(Evaluator).Verify(x => x.Evaluate(0.0), Times.Exactly(2));
        Mock.Get(Evaluator).Verify(x => x.Compare(0.0, 0.0), Times.Once);

        Mock.Get(Evaluator).Invocations.Clear();
        _operator.Evaluate(Evaluator, T.ListOf(1.0, 0.0)).Should().Be(true);
        Mock.Get(Evaluator).Verify(x => x.Evaluate(It.IsAny<object>()), Times.Exactly(2));
        Mock.Get(Evaluator).Verify(x => x.Evaluate(0.0), Times.Once);
        Mock.Get(Evaluator).Verify(x => x.Evaluate(1.0), Times.Once);
        Mock.Get(Evaluator).Verify(x => x.Compare(1.0, 0.0), Times.Once);

        Mock.Get(Evaluator).Invocations.Clear();
        _operator.Evaluate(Evaluator, T.ListOf(0.0, 1.0)).Should().Be(false);
        Mock.Get(Evaluator).Verify(x => x.Evaluate(It.IsAny<object>()), Times.Exactly(2));
        Mock.Get(Evaluator).Verify(x => x.Evaluate(0.0), Times.Once);
        Mock.Get(Evaluator).Verify(x => x.Evaluate(1.0), Times.Once);
        Mock.Get(Evaluator).Verify(x => x.Compare(0.0, 1.0), Times.Once);

        Mock.Get(Evaluator).Invocations.Clear();
        _operator.Evaluate(Evaluator, T.ListOf(null, null)).Should().BeNull();
        Mock.Get(Evaluator).Verify(x => x.Evaluate(It.IsAny<object>()), Times.Once);
        Mock.Get(Evaluator).Verify(x => x.Compare(It.IsAny<object>(), It.IsAny<object>()), Times.Never);
    }
}