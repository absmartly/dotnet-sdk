using System.Collections.Generic;

namespace ABSmartly.JsonExpressions.Operators;

public class VarOperator : IOperator
{
    public const string DictionaryKey = "path";

    public object Evaluate(IEvaluator evaluator, object path)
    {
        var resolvedPath = path;

        if (resolvedPath is Dictionary<string, string> d1 && d1.TryGetValue(DictionaryKey, out var dPath1))
            resolvedPath = dPath1;

        if (resolvedPath is Dictionary<string, object> d2 && d2.TryGetValue(DictionaryKey, out var dPath2))
            resolvedPath = dPath2;

        if (resolvedPath is string pathString)
            return evaluator.ExtractVariable(pathString);

        return null;
    }
}