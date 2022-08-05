using System.Collections.Generic;

namespace ABSmartly.JsonExpressions.Operators;

public class AndCombinator : BooleanCombinator
{
    public override object Combine(IEvaluator evaluator, IList<object> expressions)
    {
        foreach (var expression in expressions)
        {
            if (!evaluator.BooleanConvert(evaluator.Evaluate(expression)))
                return false;
        }

        return true;
    }
}