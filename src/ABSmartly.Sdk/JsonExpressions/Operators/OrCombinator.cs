using System.Collections.Generic;

namespace ABSmartly.JsonExpressions.Operators;

public class OrCombinator : BooleanCombinator
{
    internal override object Combine(IEvaluator evaluator, IList<object> expressions)
    {
        foreach (var expr in expressions)
            if (evaluator.BooleanConvert(evaluator.Evaluate(expr)) is true)
                return true;

        return expressions.Count == 0;
    }
}