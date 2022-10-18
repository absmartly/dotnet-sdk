namespace ABSmartly.JsonExpressions.Operators;

public class LessThanOperator : BinaryOperator
{
    protected override object Binary(IEvaluator evaluator, object lhs, object rhs) =>
        evaluator.Compare(lhs, rhs) is { } result ? result < 0 : null;
}