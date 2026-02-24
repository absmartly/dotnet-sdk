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

        object lhs = objectList.Count > 0 ? evaluator.Evaluate(objectList[0]) : null;
        object rhs = objectList.Count > 1 ? evaluator.Evaluate(objectList[1]) : null;

        if (lhs is null && rhs is null)
            return true;

        if (lhs is null || rhs is null)
            return null;

        return evaluator.Compare(lhs, rhs) is { } result ? result == 0 : null;
    }
}
