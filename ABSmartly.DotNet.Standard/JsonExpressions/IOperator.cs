namespace ABSmartlySdk.JsonExpressions;

public interface IOperator
{
    object Evaluate(IEvaluator evaluator, object args);
}