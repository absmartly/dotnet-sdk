using ABSmartly.JsonExpressions;
using ABSmartly.JsonExpressions.Operators;

namespace ABSmartly.DotNet.Standard.UnitTests.JsonExpressionsTests.OperatorsTests.TestImplementations;

public class BinaryOperatorTestImplementation : BinaryOperator
{
    public override object Binary(IEvaluator evaluator, object lhs, object rhs)
    {
        return "Binary Pass";
    }
}