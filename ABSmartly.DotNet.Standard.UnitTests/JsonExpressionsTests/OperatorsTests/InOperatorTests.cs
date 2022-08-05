﻿using ABSmartly.DotNet.Standard.UnitTests.TestUtils;
using ABSmartly.JsonExpressions;
using ABSmartly.JsonExpressions.Operators;
using Moq;

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
    public void Binary_NonStringOrIListOrIDictionary_ReturnsNull(object haystack)
    {
        var needle = "unused";
        var result = inOperator.Binary(evaluator.Object, haystack, needle);

        Assert.IsNull(result);
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