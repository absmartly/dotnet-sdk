namespace ABSmartly.JsonExpressions;

public interface IOperator
{
    object Evaluate(Evaluator evaluator, object arg);
}