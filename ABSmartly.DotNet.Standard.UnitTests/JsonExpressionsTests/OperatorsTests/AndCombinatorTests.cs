using ABSmartly.JsonExpressions;
using ABSmartly.JsonExpressions.Operators;
using Moq;

namespace ABSmartly.DotNet.Standard.UnitTests.JsonExpressionsTests.OperatorsTests;

[TestFixture]
public class AndCombinatorTests
{
#pragma warning disable CS8618
    private Mock<IEvaluator> evaluator;
    private AndCombinator combinator;
#pragma warning restore CS8618

    [SetUp]
    public void Init()
    {
        evaluator = new Mock<IEvaluator>();

        evaluator.Setup(x => x.Evaluate(It.IsAny<bool>()))
            .Returns(Evaluate);

        evaluator.Setup(x => x.BooleanConvert(It.IsAny<bool>()))
            .Returns(BoolConvert);

        combinator = new AndCombinator();
    }

    [Test]
    public void Combine_True_Returns_True()
    {
        var parameters = new List<object>
        {
            true
        };

        var result = combinator.Combine(evaluator.Object, parameters);

        Assert.That(result, Is.EqualTo(true));
    }

    [Test]
    public void Combine_True_Verify_EvaluateTrue_CalledOnce()
    {
        var parameters = new List<object>
        {
            true
        };

        combinator.Combine(evaluator.Object, parameters);

        evaluator.Verify(ev => ev.BooleanConvert(true), Times.Exactly(1));
    }

    [Test]
    public void Combine_True_Verify_BooleanConvertTrue_CalledOnce()
    {
        var parameters = new List<object>
        {
            true
        };

        combinator.Combine(evaluator.Object, parameters);

        evaluator.Verify(ev => ev.BooleanConvert(true), Times.Exactly(1));
    }



    #region Helper

    private static object Evaluate(object ob)
    {
        return ob;
    }
    private static bool BoolConvert(bool bools)
    {
        return bools;
    }    

    #endregion
}