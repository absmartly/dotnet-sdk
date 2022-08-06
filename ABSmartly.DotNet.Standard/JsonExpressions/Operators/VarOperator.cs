using System.Collections.Generic;

namespace ABSmartly.JsonExpressions.Operators;

public class VarOperator : IOperator
{
    public const string DictionaryKey = "path";

    public object Evaluate(IEvaluator evaluator, object path)
    {
        if (path is Dictionary<string, string> pathDictionary)
            return ParseDictionary(evaluator, pathDictionary);

        if (path is string pathString)
            return evaluator.ExtractVariable(pathString);

        return null;
    }

    private static object ParseDictionary(IEvaluator evaluator, IReadOnlyDictionary<string, string> pathDictionary)
    {
        var dictResult = pathDictionary.TryGetValue(DictionaryKey, out var dictPath);
        if (dictResult != true)
            return null;

        var extractedVariable = evaluator.ExtractVariable(dictPath);
        return extractedVariable;
    }
}