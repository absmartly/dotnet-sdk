namespace ABSmartly.JsonExpressions.Operators;

public class NullOperator : UnaryOperator
{
    public override object Unary(IEvaluator evaluator, object arg)
    {
        return arg == null;
    }
}