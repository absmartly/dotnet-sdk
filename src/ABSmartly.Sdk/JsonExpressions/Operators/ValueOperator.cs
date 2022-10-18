namespace ABSmartly.JsonExpressions.Operators;

public class ValueOperator : IOperator
{
    public object Evaluate(IEvaluator evaluator, object value) => value;
}