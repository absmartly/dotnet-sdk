using System.Collections.Generic;

namespace ABSmartly.JsonExpressions.Operators;

public class VarOperator : IOperator
{
    public object Evaluate(IEvaluator evaluator, object path)
    {
        var resolvedPath = path;

        if (resolvedPath is Dictionary<string, object> dictionary && dictionary.TryGetValue("path", out var pathValue))
            resolvedPath = pathValue;

        return resolvedPath is string pathString ? evaluator.ExtractVariable(pathString) : null;
    }
}