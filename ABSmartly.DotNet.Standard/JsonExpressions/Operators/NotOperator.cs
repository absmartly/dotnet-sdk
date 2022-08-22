namespace ABSmartlySdk.JsonExpressions.Operators;

public class NotOperator : UnaryOperator
{
    public override object Unary(IEvaluator evaluator, object args)
    {
        return !evaluator.BooleanConvert(args);
    }
}