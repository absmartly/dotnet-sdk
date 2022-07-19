using System.Collections.Generic;

namespace ABSmartly.JsonExpressions.Operators;

public abstract class BooleanCombinator : IOperator
{
    public object Evaluate(IEvaluator evaluator, object args)
    {
        if (args is not List<object> objectList)
            return null;

        return Combine(evaluator, objectList);
    }

    public abstract object Combine(IEvaluator evaluator, List<object> args);
}