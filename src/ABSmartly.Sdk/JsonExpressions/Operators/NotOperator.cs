namespace ABSmartly.JsonExpressions.Operators;

public class NotOperator : UnaryOperator
{
    protected override object Unary(IEvaluator evaluator, object args) => !evaluator.BooleanConvert(args);
}