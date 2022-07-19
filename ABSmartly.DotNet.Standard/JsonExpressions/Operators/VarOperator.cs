using System.Collections.Generic;

namespace ABSmartly.JsonExpressions.Operators;

public class VarOperator : IOperator
{
    public object Evaluate(IEvaluator evaluator, object path)
    {
        object dictPath = null;

        if (path is Dictionary<string, object> dict)
        {
            dict.TryGetValue("path", out dictPath);
        }

        // Todo: Tegu: cleanup
        if (dictPath is string)
            return evaluator.ExtractVariable(dictPath as string);
        return null;
    }
}