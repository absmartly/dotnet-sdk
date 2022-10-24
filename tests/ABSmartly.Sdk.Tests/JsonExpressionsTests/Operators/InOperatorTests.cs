using ABSmartly.JsonExpressions.Operators;

namespace ABSmartly.Sdk.Tests.JsonExpressionsTests.Operators;

[TestFixture]
public class InOperatorTests: OperatorTestBase
{
    private readonly InOperator _operator = new();

    [Test]
    public void TestStrings()
    {
        _operator.Evaluate(Evaluator,  T.ListOf("abcdefghijk", "abc")).Should().Be(true);
        _operator.Evaluate(Evaluator,  T.ListOf("abcdefghijk", "def")).Should().Be(true);
        _operator.Evaluate(Evaluator,  T.ListOf("abcdefghijk", "xxx")).Should().Be(false);
        _operator.Evaluate(Evaluator,  T.ListOf("abcdefghijk", null)).Should().BeNull();
        _operator.Evaluate(Evaluator,  T.ListOf(null, "xxx")).Should().BeNull();
        
        Mock.Get(Evaluator).Verify(x => x.Evaluate("abcdefghijk"), Times.Exactly(4));
        Mock.Get(Evaluator).Verify(x => x.Evaluate("abc"), Times.Once);
        Mock.Get(Evaluator).Verify(x => x.Evaluate("def"), Times.Once);
        Mock.Get(Evaluator).Verify(x => x.Evaluate("xxx"), Times.Once);
        Mock.Get(Evaluator).Verify(x => x.StringConvert("abc"), Times.Once);
        Mock.Get(Evaluator).Verify(x => x.StringConvert("def"), Times.Once);
        Mock.Get(Evaluator).Verify(x => x.StringConvert("xxx"), Times.Once);
    }
    
    [Test]
    public void TestArrayEmpty()
    {
        _operator.Evaluate(Evaluator,  T.ListOf<object>(T.ListOf<object>(), 1)).Should().Be(false);
        _operator.Evaluate(Evaluator,  T.ListOf<object>(T.ListOf<object>(), "1")).Should().Be(false);
        _operator.Evaluate(Evaluator,  T.ListOf<object>(T.ListOf<object>(), true)).Should().Be(false);
        _operator.Evaluate(Evaluator,  T.ListOf<object>(T.ListOf<object>(), false)).Should().Be(false);
        _operator.Evaluate(Evaluator,  T.ListOf<object>(T.ListOf<object>(), null)).Should().BeNull();
        
        Mock.Get(Evaluator).Verify(x => x.BooleanConvert(It.IsAny<object>()), Times.Never);
        Mock.Get(Evaluator).Verify(x => x.NumberConvert(It.IsAny<object>()), Times.Never);
        Mock.Get(Evaluator).Verify(x => x.StringConvert(It.IsAny<object>()), Times.Never);
        Mock.Get(Evaluator).Verify(x => x.Compare(It.IsAny<object>(), It.IsAny<object>()), Times.Never);
    }
    
    [Test]
    public void TestArrayCompares()
    {
        var haystack01 = T.ListOf<object>(0.0, 1.0);
        var haystack12 = T.ListOf<object>(1.0, 2.0);
        
        _operator.Evaluate(Evaluator,  T.ListOf<object>(haystack01, 2.0)).Should().Be(false);
        Mock.Get(Evaluator).Verify(x => x.Evaluate(It.IsAny<object>()), Times.Exactly(2));
        Mock.Get(Evaluator).Verify(x => x.Evaluate(haystack01), Times.Once);
        Mock.Get(Evaluator).Verify(x => x.Evaluate(2.0), Times.Once);
        Mock.Get(Evaluator).Verify(x => x.Compare(It.IsAny<double>(), 2.0), Times.Exactly(2));
        
        Mock.Get(Evaluator).Invocations.Clear();
        _operator.Evaluate(Evaluator,  T.ListOf<object>(haystack12, 0.0)).Should().Be(false);
        Mock.Get(Evaluator).Verify(x => x.Evaluate(It.IsAny<object>()), Times.Exactly(2));
        Mock.Get(Evaluator).Verify(x => x.Evaluate(haystack12), Times.Once);
        Mock.Get(Evaluator).Verify(x => x.Evaluate(0.0), Times.Once);
        Mock.Get(Evaluator).Verify(x => x.Compare(It.IsAny<double>(), 0.0), Times.Exactly(2));
        
        Mock.Get(Evaluator).Invocations.Clear();
        _operator.Evaluate(Evaluator,  T.ListOf<object>(haystack01, 0.0)).Should().Be(true);
        Mock.Get(Evaluator).Verify(x => x.Evaluate(It.IsAny<object>()), Times.Exactly(2));
        Mock.Get(Evaluator).Verify(x => x.Evaluate(haystack01), Times.Once);
        Mock.Get(Evaluator).Verify(x => x.Evaluate(0.0), Times.Once);
        Mock.Get(Evaluator).Verify(x => x.Compare(It.IsAny<double>(), 0.0), Times.Once);
        
        Mock.Get(Evaluator).Invocations.Clear();
        _operator.Evaluate(Evaluator,  T.ListOf<object>(haystack12, 2.0)).Should().Be(true);
        Mock.Get(Evaluator).Verify(x => x.Evaluate(It.IsAny<object>()), Times.Exactly(2));
        Mock.Get(Evaluator).Verify(x => x.Evaluate(haystack12), Times.Once);
        Mock.Get(Evaluator).Verify(x => x.Evaluate(2.0), Times.Once);
        Mock.Get(Evaluator).Verify(x => x.Compare(It.IsAny<double>(), 2.0), Times.Exactly(2));
    }
    
    [Test]
    public void TestObject()
    {
        // TODO: support casting, or generics
        var haystackAb = T.MapOf<string, object>("a", 1, "b", 2);
        var haystackBc = T.MapOf<string, object>("b", 2, "c", 3, "0", 100);
        
        _operator.Evaluate(Evaluator,  T.ListOf<object>(haystackAb, "c")).Should().Be(false);
        Mock.Get(Evaluator).Verify(x => x.Evaluate(It.IsAny<object>()), Times.Exactly(2));
        Mock.Get(Evaluator).Verify(x => x.Evaluate(haystackAb), Times.Once);
        Mock.Get(Evaluator).Verify(x => x.StringConvert(It.IsAny<object>()), Times.Once);
        Mock.Get(Evaluator).Verify(x => x.StringConvert("c"), Times.Once);
        Mock.Get(Evaluator).Verify(x => x.Evaluate("c"), Times.Once);
        
        Mock.Get(Evaluator).Invocations.Clear();
        _operator.Evaluate(Evaluator,  T.ListOf<object>(haystackBc, "a")).Should().Be(false);
        Mock.Get(Evaluator).Verify(x => x.Evaluate(It.IsAny<object>()), Times.Exactly(2));
        Mock.Get(Evaluator).Verify(x => x.Evaluate(haystackBc), Times.Once);
        Mock.Get(Evaluator).Verify(x => x.StringConvert(It.IsAny<object>()), Times.Once);
        Mock.Get(Evaluator).Verify(x => x.StringConvert("a"), Times.Once);
        Mock.Get(Evaluator).Verify(x => x.Evaluate("a"), Times.Once);
        
        Mock.Get(Evaluator).Invocations.Clear();
        _operator.Evaluate(Evaluator,  T.ListOf<object>(haystackAb, "a")).Should().Be(true);
        Mock.Get(Evaluator).Verify(x => x.Evaluate(It.IsAny<object>()), Times.Exactly(2));
        Mock.Get(Evaluator).Verify(x => x.Evaluate(haystackAb), Times.Once);
        Mock.Get(Evaluator).Verify(x => x.StringConvert(It.IsAny<object>()), Times.Once);
        Mock.Get(Evaluator).Verify(x => x.StringConvert("a"), Times.Once);
        Mock.Get(Evaluator).Verify(x => x.Evaluate("a"), Times.Once);
        
        Mock.Get(Evaluator).Invocations.Clear();
        _operator.Evaluate(Evaluator,  T.ListOf<object>(haystackBc, "c")).Should().Be(true);
        Mock.Get(Evaluator).Verify(x => x.Evaluate(It.IsAny<object>()), Times.Exactly(2));
        Mock.Get(Evaluator).Verify(x => x.Evaluate(haystackBc), Times.Once);
        Mock.Get(Evaluator).Verify(x => x.StringConvert(It.IsAny<object>()), Times.Once);
        Mock.Get(Evaluator).Verify(x => x.StringConvert("c"), Times.Once);
        Mock.Get(Evaluator).Verify(x => x.Evaluate("c"), Times.Once);
        
        Mock.Get(Evaluator).Invocations.Clear();
        _operator.Evaluate(Evaluator,  T.ListOf<object>(haystackBc, 0)).Should().Be(true);
        Mock.Get(Evaluator).Verify(x => x.Evaluate(It.IsAny<object>()), Times.Exactly(2));
        Mock.Get(Evaluator).Verify(x => x.Evaluate(haystackBc), Times.Once);
        Mock.Get(Evaluator).Verify(x => x.StringConvert(It.IsAny<object>()), Times.Once);
        Mock.Get(Evaluator).Verify(x => x.StringConvert(0), Times.Once);
        Mock.Get(Evaluator).Verify(x => x.Evaluate(0), Times.Once);
    }
}