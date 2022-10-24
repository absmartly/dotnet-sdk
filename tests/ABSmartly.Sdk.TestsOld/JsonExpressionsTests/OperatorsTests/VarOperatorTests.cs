using ABSmartly.JsonExpressions;
using ABSmartly.JsonExpressions.Operators;
using Moq;

namespace ABSmartly.DotNet.Standard.UnitTests.JsonExpressionsTests.OperatorsTests;

[TestFixture]
public class VarOperatorTests : TestCases
{
    private Mock<IEvaluator> evaluator;
    private VarOperator varOperator;
#pragma warning restore CS8618

    [SetUp]
    public void Init()
    {
        evaluator = new Mock<IEvaluator>();

        evaluator.Setup(e => e.ExtractVariable(It.IsAny<string>()))
            .Returns(Evaluator_ExtractVariable);

        varOperator = new VarOperator();
    }

    [TestCaseSource(nameof(NotStringOrDictionaryOfObjectString))]
    public void Evaluate_ParamIsNotStringOrDictionaryOfObjectString_Returns_Null(object parameter)
    {
        var result = varOperator.Evaluate(evaluator.Object, parameter);

        Assert.That(result, Is.Null);
    }

    [TestCaseSource(nameof(StringsWithEmpty))]
    public void Evaluate_StringParam_Returns_ExtractVariableResult(object parameter)
    {
        var result = varOperator.Evaluate(evaluator.Object, parameter);

        var expectedResult = Evaluator_ExtractVariable((string)parameter);

        Assert.That(result, Is.EqualTo(expectedResult));
    }

    [TestCaseSource(nameof(DictionaryOfStringString))]
    public void Evaluate_DictionaryOfStringStringWithoutKeyOfPath_Returns_Null(object parameter)
    {
        var result = varOperator.Evaluate(evaluator.Object, parameter);

        Assert.That(result, Is.Null);
    }

    [TestCaseSource(nameof(DictionaryOfStringString_WithKeyOfPath))]
    public void Evaluate_DictionaryOfStringStringWithKeyOfPath_Returns_ExtractVariableResult(object parameter)
    {
        var result = varOperator.Evaluate(evaluator.Object, parameter);

        var dict = (Dictionary<string, string>)parameter;
        var expectedResult = Evaluator_ExtractVariable(dict["path"]);

        Assert.That(result, Is.EqualTo(expectedResult));
    }

    #region Helper

    private static object Evaluator_ExtractVariable(string arg) => arg;

    #endregion

}