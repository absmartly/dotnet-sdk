using ABSmartly.DotNet.Standard.UnitTests.JsonExpressionsTests.OperatorsTests.TestImplementations;
using ABSmartlySdk.JsonExpressions;
using ABSmartlySdk.JsonExpressions.Operators;
using Moq;

namespace ABSmartly.DotNet.Standard.UnitTests.JsonExpressionsTests.OperatorsTests;

[TestFixture]
public class BinaryOperatorTests : TestCases
{
#pragma warning disable CS8618
    private Mock<IEvaluator> evaluator;
    private BinaryOperator binaryOperator;
#pragma warning restore CS8618

    [SetUp]
    public void Init()
    {
        evaluator = new Mock<IEvaluator>();

        evaluator.Setup(x => x.Evaluate(It.IsAny<object>()))
            .Returns(Evaluate);

        binaryOperator = new BinaryOperatorTestImplementation();
    }

    [TestCaseSource(nameof(RandomNotIListValues))]
    public void Evaluate_RandomParameterWhichIsNotIList_ReturnsNull(object parameters)
    {
        var result = binaryOperator.Evaluate(evaluator.Object, parameters);

        Assert.That(result, Is.Null);
    }

    [TestCaseSource(nameof(RandomEmptyIListValues))]
    public void Evaluate_RandomParameter_EmptyIList_ReturnsNull(object parameters)
    {
        var result = binaryOperator.Evaluate(evaluator.Object, parameters);

        Assert.That(result, Is.Null);
    }

    [TestCaseSource(nameof(RandomOneElementIListValues))]
    public void Evaluate_RandomParameter_OneElementIList_ReturnsNull(object parameters)
    {
        var result = binaryOperator.Evaluate(evaluator.Object, parameters);

        Assert.That(result, Is.Null);
    }

    [TestCaseSource(nameof(RandomTwoOrMoreElementIListValues))]
    public void Evaluate_RandomParameter_TwoOrMoreElementIList_Returns_ValidResult(object parameters)
    {
        var result = binaryOperator.Evaluate(evaluator.Object, parameters);

        Assert.That(result, Is.EqualTo(BinaryOperatorTestImplementation.ValidResult));
    }

    #region Helper

    private static object Evaluate(object ob)
    {
        return ob;
    }

    #endregion
}