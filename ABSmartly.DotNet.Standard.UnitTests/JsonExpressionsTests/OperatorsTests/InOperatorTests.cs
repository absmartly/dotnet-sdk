﻿using ABSmartly.DotNet.Standard.UnitTests.TestUtils;
using ABSmartly.JsonExpressions;
using ABSmartly.JsonExpressions.Operators;
using Moq;
// ReSharper disable ExpressionIsAlwaysNull

namespace ABSmartly.DotNet.Standard.UnitTests.JsonExpressionsTests.OperatorsTests;

[TestFixture]
public class InOperatorTests : TestCases
{
#pragma warning disable CS8618
    private Mock<IEvaluator> evaluator;
    private InOperator inOperator;
#pragma warning restore CS8618

    [SetUp]
    public void Init()
    {
        evaluator = new Mock<IEvaluator>();

        evaluator.Setup(e => e.StringConvert(It.IsAny<object>()))
            .Returns(StringConvert);

        evaluator.Setup(x => x.Compare(It.IsAny<object>(), It.IsAny<object>()))
            .Returns(Compare);

        inOperator = new InOperator();
    }

    [TestCaseSource(nameof(NonStringOrIListOrIDictionary))]
    public void Binary_NonStringOrIListOrIDictionary_Returns_Null(object haystack)
    {
        var needle = "unused";
        var result = inOperator.Binary(evaluator.Object, haystack, needle);

        Assert.That(result, Is.Null);
    }


    [TestCaseSource(nameof(StringsWithEmpty))]
    public void Binary_StringWithNullNeedle_Returns_False(object haystack)
    {
        string needle = null;
        var result = inOperator.Binary(evaluator.Object, haystack, needle);

        Assert.That((bool)result, Is.False);
    }

    [TestCaseSource(nameof(StringsWithEmpty))]
    public void Binary_StringWithEmptyNeedle_Returns_False(object haystack)
    {
        var needle = "";
        var result = inOperator.Binary(evaluator.Object, haystack, needle);

        Assert.That((bool)result, Is.False);
    }

    [TestCaseSource(nameof(StringObject))]
    public void Binary_String_Returns_HayStackContainsStringifiedNeedle(object haystack, object needle)
    {
        var result = inOperator.Binary(evaluator.Object, haystack, needle);

        var expectedResult = haystack.ToString()!.Contains(needle.ToString()!);

        Assert.That(result, Is.EqualTo(expectedResult));
    }





    #region Helper

    private static string StringConvert(object ob)
    {
        return ob.ToString();
    }

    private static int? Compare(object lhs, object rhs)
    {
        if (lhs.ToString() == "null")
            return null;

        if (lhs.ToString() == "-1")
            return -1;

        if (lhs.ToString() == "0")
            return 0;

        if (lhs.ToString() == "1")
            return 1;

        return null;
    }

    #endregion
}