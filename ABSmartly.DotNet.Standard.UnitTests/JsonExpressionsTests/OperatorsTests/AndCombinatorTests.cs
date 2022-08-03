using ABSmartly.DotNet.Standard.UnitTests.TestUtils;
using ABSmartly.JsonExpressions;
using ABSmartly.JsonExpressions.Operators;
using Moq;

namespace ABSmartly.DotNet.Standard.UnitTests.JsonExpressionsTests.OperatorsTests;

[TestFixture]
public class AndCombinatorTests : TestCases
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

    [TestCaseSource(nameof(ObjectArrayOfBoolWithNull))]
    public void Combine_Returns_True_IfAllElementsEqualsTrue_OtherwiseFalse(object?[] parameters)
    {
        var result = combinator.Combine(evaluator.Object, parameters.ToList());

        Assert.That(result, Is.EqualTo(parameters.All(x => (bool?)x == true)));
    }


    [TestCaseSource(nameof(ObjectArrayOfBoolWithNull))]
    public void Combine_Verify_EvaluateOfTrueCalledNumberOfFirstTruesInSequence(object?[] parameters)
    {
        combinator.Combine(evaluator.Object, parameters.ToList());

        var count = 0;
        foreach (var parameter in parameters)
        {
            if ((bool?)parameter != true)
                break;

            count++;
        }

        evaluator.Verify(ev => ev.Evaluate(true), Times.Exactly(count));
    }

    [TestCaseSource(nameof(ObjectArrayOfBoolWithNull))]
    public void Combine_Verify_EvaluateOfFalseCalledNumberOfFirstFalseOrNull(object?[] parameters)
    {
        combinator.Combine(evaluator.Object, parameters.ToList());

        var count = 0;
        foreach (var parameter in parameters)
        {
            if ((bool?)parameter is null)
            {
                break;
            }
            if ((bool?)parameter is false)
            {
                count++;
                break;
            }
        }

        Assert.Less(count, 2);
        evaluator.Verify(ev => ev.Evaluate(false), Times.Exactly(count));
    }

    [TestCaseSource(nameof(ObjectArrayOfBoolWithNull))]
    public void Combine_Verify_EvaluateOfNullCalledNumberOfFirstFalseOrNull(object?[] parameters)
    {
        combinator.Combine(evaluator.Object, parameters.ToList());

        var count = 0;
        foreach (var parameter in parameters)
        {
            if ((bool?)parameter is null)
            {
                count++;
                break;
            }
            if ((bool?)parameter is false)
            {
                break;
            }
        }

        Assert.Less(count, 2);
        evaluator.Verify(ev => ev.Evaluate(null), Times.Exactly(count));
    }


    [TestCaseSource(nameof(ObjectArrayOfBoolWithNull))]
    public void Combine_Verify_BooleanConvertOfTrueCalledNumberOfFirstTruesInSequence(object?[] parameters)
    {
        combinator.Combine(evaluator.Object, parameters.ToList());

        var count = 0;
        foreach (var parameter in parameters)
        {
            if ((bool?)parameter != true)
                break;

            count++;
        }

        evaluator.Verify(ev => ev.BooleanConvert(true), Times.Exactly(count));
    }

    [TestCaseSource(nameof(ObjectArrayOfBoolWithNull))]
    public void Combine_Verify_BooleanConvertOfFalseCalledNumberOfFirstFalseOrNull(object?[] parameters)
    {
        combinator.Combine(evaluator.Object, parameters.ToList());

        var count = 0;
        foreach (var parameter in parameters)
        {
            if ((bool?)parameter is null)
            {
                break;
            }
            if ((bool?)parameter is false)
            {
                count++;
                break;
            }
        }

        Assert.Less(count, 2);
        evaluator.Verify(ev => ev.BooleanConvert(false), Times.Exactly(count));
    }

    [TestCaseSource(nameof(ObjectArrayOfBoolWithNull))]
    public void Combine_Verify_BooleanConvertOfNullCalledNumberOfFirstFalseOrNull(object?[] parameters)
    {
        combinator.Combine(evaluator.Object, parameters.ToList());

        var count = 0;
        foreach (var parameter in parameters)
        {
            if ((bool?)parameter is null)
            {
                count++;
                break;
            }
            if ((bool?)parameter is false)
            {
                break;
            }
        }

        Assert.Less(count, 2);
        evaluator.Verify(ev => ev.BooleanConvert(null), Times.Exactly(count));
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