using System.Collections;
using System.Collections.Generic;

namespace ABSmartly.JsonExpressions.Operators;

public class VarOperator : IOperator
{
    public const string DictionaryKey = "path";

    public object Evaluate(IEvaluator evaluator, object path)
    {


        //if (path is Dictionary<string, string> pathDictionary)
        if (path is IDictionary)
            return ParseDictionary2(evaluator, path as IDictionary);

        if (path is string pathString)
            return evaluator.ExtractVariable(pathString);

        var type = path.GetType();

        return null;
    }

    private static object ParseDictionary2(IEvaluator evaluator, IDictionary pathDictionary)
    {
        var dict = pathDictionary as Dictionary<string, object>;
        var dictResult = dict.TryGetValue(DictionaryKey, out var dictPath);
        if (dictResult != true)
            return null;

        var extractedVariable = evaluator.ExtractVariable(dictPath.ToString());
        return extractedVariable;
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