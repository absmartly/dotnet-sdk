using ABSmartly.JsonExpressions.Operators;

namespace ABSmartly.Sdk.Tests.JsonExpressionsTests.Operators;

[TestFixture]
public class AndCombinatorTests: OperatorTestBase
{
    private readonly AndCombinator _combinator = new();
    
    [Test]
    public void TestCombineTrue()
    {
        _combinator.Combine(Evaluator, T.ListOf<object>(true)).Should().Be(true);
        Mock.Get(Evaluator).Verify(e => e.BooleanConvert(true), Times.Once);
        Mock.Get(Evaluator).Verify(e => e.Evaluate(true), Times.Once);
    }
    
    [Test]
    public void TestCombineFalse()
    {
        _combinator.Combine(Evaluator, T.ListOf<object>(false)).Should().Be(false);
        Mock.Get(Evaluator).Verify(e => e.BooleanConvert(false), Times.Once);
        Mock.Get(Evaluator).Verify(e => e.Evaluate(false), Times.Once);
    }
    
    [Test]
    public void TestCombineNull()
    {
        _combinator.Combine(Evaluator, T.ListOf((object)null!)).Should().Be(false);
        Mock.Get(Evaluator).Verify(e => e.BooleanConvert(null), Times.Once);
        Mock.Get(Evaluator).Verify(e => e.Evaluate(null), Times.Once);
    }
    
    [Test]
    public void TestCombineShortCircuit()
    {
        _combinator.Combine(Evaluator, T.ListOf<object>(true, false, true)).Should().Be(false);
        Mock.Get(Evaluator).Verify(e => e.BooleanConvert(true), Times.Once);
        Mock.Get(Evaluator).Verify(e => e.Evaluate(true), Times.Once);
        Mock.Get(Evaluator).Verify(e => e.BooleanConvert(false), Times.Once);
        Mock.Get(Evaluator).Verify(e => e.Evaluate(false), Times.Once);
    }
    
    [Test]
    public void TestCombine()
    {
        _combinator.Combine(Evaluator, T.ListOf<object>(true, true)).Should().Be(true);
        _combinator.Combine(Evaluator, T.ListOf<object>(true, true, true)).Should().Be(true);
        
        _combinator.Combine(Evaluator, T.ListOf<object>(true, false)).Should().Be(false);
        _combinator.Combine(Evaluator, T.ListOf<object>(false, true)).Should().Be(false);
        _combinator.Combine(Evaluator, T.ListOf<object>(false, false)).Should().Be(false);
        _combinator.Combine(Evaluator, T.ListOf<object>(false, false, false)).Should().Be(false);
    }
}