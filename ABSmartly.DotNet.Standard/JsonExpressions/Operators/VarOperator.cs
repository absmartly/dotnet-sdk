using System.Collections.Generic;

namespace ABSmartly.JsonExpressions.Operators;

public class VarOperator : IOperator
{
    public const string DictionaryKey = "path";

    public object Evaluate(IEvaluator evaluator, object path)
    {
        if (path is Dictionary<string, string> pathDictString)
            return ParseDictionaryString(evaluator, pathDictString);

        if (path is Dictionary<string, object> pathDictObject)
            return ParseDictionaryObject(evaluator, pathDictObject);

        if (path is string pathString)
            return evaluator.ExtractVariable(pathString);

        return null;
    }

    private static object ParseDictionaryString(IEvaluator evaluator, IReadOnlyDictionary<string, string> pathDictionary)
    {
        var dictResult = pathDictionary.TryGetValue(DictionaryKey, out var dictPath);
        if (dictResult != true)
            return null;

        var extractedVariable = evaluator.ExtractVariable(dictPath);
        return extractedVariable;
    }

    private static object ParseDictionaryObject(IEvaluator evaluator, IReadOnlyDictionary<string, object> pathDictionary)
    {
        var dictResult = pathDictionary.TryGetValue(DictionaryKey, out var dictPath);
        if (dictResult != true)
            return null;

        var extractedVariable = evaluator.ExtractVariable(dictPath.ToString());
        return extractedVariable;
    }
}