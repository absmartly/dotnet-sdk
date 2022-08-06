using System.Collections.Generic;

namespace ABSmartly.JsonExpressions.Operators;

public class VarOperator : IOperator
{
    public object Evaluate(IEvaluator evaluator, object path)
    {
        object dictPath = null;

        if (path is Dictionary<string, object> dict)
        {
            var res = dict.TryGetValue("path", out dictPath);
            if (res != true)
                return null;
        }

        if (dictPath is not string dictPathString)
            return null;

        var extractedVariable = evaluator.ExtractVariable(dictPathString);
        return extractedVariable;
    }
}