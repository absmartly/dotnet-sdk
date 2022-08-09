using System.Collections.Generic;

namespace ABSmartly.JsonExpressions.Operators;

public class AndCombinator : BooleanCombinator
{
    public override object Combine(IEvaluator evaluator, IList<object> expressions)
    {
        foreach (var expression in expressions)
        {
            var evaluateResult = evaluator.Evaluate(expression);
            var booleanConvertResult = evaluator.BooleanConvert(evaluateResult);
            if (!booleanConvertResult)
                return false;

            //if (!evaluator.BooleanConvert(evaluator.Evaluate(expression)))
            //    return false;
        }

        return true;
    }
}