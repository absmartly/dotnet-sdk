using ABSmartly.DotNet.Standard.UnitTests.TestUtils;
using ABSmartly.JsonExpressions;
using ABSmartly.JsonExpressions.Operators;
using Moq;

namespace ABSmartly.DotNet.Standard.UnitTests.JsonExpressionsTests.OperatorsTests;

[TestFixture]
public class NotOperatorTests : TestCases
{
#pragma warning disable CS8618
    private Mock<IEvaluator> evaluator;
    private NotOperator notOperator;
#pragma warning restore CS8618

    [SetUp]
    public void Init()
    {
        evaluator = new Mock<IEvaluator>();

        evaluator.Setup(e => e.BooleanConvert(It.IsAny<bool?>()))
            .Returns(NullableBoolConvert);

        notOperator = new NotOperator();
    }

    [TestCaseSource(nameof(BoolsWithNull))]
    public void Unary_Returns_ExpectedResult(object parameter)
    {
        var result = notOperator.Unary(evaluator.Object, parameter);

        var expectedResult = (bool?)parameter == null || (bool?)parameter == false;

        Assert.That(result, Is.EqualTo(expectedResult));
    }

    #region Helper

    private static bool NullableBoolConvert(bool? arg)
    {
        if (arg is null)
            return false;

        return arg.Value;
    }

    #endregion
}