namespace ABSmartly.JsonExpressions.Operators;

public abstract class UnaryOperator : IOperator
{
    public object Evaluate(IEvaluator evaluator, object args)
    {
        var arg = evaluator.Evaluate(args);
        return Unary(evaluator, arg);
    }

    protected abstract object Unary(IEvaluator evaluator, object arg);
}