using System.Collections.Generic;
using System.Linq;

namespace ABSmartly.JsonExpressions.Operators;

public class AndCombinator : BooleanCombinator
{
    internal override object Combine(IEvaluator evaluator, IList<object> expressions) =>
        expressions
            .Select(evaluator.Evaluate).Select(evaluator.BooleanConvert)
            .All(booleanConvertResult => booleanConvertResult is true);
}