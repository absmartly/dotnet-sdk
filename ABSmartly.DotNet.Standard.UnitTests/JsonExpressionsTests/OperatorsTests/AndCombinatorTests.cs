﻿using ABSmartly.DotNet.Standard.UnitTests.TestUtils;
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







    //[Test]
    //public void Combine_True_Returns_True()
    //{
    //    var parameters = new List<object>
    //    {
    //        true
    //    };

    //    var result = combinator.Combine(evaluator.Object, parameters);

    //    Assert.That(result, Is.EqualTo(true));
    //}

    //[Test]
    //public void Combine_True_Verify_EvaluateTrue_Called1x()
    //{
    //    var parameters = new List<object>
    //    {
    //        true
    //    };

    //    combinator.Combine(evaluator.Object, parameters);

    //    evaluator.Verify(ev => ev.Evaluate(true), Times.Exactly(1));
    //}

    //[Test]
    //public void Combine_True_Verify_BooleanConvertTrue_Called1x()
    //{
    //    var parameters = new List<object>
    //    {
    //        true
    //    };

    //    combinator.Combine(evaluator.Object, parameters);

    //    evaluator.Verify(ev => ev.BooleanConvert(true), Times.Exactly(1));
    //}


    //[Test]
    //public void Combine_False_Returns_False()
    //{
    //    var parameters = new List<object>
    //    {
    //        false
    //    };

    //    var result = combinator.Combine(evaluator.Object, parameters);

    //    Assert.That(result, Is.EqualTo(false));
    //}

    //[Test]
    //public void Combine_False_Verify_EvaluateFalse_Called1x()
    //{
    //    var parameters = new List<object>
    //    {
    //        false
    //    };

    //    combinator.Combine(evaluator.Object, parameters);

    //    evaluator.Verify(ev => ev.Evaluate(false), Times.Exactly(1));
    //}

    //[Test]
    //public void Combine_False_Verify_BooleanConvertFalse_Called1x()
    //{
    //    var parameters = new List<object>
    //    {
    //        false
    //    };

    //    combinator.Combine(evaluator.Object, parameters);

    //    evaluator.Verify(ev => ev.BooleanConvert(false), Times.Exactly(1));
    //}


    //[Test]
    //public void Combine_Null_Returns_False()
    //{
    //    var parameters = new List<object?>
    //    {
    //        null
    //    };

    //    var result = combinator.Combine(evaluator.Object, parameters);

    //    Assert.That(result, Is.EqualTo(false));
    //}

    //[Test]
    //public void Combine_Null_Verify_EvaluateNull_Called1x()
    //{
    //    var parameters = new List<object?>
    //    {
    //        null
    //    };

    //    combinator.Combine(evaluator.Object, parameters);

    //    evaluator.Verify(ev => ev.Evaluate(null), Times.Exactly(1));
    //}

    //[Test]
    //public void Combine_Null_Verify_BooleanConvertNull_Called1x()
    //{
    //    var parameters = new List<object?>
    //    {
    //        null
    //    };

    //    combinator.Combine(evaluator.Object, parameters);

    //    evaluator.Verify(ev => ev.BooleanConvert(null), Times.Exactly(1));
    //}


    //[Test]
    //public void Combine_TrueFalseTrue_Returns_False()
    //{
    //    var parameters = new List<object>
    //    {
    //        true, false, true
    //    };

    //    var result = combinator.Combine(evaluator.Object, parameters);

    //    Assert.That(result, Is.EqualTo(false));
    //}

    //[Test]
    //public void Combine_TrueFalseTrue_Verify_EvaluateTrue2x_EvaluateFalse1x()
    //{
    //    var parameters = new List<object>
    //    {
    //        true, false, true
    //    };

    //    combinator.Combine(evaluator.Object, parameters);

    //    evaluator.Verify(ev => ev.Evaluate(true), Times.Exactly(1));
    //    evaluator.Verify(ev => ev.Evaluate(false), Times.Exactly(1));
    //}

    //[Test]
    //public void Combine_TrueFalseTrue_Verify_BooleanConvertNull_Called1x()
    //{
    //    var parameters = new List<object>
    //    {
    //        true, false, true
    //    };

    //    combinator.Combine(evaluator.Object, parameters);

    //    evaluator.Verify(ev => ev.BooleanConvert(true), Times.Exactly(1));
    //    evaluator.Verify(ev => ev.BooleanConvert(false), Times.Exactly(1));
    //}


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