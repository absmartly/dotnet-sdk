namespace ABSmartly.JsonExpressions.Operators;

public class NullOperator : UnaryOperator
{
    protected override object Unary(IEvaluator evaluator, object arg)
    {
        return arg == null;
    }
}