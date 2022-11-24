using ABSmartly.JsonExpressions.Operators;

namespace ABSmartly.Sdk.Tests.JsonExpressionsTests.Operators;

[TestFixture]
public class VarOperatorTests : OperatorTestBase
{
    private readonly VarOperator _operator = new();

    [Test]
    public void TestEvaluate()
    {
        _operator.Evaluate(Evaluator, "a/b/c").Should().Be("abc");

        Mock.Get(Evaluator).Verify(x => x.ExtractVariable(It.IsAny<string>()), Times.Once);
        Mock.Get(Evaluator).Verify(x => x.ExtractVariable("a/b/c"), Times.Once);
        Mock.Get(Evaluator).Verify(x => x.Evaluate(It.IsAny<object>()), Times.Never);
    }
}