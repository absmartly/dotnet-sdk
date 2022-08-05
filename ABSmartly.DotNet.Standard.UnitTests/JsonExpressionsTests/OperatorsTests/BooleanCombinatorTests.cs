using ABSmartly.DotNet.Standard.UnitTests.JsonExpressionsTests.OperatorsTests.TestImplementations;
using ABSmartly.DotNet.Standard.UnitTests.TestUtils;
using ABSmartly.JsonExpressions;
using ABSmartly.JsonExpressions.Operators;
using Moq;

namespace ABSmartly.DotNet.Standard.UnitTests.JsonExpressionsTests.OperatorsTests;

[TestFixture]
public class BooleanCombinatorTests : TestCases
{
#pragma warning disable CS8618
    private Mock<IEvaluator> evaluator;
    private BooleanCombinator booleanCombinator;
#pragma warning restore CS8618

    [SetUp]
    public void Init()
    {
        evaluator = new Mock<IEvaluator>();

        evaluator.Setup(x => x.Evaluate(It.IsAny<object>()))
            .Returns(Evaluate);

        booleanCombinator = new BooleanCombinatorTestImplementation();
    }

    [TestCaseSource(nameof(RandomNotIListValues))]
    public void Evaluate_RandomParameterWhichIsNotIList_ReturnsNull(object parameters)
    {
        var result = booleanCombinator.Evaluate(evaluator.Object, parameters);

        Assert.That(result, Is.Null);
    }

    [TestCaseSource(nameof(RandomEmptyIListValues))]
    [TestCaseSource(nameof(RandomOneElementIListWithoutArrayValues))]
    [TestCaseSource(nameof(RandomTwoOrMoreElementIListValues))]
    public void Evaluate_RandomParameter_IList_Returns_ValidResult(object parameters)
    {
        var result = booleanCombinator.Evaluate(evaluator.Object, parameters);

        Assert.That(result, Is.EqualTo(BooleanCombinatorTestImplementation.ValidResult));
    }

    #region Helper

    private static object Evaluate(object ob)
    {
        return ob;
    }

    #endregion
}