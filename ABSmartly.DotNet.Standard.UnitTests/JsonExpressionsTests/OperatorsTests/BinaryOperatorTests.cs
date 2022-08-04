using ABSmartly.DotNet.Standard.UnitTests.JsonExpressionsTests.OperatorsTests.TestImplementations;
using ABSmartly.JsonExpressions;
using ABSmartly.JsonExpressions.Operators;
using Moq;

namespace ABSmartly.DotNet.Standard.UnitTests.JsonExpressionsTests.OperatorsTests;

[TestFixture]
public class BinaryOperatorTests
{
#pragma warning disable CS8618
    private Mock<IEvaluator> evaluator;
    private BinaryOperator op;
#pragma warning restore CS8618

    [SetUp]
    public void Init()
    {
        evaluator = new Mock<IEvaluator>();

        evaluator.Setup(x => x.Evaluate(It.IsAny<bool>()))
            .Returns(Evaluate);

        //evaluator.Setup(x => x.Compare(It.IsAny<bool>(), It.IsAny<bool>()))
        //    .Returns(Compare);

        op = new BinaryOperatorTestImplementation();
    }

    [Test]
    public void Evaluate_RandomParameterWhichIsNotIList_ReturnsNull()
    {
        var result = op.Evaluate(evaluator.Object, new bool[]{});

        Assert.IsNull(result);

    }

    #region Helper

    private static object Evaluate(object ob)
    {
        return ob;
    }
    //private static int? Compare(object lhs, object rhs)
    //{
    //    if (lhs == rhs)
    //        return 1;

    //    return 0;
    //}

    #endregion
}