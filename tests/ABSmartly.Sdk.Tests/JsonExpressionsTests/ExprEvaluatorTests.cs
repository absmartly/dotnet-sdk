using ABSmartly.JsonExpressions;


namespace ABSmartly.Sdk.Tests.JsonExpressionsTests;

[TestFixture]
public class ExprEvaluatorTests
{
    [Test]
    public void EvaluateConsidersListAsAndCombinator()
    {
        var andOp = Mock.Of<IOperator>();
        var orOp = Mock.Of<IOperator>();
        Mock.Get(andOp).Setup(x => x.Evaluate(It.IsAny<IEvaluator>(), It.IsAny<object>())).Returns(true);

        var evaluator = new ExprEvaluator(Dict(("and", andOp), ("or", orOp)), Dict<object>());
        var args = new List<Dictionary<string, object>> { Dict("value", true), Dict("value", false) };

        evaluator.Evaluate(args).Should().NotBeNull();

        Mock.Get(orOp).Verify(x => x.Evaluate(It.IsAny<IEvaluator>(), It.IsAny<object>()), Times.Never);
        Mock.Get(andOp).Verify(x => x.Evaluate(evaluator, args), Times.Once);
    }
    
    [Test]
    public void EvaluateReturnsNullIfOperatorNotFound()
    {
        var valueOp = Mock.Of<IOperator>();
        Mock.Get(valueOp).Setup(x => x.Evaluate(It.IsAny<IEvaluator>(), It.IsAny<object>())).Returns(true);

        var evaluator = new ExprEvaluator(Dict(("value", valueOp)), Dict<object>());
        var args = Dict("not_found", true);

        evaluator.Evaluate(args).Should().BeNull();

        Mock.Get(valueOp).Verify(x => x.Evaluate(It.IsAny<IEvaluator>(), It.IsAny<object>()), Times.Never);
    }
    
    [Test]
    public void EvaluateCallsOperatorWithArgs()
    {
        var valueOp = Mock.Of<IOperator>();
        var args = new List<int>{1, 2, 3};
        
        Mock.Get(valueOp).Setup(x => x.Evaluate(It.IsAny<IEvaluator>(), args)).Returns(args);

        var evaluator = new ExprEvaluator(Dict(("value", valueOp)), Dict<object>());

        evaluator.Evaluate(Dict("value", args)).Should().BeSameAs(args);

        Mock.Get(valueOp).Verify(x => x.Evaluate(It.IsAny<IEvaluator>(), args), Times.Once);
    }

    [Test]
    public void TestBooleanConvert()
    {
        var e = new ExprEvaluator(Dict<IOperator>(), Dict<object>());
            
        e.BooleanConvert(new Dictionary<string, object>()).Should().BeTrue();
        e.BooleanConvert(new List<object>()).Should().BeTrue();
        e.BooleanConvert(null).Should().BeFalse();
        
        e.BooleanConvert(true).Should().BeTrue();
        e.BooleanConvert(1).Should().BeTrue();
        e.BooleanConvert(2).Should().BeTrue();
        e.BooleanConvert("abc").Should().BeTrue();
        e.BooleanConvert("1").Should().BeTrue();
        
        e.BooleanConvert(false).Should().BeFalse();
        e.BooleanConvert(0).Should().BeFalse();
        e.BooleanConvert("").Should().BeFalse();
        e.BooleanConvert("0").Should().BeFalse();
        e.BooleanConvert("false").Should().BeFalse();
    }

    [Test]
    public void TestNumberConvert()
    {
        var e = new ExprEvaluator(Dict<IOperator>(), Dict<object>());

        e.NumberConvert(new Dictionary<string, object>()).Should().BeNull();
        e.NumberConvert(new List<object>()).Should().BeNull();
        e.NumberConvert(null).Should().BeNull();
        e.NumberConvert("").Should().BeNull();
        e.NumberConvert("abcd").Should().BeNull();
        e.NumberConvert("x1234").Should().BeNull();
        
        e.NumberConvert(true).Should().Be(1.0);
        e.NumberConvert(false).Should().Be(0.0);
        
        e.NumberConvert(-1.0).Should().Be(-1.0);
        e.NumberConvert(0.0).Should().Be(0.0);
        e.NumberConvert(1.0).Should().Be(1.0);
        e.NumberConvert(1.5).Should().Be(1.5);
        e.NumberConvert(2.0).Should().Be(2.0);
        e.NumberConvert(3.0).Should().Be(3.0);
        
        e.NumberConvert(-1).Should().Be(-1.0);
        e.NumberConvert(0).Should().Be(0.0);
        e.NumberConvert(1).Should().Be(1.0);
        e.NumberConvert(2).Should().Be(2.0);
        e.NumberConvert(3).Should().Be(3.0);
        e.NumberConvert(int.MaxValue).Should().Be(2147483647.0);
        e.NumberConvert(-int.MaxValue).Should().Be(-2147483647.0);
        e.NumberConvert(9007199254740991L).Should().Be(9007199254740991.0);
        e.NumberConvert(-9007199254740991L).Should().Be(-9007199254740991.0);
        
        e.NumberConvert("-1.0").Should().Be(-1.0);
        e.NumberConvert("0.0").Should().Be(0.0);
        e.NumberConvert("1.0").Should().Be(1.0);
        e.NumberConvert("1.5").Should().Be(1.5);
        e.NumberConvert("2.0").Should().Be(2.0);
        e.NumberConvert("3.0").Should().Be(3.0);
    }

    [Test]
    public void TestStringConvert()
    {
        var e = new ExprEvaluator(Dict<IOperator>(), Dict<object>());

        e.StringConvert(null).Should().BeNull();
        e.StringConvert(new Dictionary<string, object>()).Should().BeNull();
        e.StringConvert(new List<object>()).Should().BeNull();
        
        e.StringConvert(true).Should().Be("true");
        e.StringConvert(false).Should().Be("false");
        
        e.StringConvert("").Should().Be("");
        e.StringConvert("abc").Should().Be("abc");
        
        e.StringConvert(-1.0).Should().Be("-1");
        e.StringConvert(0.0).Should().Be("0");
        e.StringConvert(1.0).Should().Be("1");
        e.StringConvert(1.5).Should().Be("1.5");
        e.StringConvert(2.0).Should().Be("2");
        e.StringConvert(3.0).Should().Be("3");
        e.StringConvert(2147483647.0).Should().Be("2147483647");
        e.StringConvert(-2147483647.0).Should().Be("-2147483647");
        e.StringConvert(9007199254740991.0).Should().Be("9007199254740991");
        e.StringConvert(-9007199254740991.0).Should().Be("-9007199254740991");
        e.StringConvert(0.9007199254740991).Should().Be("0.9007199254740991");
        e.StringConvert(-0.9007199254740991).Should().Be("-0.9007199254740991");
        
        e.StringConvert(-1).Should().Be("-1");
        e.StringConvert(0).Should().Be("0");
        e.StringConvert(1).Should().Be("1");
        e.StringConvert(2).Should().Be("2");
        e.StringConvert(3).Should().Be("3");
        e.StringConvert(2147483647).Should().Be("2147483647");
        e.StringConvert(-2147483647).Should().Be("-2147483647");
        e.StringConvert(9007199254740991L).Should().Be("9007199254740991");
        e.StringConvert(-9007199254740991L).Should().Be("-9007199254740991");
    }

    [Test]
    public void TestExtractVar()
    {
        var vars = new Dictionary<string, object>
        {
            ["a"] = 1,
            ["b"] = true,
            ["c"] = false,
            ["d"] = new List<int> { 1, 2, 3 },
            ["e"] = new List<object> { 1, Dict("z", 2), 3 },
            ["f"] = Dict("y", Dict<object>(("x", 3), ("0", 10))),
        };
        
        var e = new ExprEvaluator(Dict<IOperator>(), vars);

        e.ExtractVariable("a").Should().Be(1);
        e.ExtractVariable("b").Should().Be(true);
        e.ExtractVariable("c").Should().Be(false);
        e.ExtractVariable("d").Should().BeEquivalentTo(new List<int> { 1, 2, 3 });
        e.ExtractVariable("e").Should().BeEquivalentTo(new List<object> { 1, Dict("z", 2), 3 });
        e.ExtractVariable("f").Should().BeEquivalentTo(Dict("y", Dict(("x", 3), ("0", 10))));
        
        e.ExtractVariable("a/0").Should().Be(null);
        e.ExtractVariable("a/b").Should().Be(null);
        e.ExtractVariable("b/0").Should().Be(null);
        e.ExtractVariable("b/e").Should().Be(null);
        
        e.ExtractVariable("d/0").Should().Be(1);
        e.ExtractVariable("d/1").Should().Be(2);
        e.ExtractVariable("d/2").Should().Be(3);
        e.ExtractVariable("d/3").Should().Be(null);
        
        e.ExtractVariable("e/0").Should().Be(1);
        e.ExtractVariable("e/1/z").Should().Be(2);
        e.ExtractVariable("e/2").Should().Be(3);
        e.ExtractVariable("e/1/0").Should().Be(null);
        
        e.ExtractVariable("f/y").Should().BeEquivalentTo(Dict(("x", 3), ("0", 10)));
        e.ExtractVariable("f/y/x").Should().Be(3);
        e.ExtractVariable("f/y/0").Should().Be(10);
    }

    [Test]
    public void TestCompareNull()
    {
        var e = new ExprEvaluator(Dict<IOperator>(), Dict<object>());

        e.Compare(null, null).Should().Be(0);
        
        e.Compare(null, 0).Should().BeNull();
        e.Compare(null, 1).Should().BeNull();
        e.Compare(null, true).Should().BeNull();
        e.Compare(null, false).Should().BeNull();
        e.Compare(null, "").Should().BeNull();
        e.Compare(null, "abc").Should().BeNull();
        e.Compare(null, new Dictionary<string, object>()).Should().BeNull();
        e.Compare(null, new List<object>()).Should().BeNull();
    }

    [Test]
    public void TestCompareObjects()
    {
        var e = new ExprEvaluator(Dict<IOperator>(), Dict<object>());

        e.Compare(Dict<object>(), 0).Should().BeNull();
        e.Compare(Dict<object>(), 1).Should().BeNull();
        e.Compare(Dict<object>(), true).Should().BeNull();
        e.Compare(Dict<object>(), false).Should().BeNull();
        e.Compare(Dict<object>(), "").Should().BeNull();
        e.Compare(Dict<object>(), "abc").Should().BeNull();
        e.Compare(Dict<object>(), new List<object>()).Should().BeNull();
        e.Compare(Dict("a", 1), Dict("b", 1)).Should().BeNull();
        e.Compare(Dict<object>(), Dict<object>()).Should().Be(0);
        e.Compare(Dict("a", 1), Dict("a", 1)).Should().Be(0);
        e.Compare(Dict("a", Dict("b", 1)), Dict("a", Dict("b", 1))).Should().Be(0);
        e.Compare(Dict("a", new List<object>{1, 2}), Dict("a", new List<object>{1, 2})).Should().Be(0);
                
        e.Compare(new List<object>(), 0).Should().BeNull();
        e.Compare(new List<object>(), 1).Should().BeNull();
        e.Compare(new List<object>(), true).Should().BeNull();
        e.Compare(new List<object>(), false).Should().BeNull();
        e.Compare(new List<object>(), "").Should().BeNull();
        e.Compare(new List<object>(), "abc").Should().BeNull();
        e.Compare(new List<object>(), Dict<object>()).Should().BeNull();
        e.Compare(new List<object>{1, 2}, new List<object>{3, 4}).Should().BeNull();
        e.Compare(new List<object>(), new List<object>()).Should().Be(0);
        e.Compare(new List<object>{1, 2}, new List<object>{1, 2}).Should().Be(0);
        e.Compare(new List<object>{new List<object>{1, 2}}, new List<object>{new List<object>{1, 2}}).Should().Be(0);
        e.Compare(new List<object>{Dict("a", 1)}, new List<object>{Dict("a", 1)}).Should().Be(0);
    }

    [Test]
    public void TestCompareBooleans()
    {
        var e = new ExprEvaluator(Dict<IOperator>(), Dict<object>());

        e.Compare(false, 0).Should().Be(0);
        e.Compare(false, 1).Should().Be(-1);
        e.Compare(false, false).Should().Be(0);
        e.Compare(false, true).Should().Be(-1);
        e.Compare(false, "").Should().Be(0);
        e.Compare(false, "abc").Should().Be(-1);
        e.Compare(false, Dict<object>()).Should().Be(-1);
        e.Compare(false, new List<object>()).Should().Be(-1);
        
        e.Compare(true, 0).Should().Be(1);
        e.Compare(true, 1).Should().Be(0);
        e.Compare(true, false).Should().Be(1);
        e.Compare(true, true).Should().Be(0);
        e.Compare(true, "").Should().Be(1);
        e.Compare(true, "abc").Should().Be(0);
        e.Compare(true, Dict<object>()).Should().Be(0);
        e.Compare(true, new List<object>()).Should().Be(0);
    }

    [Test]
    public void TestCompareNumbers()
    {
        var e = new ExprEvaluator(Dict<IOperator>(), Dict<object>());

        e.Compare(0, 0).Should().Be(0);
        e.Compare(0, 1).Should().Be(-1);
        e.Compare(0, false).Should().Be(0);
        e.Compare(0, true).Should().Be(-1);
        e.Compare(0, "").Should().BeNull();
        e.Compare(0, "abc").Should().BeNull();
        e.Compare(0, Dict<object>()).Should().BeNull();
        e.Compare(0, new List<object>()).Should().BeNull();
        
        e.Compare(1, 0).Should().Be(1);
        e.Compare(1, 1).Should().Be(0);
        e.Compare(1, false).Should().Be(1);
        e.Compare(1, true).Should().Be(0);
        e.Compare(1, "").Should().BeNull();
        e.Compare(1, "abc").Should().BeNull();
        e.Compare(1, Dict<object>()).Should().BeNull();
        e.Compare(1, new List<object>()).Should().BeNull();
        
        e.Compare(1.0, 1).Should().Be(0);
        e.Compare(1.5, 1).Should().Be(1);
        e.Compare(2.0, 1).Should().Be(1);
        e.Compare(3.0, 1).Should().Be(1);
        
        e.Compare(1, 1.0).Should().Be(0);
        e.Compare(1, 1.5).Should().Be(-1);
        e.Compare(1, 2.0).Should().Be(-1);
        e.Compare(1, 3.0).Should().Be(-1);
        
        e.Compare(9007199254740991L, 9007199254740991L).Should().Be(0);
        e.Compare(0, 9007199254740991L).Should().Be(-1);
        e.Compare(9007199254740991L, 0).Should().Be(1);
        
        e.Compare(9007199254740991.0, 9007199254740991.0).Should().Be(0);
        e.Compare(0.0, 9007199254740991.0).Should().Be(-1);
        e.Compare(9007199254740991.0, 0.0).Should().Be(1);
    }
    
    [Test]
    public void TestCompareStrings()
    {
        var e = new ExprEvaluator(Dict<IOperator>(), Dict<object>());

        e.Compare("", "").Should().Be(0);
        e.Compare("abc", "abc").Should().Be(0);
        e.Compare("0", 0).Should().Be(0);
        e.Compare("1", 1).Should().Be(0);
        e.Compare("true", true).Should().Be(0);
        e.Compare("false", false).Should().Be(0);
        e.Compare("", Dict<object>()).Should().BeNull();
        e.Compare("abc", Dict<object>()).Should().BeNull();
        e.Compare("", new List<object>()).Should().BeNull();
        e.Compare("abc", new List<object>()).Should().BeNull();
        
        e.Compare("abc", "bcd").Should().Be(-1);
        e.Compare("bcd", "abc").Should().Be(1);
        e.Compare("0", "1").Should().Be(-1);
        e.Compare("1", "0").Should().Be(1);
        e.Compare("9", "100").Should().Be(8);
        e.Compare("100", "9").Should().Be(-8);
    }
    
    #region Helper & Test Data

    private static Dictionary<string, object> Dict(string op, object arg) => new() { { op, arg } };

    private static Dictionary<string, T> Dict<T>(params (string op, T arg)[] inputs)
    {
        var dict = new Dictionary<string, T>();
        foreach (var (op, arg) in inputs)
        {
            dict[op] = arg;
        }

        return dict;
    }

    #endregion
}