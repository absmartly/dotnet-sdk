using Moq;

namespace ABSmartly.DotNet.Standard.UnitTests.JsonExpressionsTests.OperatorsTests;

public class OperatorTestsBase
{
    private Mock<IEvaluator> evaluator;

    [SetUp]
    public void Init()
    {
        evaluator = new Mock<IEvaluator>();
    }
}