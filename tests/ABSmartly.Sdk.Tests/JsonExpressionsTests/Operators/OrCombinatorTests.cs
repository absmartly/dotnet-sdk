using ABSmartly.JsonExpressions.Operators;

namespace ABSmartly.Sdk.Tests.JsonExpressionsTests.Operators;

[TestFixture]
public class OrCombinatorTests : OperatorTestBase
{
    private readonly OrCombinator _combinator = new();

    [Test]
    public void TestCombineTrue()
    {
        _combinator.Combine(Evaluator, T.ListOf(true)).Should().Be(true);
        Mock.Get(Evaluator).Verify(e => e.BooleanConvert(true), Times.Once);
        Mock.Get(Evaluator).Verify(e => e.Evaluate(true), Times.Once);
    }

    [Test]
    public void TestCombineFalse()
    {
        _combinator.Combine(Evaluator, T.ListOf(false)).Should().Be(false);
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
        _combinator.Combine(Evaluator, T.ListOf(true, false, true)).Should().Be(true);
        Mock.Get(Evaluator).Verify(e => e.BooleanConvert(true), Times.Once);
        Mock.Get(Evaluator).Verify(e => e.Evaluate(true), Times.Once);
        Mock.Get(Evaluator).Verify(e => e.BooleanConvert(false), Times.Never);
        Mock.Get(Evaluator).Verify(e => e.Evaluate(false), Times.Never);
    }

    [Test]
    public void TestCombine()
    {
        _combinator.Combine(Evaluator, T.ListOf(true, true)).Should().Be(true);
        _combinator.Combine(Evaluator, T.ListOf(true, true, true)).Should().Be(true);

        _combinator.Combine(Evaluator, T.ListOf(true, false)).Should().Be(true);
        _combinator.Combine(Evaluator, T.ListOf(false, true)).Should().Be(true);
        _combinator.Combine(Evaluator, T.ListOf(false, false)).Should().Be(false);
        _combinator.Combine(Evaluator, T.ListOf(false, false, false)).Should().Be(false);
    }
}