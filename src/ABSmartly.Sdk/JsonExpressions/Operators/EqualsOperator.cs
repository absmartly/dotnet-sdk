using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace ABSmartly.JsonExpressions.Operators;

public class EqualsOperator : IOperator
{
    public object Evaluate(IEvaluator evaluator, object args)
    {
        if (args is not IList list)
            return null;

        var objectList = list as List<object> ?? list.Cast<object>().ToList();

        if (objectList.Count < 2)
            return null;

        object lhs = evaluator.Evaluate(objectList[0]);
        object rhs = evaluator.Evaluate(objectList[1]);

        if (lhs is null && rhs is null)
            return true;

        if (lhs is null || rhs is null)
            return null;

        return evaluator.Compare(lhs, rhs) is { } result ? result == 0 : null;
    }
}
