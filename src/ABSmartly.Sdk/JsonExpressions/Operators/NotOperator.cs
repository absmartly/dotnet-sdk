namespace ABSmartly.JsonExpressions.Operators;

public class NotOperator : UnaryOperator
{
    protected override object Unary(IEvaluator evaluator, object args)
    {
        return !evaluator.BooleanConvert(args);
    }
}