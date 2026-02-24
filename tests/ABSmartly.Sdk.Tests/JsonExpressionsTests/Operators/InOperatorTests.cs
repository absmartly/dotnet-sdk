using ABSmartly.JsonExpressions.Operators;

namespace ABSmartly.Sdk.Tests.JsonExpressionsTests.Operators;

[TestFixture]
public class InOperatorTests : OperatorTestBase
{
    private readonly InOperator _operator = new();

    [Test]
    public void TestStrings()
    {
        _operator.Evaluate(Evaluator, T.ListOf("abc", "abcdefghijk")).Should().Be(true);
        _operator.Evaluate(Evaluator, T.ListOf("def", "abcdefghijk")).Should().Be(true);
        _operator.Evaluate(Evaluator, T.ListOf("xxx", "abcdefghijk")).Should().Be(false);
        _operator.Evaluate(Evaluator, T.ListOf(null, "abcdefghijk")).Should().BeNull();
        _operator.Evaluate(Evaluator, T.ListOf("xxx", null)).Should().BeNull();

        Mock.Get(Evaluator).Verify(x => x.Evaluate("abcdefghijk"), Times.Exactly(3));
        Mock.Get(Evaluator).Verify(x => x.Evaluate("abc"), Times.Once);
        Mock.Get(Evaluator).Verify(x => x.Evaluate("def"), Times.Once);
        Mock.Get(Evaluator).Verify(x => x.Evaluate("xxx"), Times.Exactly(2));
        Mock.Get(Evaluator).Verify(x => x.StringConvert("abc"), Times.Once);
        Mock.Get(Evaluator).Verify(x => x.StringConvert("def"), Times.Once);
        Mock.Get(Evaluator).Verify(x => x.StringConvert("xxx"), Times.Once);
    }

    [Test]
    public void TestArrayEmpty()
    {
        _operator.Evaluate(Evaluator, T.ListOf(1, T.ListOf())).Should().Be(false);
        _operator.Evaluate(Evaluator, T.ListOf("1", T.ListOf())).Should().Be(false);
        _operator.Evaluate(Evaluator, T.ListOf(true, T.ListOf())).Should().Be(false);
        _operator.Evaluate(Evaluator, T.ListOf(false, T.ListOf())).Should().Be(false);
        _operator.Evaluate(Evaluator, T.ListOf(null, T.ListOf())).Should().BeNull();

        Mock.Get(Evaluator).Verify(x => x.BooleanConvert(It.IsAny<object>()), Times.Never);
        Mock.Get(Evaluator).Verify(x => x.NumberConvert(It.IsAny<object>()), Times.Never);
        Mock.Get(Evaluator).Verify(x => x.StringConvert(It.IsAny<object>()), Times.Never);
        Mock.Get(Evaluator).Verify(x => x.Compare(It.IsAny<object>(), It.IsAny<object>()), Times.Never);
    }

    [Test]
    public void TestArrayCompares()
    {
        var haystack01 = T.ListOf(0.0, 1.0);
        var haystack12 = T.ListOf(1.0, 2.0);

        _operator.Evaluate(Evaluator, T.ListOf(2.0, haystack01)).Should().Be(false);
        Mock.Get(Evaluator).Verify(x => x.Evaluate(It.IsAny<object>()), Times.Exactly(2));
        Mock.Get(Evaluator).Verify(x => x.Evaluate(haystack01), Times.Once);
        Mock.Get(Evaluator).Verify(x => x.Evaluate(2.0), Times.Once);
        Mock.Get(Evaluator).Verify(x => x.Compare(It.IsAny<double>(), 2.0), Times.Exactly(2));

        Mock.Get(Evaluator).Invocations.Clear();
        _operator.Evaluate(Evaluator, T.ListOf(0.0, haystack12)).Should().Be(false);
        Mock.Get(Evaluator).Verify(x => x.Evaluate(It.IsAny<object>()), Times.Exactly(2));
        Mock.Get(Evaluator).Verify(x => x.Evaluate(haystack12), Times.Once);
        Mock.Get(Evaluator).Verify(x => x.Evaluate(0.0), Times.Once);
        Mock.Get(Evaluator).Verify(x => x.Compare(It.IsAny<double>(), 0.0), Times.Exactly(2));

        Mock.Get(Evaluator).Invocations.Clear();
        _operator.Evaluate(Evaluator, T.ListOf(0.0, haystack01)).Should().Be(true);
        Mock.Get(Evaluator).Verify(x => x.Evaluate(It.IsAny<object>()), Times.Exactly(2));
        Mock.Get(Evaluator).Verify(x => x.Evaluate(haystack01), Times.Once);
        Mock.Get(Evaluator).Verify(x => x.Evaluate(0.0), Times.Once);
        Mock.Get(Evaluator).Verify(x => x.Compare(It.IsAny<double>(), 0.0), Times.Once);

        Mock.Get(Evaluator).Invocations.Clear();
        _operator.Evaluate(Evaluator, T.ListOf(2.0, haystack12)).Should().Be(true);
        Mock.Get(Evaluator).Verify(x => x.Evaluate(It.IsAny<object>()), Times.Exactly(2));
        Mock.Get(Evaluator).Verify(x => x.Evaluate(haystack12), Times.Once);
        Mock.Get(Evaluator).Verify(x => x.Evaluate(2.0), Times.Once);
        Mock.Get(Evaluator).Verify(x => x.Compare(It.IsAny<double>(), 2.0), Times.Exactly(2));
    }

    [Test]
    public void TestObject()
    {
        var haystackAb = T.MapOf("a", 1, "b", 2);
        var haystackBc = T.MapOf("b", 2, "c", 3, "0", 100);

        _operator.Evaluate(Evaluator, T.ListOf("c", haystackAb)).Should().Be(false);
        Mock.Get(Evaluator).Verify(x => x.Evaluate(It.IsAny<object>()), Times.Exactly(2));
        Mock.Get(Evaluator).Verify(x => x.Evaluate(haystackAb), Times.Once);
        Mock.Get(Evaluator).Verify(x => x.StringConvert(It.IsAny<object>()), Times.Once);
        Mock.Get(Evaluator).Verify(x => x.StringConvert("c"), Times.Once);
        Mock.Get(Evaluator).Verify(x => x.Evaluate("c"), Times.Once);

        Mock.Get(Evaluator).Invocations.Clear();
        _operator.Evaluate(Evaluator, T.ListOf("a", haystackBc)).Should().Be(false);
        Mock.Get(Evaluator).Verify(x => x.Evaluate(It.IsAny<object>()), Times.Exactly(2));
        Mock.Get(Evaluator).Verify(x => x.Evaluate(haystackBc), Times.Once);
        Mock.Get(Evaluator).Verify(x => x.StringConvert(It.IsAny<object>()), Times.Once);
        Mock.Get(Evaluator).Verify(x => x.StringConvert("a"), Times.Once);
        Mock.Get(Evaluator).Verify(x => x.Evaluate("a"), Times.Once);

        Mock.Get(Evaluator).Invocations.Clear();
        _operator.Evaluate(Evaluator, T.ListOf("a", haystackAb)).Should().Be(true);
        Mock.Get(Evaluator).Verify(x => x.Evaluate(It.IsAny<object>()), Times.Exactly(2));
        Mock.Get(Evaluator).Verify(x => x.Evaluate(haystackAb), Times.Once);
        Mock.Get(Evaluator).Verify(x => x.StringConvert(It.IsAny<object>()), Times.Once);
        Mock.Get(Evaluator).Verify(x => x.StringConvert("a"), Times.Once);
        Mock.Get(Evaluator).Verify(x => x.Evaluate("a"), Times.Once);

        Mock.Get(Evaluator).Invocations.Clear();
        _operator.Evaluate(Evaluator, T.ListOf("c", haystackBc)).Should().Be(true);
        Mock.Get(Evaluator).Verify(x => x.Evaluate(It.IsAny<object>()), Times.Exactly(2));
        Mock.Get(Evaluator).Verify(x => x.Evaluate(haystackBc), Times.Once);
        Mock.Get(Evaluator).Verify(x => x.StringConvert(It.IsAny<object>()), Times.Once);
        Mock.Get(Evaluator).Verify(x => x.StringConvert("c"), Times.Once);
        Mock.Get(Evaluator).Verify(x => x.Evaluate("c"), Times.Once);

        Mock.Get(Evaluator).Invocations.Clear();
        _operator.Evaluate(Evaluator, T.ListOf(0, haystackBc)).Should().Be(true);
        Mock.Get(Evaluator).Verify(x => x.Evaluate(It.IsAny<object>()), Times.Exactly(2));
        Mock.Get(Evaluator).Verify(x => x.Evaluate(haystackBc), Times.Once);
        Mock.Get(Evaluator).Verify(x => x.StringConvert(It.IsAny<object>()), Times.Once);
        Mock.Get(Evaluator).Verify(x => x.StringConvert(0), Times.Once);
        Mock.Get(Evaluator).Verify(x => x.Evaluate(0), Times.Once);
    }
}