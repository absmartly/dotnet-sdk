using ABSmartly.JsonExpressions;
using ABSmartly.JsonExpressions.Operators;
using Moq;

namespace ABSmartly.DotNet.Standard.UnitTests.JsonExpressionsTests.OperatorsTests;

[TestFixture]
public class MatchOperatorTests : TestCases
{
#pragma warning disable CS8618
    private Mock<IEvaluator> evaluator;
    private MatchOperator matchOperator;
#pragma warning restore CS8618

    [SetUp]
    public void Init()
    {
        evaluator = new Mock<IEvaluator>();

        evaluator.Setup(e => e.StringConvert(It.IsAny<object>()))
            .Returns(Evaluator_StringConvert);

        matchOperator = new MatchOperator();
    }

    [TestCase(null, "unused")]
    public void Binary_NullLHS_Returns_Null(object lhs, object rhs)
    {
        var result = matchOperator.Binary(evaluator.Object, lhs, rhs);

        Assert.That(result, Is.Null);
    }

    [TestCase("", "unused")]
    public void Binary_EmptyLHS_Returns_Null(object lhs, object rhs)
    {
        var result = matchOperator.Binary(evaluator.Object, lhs, rhs);

        Assert.That(result, Is.Null);
    }

    [TestCase("valid", null)]
    public void Binary_ValidLHS_NullRHS_Returns_Null(object lhs, object rhs)
    {
        var result = matchOperator.Binary(evaluator.Object, lhs, rhs);

        Assert.That(result, Is.Null);
    }

    [TestCase("valid", "")]
    public void Binary_ValidLHS_EmptyRHS_Returns_True(object lhs, object rhs)
    {
        var result = matchOperator.Binary(evaluator.Object, lhs, rhs);

        Assert.That(result, Is.True);
    }

    [TestCase("abcdefghijk", "abc")]
    [TestCase("abcdefghijk", "ijk")]
    [TestCase("abcdefghijk", "^abc")]
    [TestCase(",l5abcdefghijk", "ijk$")]
    [TestCase("abcdefghijk", "def")]
    [TestCase("abcdefghijk", "b.*j")]
    public void Binary_MatchingValues_Returns_True(object lhs, object rhs)
    {
        var result = matchOperator.Binary(evaluator.Object, lhs, rhs);

        Assert.That(result, Is.True);
    }

    [TestCase("abcdefghijk", "*****")]
    public void Binary_InvalidRHS_RegexException_Returns_Null(object lhs, object rhs)
    {
        var result = matchOperator.Binary(evaluator.Object, lhs, rhs);

        Assert.That(result, Is.Null);
    }

    #region Helper

    private string Evaluator_StringConvert(object arg)
    {
        if (arg is null)
            return null;

        return arg.ToString();
    }

    #endregion
}