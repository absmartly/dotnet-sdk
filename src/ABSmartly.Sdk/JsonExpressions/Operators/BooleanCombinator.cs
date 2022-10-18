using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace ABSmartly.JsonExpressions.Operators;

public abstract class BooleanCombinator : IOperator
{
    public object Evaluate(IEvaluator evaluator, object args)
    {
        if (args is not IList list)
            return null;

        var objectList = list as List<object> ?? list.Cast<object>().ToList();

        return Combine(evaluator, objectList);
    }

    internal abstract object Combine(IEvaluator evaluator, IList<object> args);
}