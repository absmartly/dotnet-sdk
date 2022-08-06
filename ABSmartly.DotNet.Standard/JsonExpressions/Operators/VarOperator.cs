using System.Collections.Generic;

namespace ABSmartly.JsonExpressions.Operators;

public class VarOperator : IOperator
{
    public object Evaluate(IEvaluator evaluator, object path)
    {
        if (path is Dictionary<string, object> pathDictionary)
            return ParseDictionary(evaluator, pathDictionary);

        if (path is string pathString)
            return evaluator.ExtractVariable(pathString);

        return null;
    }

    private static object ParseDictionary(IEvaluator evaluator, IReadOnlyDictionary<string, object> pathDictionary)
    {
        var dictResult = pathDictionary.TryGetValue("path", out var dictPath);
        if (dictResult != true)
            return null;

        if (dictPath is not string dictPathString)
            return null;

        var extractedVariable = evaluator.ExtractVariable(dictPathString);
        return extractedVariable;
    }
}