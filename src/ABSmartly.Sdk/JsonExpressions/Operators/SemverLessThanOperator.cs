namespace ABSmartly.JsonExpressions.Operators;

public class SemverLessThanOperator : BinaryOperator
{
    protected override object Binary(IEvaluator evaluator, object lhs, object rhs)
    {
        return SemverComparator.Compare(evaluator, lhs, rhs) is { } result ? result < 0 : null;
    }
}
