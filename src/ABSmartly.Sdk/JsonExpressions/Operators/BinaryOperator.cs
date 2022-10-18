using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace ABSmartly.JsonExpressions.Operators;

public abstract class BinaryOperator : IOperator
{
    public object Evaluate(IEvaluator evaluator, object args)
    {
        if (args is not IList list)
            return null;

        var objectList = list as List<object> ?? list.Cast<object>().ToList();

        object lhs = null;
        if (objectList.Count > 0)
            lhs = evaluator.Evaluate(objectList[0]);

        if (lhs is null)
            return null;

        object rhs = null;
        if (objectList.Count > 1)
            rhs = evaluator.Evaluate(objectList[1]);

        if (rhs is null)
            return null;

        return Binary(evaluator, lhs, rhs);
    }

    protected abstract object Binary(IEvaluator evaluator, object lhs, object rhs);
}