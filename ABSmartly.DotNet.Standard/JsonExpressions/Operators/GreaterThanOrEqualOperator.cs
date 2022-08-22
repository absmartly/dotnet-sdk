namespace ABSmartlySdk.JsonExpressions.Operators;

public class GreaterThanOrEqualOperator : BinaryOperator
{
    public override object Binary(IEvaluator evaluator, object lhs, object rhs)
    {
        var result = evaluator.Compare(lhs, rhs);
        if (result is null)
            return null;

        return result >= 0;
    }
}