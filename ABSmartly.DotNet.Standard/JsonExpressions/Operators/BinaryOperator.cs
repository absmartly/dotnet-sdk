using System.Collections.Generic;

namespace ABSmartly.JsonExpressions.Operators;

public abstract class BinaryOperator : IOperator
{
    public object Evaluate(IEvaluator evaluator, object args)
    {
        if (args is not List<object> objectList)
            return null;

        var lhs = objectList.Count > 0 ? evaluator.Evaluate(objectList[0]) : null;
        if (lhs is null)
            return null;

        // Todo: Tegu: review!?
        var rhs = objectList.Count > 1 ? evaluator.Evaluate(objectList[1]) : null;
        if (rhs is null)
            return null;

        return Binary(evaluator, lhs, rhs);
    }

    public abstract object Binary(IEvaluator evaluator, object lhs, object rhs);
}