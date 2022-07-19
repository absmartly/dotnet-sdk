using System.Collections.Generic;

namespace ABSmartly.JsonExpressions.Operators;

public class OrCombinator : BooleanCombinator
{
    public override object Combine(IEvaluator evaluator, List<object> exprs)
    {
        foreach (var expr in exprs)
        {
            if (evaluator.BooleanConvert(evaluator.Evaluate(expr)))
                return true;
        }

        return exprs.Count == 0;
    }
}