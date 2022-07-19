namespace ABSmartly.JsonExpressions.Operators;

public class GreaterThanOrEqualOperator : BinaryOperator
{
    public override object Binary(IEvaluator evaluator, object lhs, object rhs)
    {
        var result = evaluator.Compare(lhs, rhs);
        return (result is not null) ? (result >= 0) : null;
    }
}