using ABSmartly.JsonExpressions;

namespace ABSmartly.Sdk.Tests.JsonExpressionsTests.Operators;

public abstract class OperatorTestBase
{
    protected IEvaluator Evaluator;

    [SetUp]
    public void SetUp()
    {
        Evaluator = Mock.Of<IEvaluator>();

        Mock.Get(Evaluator).Setup(e => e.Evaluate(It.IsAny<object>())).Returns<object>(x => x);
        Mock.Get(Evaluator).Setup(e => e.BooleanConvert(It.IsAny<object>())).Returns<object>(x => x != null && Convert.ToBoolean(x));
        Mock.Get(Evaluator).Setup(e => e.NumberConvert(It.IsAny<object>())).Returns<object>(x => Convert.ToDouble(x));
        Mock.Get(Evaluator).Setup(e => e.StringConvert(It.IsAny<object>())).Returns<object>(x => x.ToString() ?? string.Empty);
        Mock.Get(Evaluator).Setup(e => e.ExtractVariable("a/b/c")).Returns("abc");
        Mock.Get(Evaluator).Setup(e => e.Compare(It.IsAny<object>(), It.IsAny<object>())).Returns<object, object>(
            (lhs, rhs) =>
            {
                if (lhs is bool lhsBool)
                    return (lhsBool ? 1 : 0) - ((bool)rhs ? 1 : 0);
                if (lhs is double lhsDouble)
                    return Math.Sign(lhsDouble - (double)rhs);
                if (lhs is string lhsString)
                    return string.Compare(lhsString, (string)rhs, StringComparison.Ordinal);
                if (lhs.Equals(rhs))
                    return 0;
                return null;
            });
    }
}