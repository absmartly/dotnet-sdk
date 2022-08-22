using ABSmartly.DotNet.Standard.UnitTests.JsonExpressionsTests.OperatorsTests.TestImplementations;
using ABSmartlySdk.JsonExpressions;
using ABSmartlySdk.JsonExpressions.Operators;
using Moq;

namespace ABSmartly.DotNet.Standard.UnitTests.JsonExpressionsTests.OperatorsTests;

[TestFixture]
public class UnaryOperatorTests : TestCases
{
#pragma warning disable CS8618
    private Mock<IEvaluator> evaluator;
    private UnaryOperator unaryOperator;
#pragma warning restore CS8618

    [SetUp]
    public void Init()
    {
        evaluator = new Mock<IEvaluator>();

        evaluator.Setup(x => x.Evaluate(It.IsAny<object>()))
            .Returns(Evaluate);

        unaryOperator = new UnaryOperatorTestImplementation();
    }


    [TestCaseSource(nameof(StringsWithEmpty))]
    public void Evaluate_Returns_ValidResult(object parameter)
    {
        var result = unaryOperator.Evaluate(evaluator.Object, parameter);

        Assert.That(result, Is.EqualTo(UnaryOperatorTestImplementation.ValidResult));
    }





    #region Helper

    private static object Evaluate(object ob)
    {
        return ob;
    }

    #endregion
}