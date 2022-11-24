namespace ABSmartly.JsonExpressions.Operators;

public class GreaterThanOrEqualOperator : BinaryOperator
{
    protected override object Binary(IEvaluator evaluator, object lhs, object rhs)
    {
        return evaluator.Compare(lhs, rhs) is { } result ? result >= 0 : null;
    }
}