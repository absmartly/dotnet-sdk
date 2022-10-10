using System.Collections;
using System.Collections.Generic;

namespace ABSmartly.JsonExpressions.Operators;

public class InOperator : BinaryOperator
{
    public override object Binary(IEvaluator evaluator, object haystack, object needle)
    {
        if (haystack is string hsString)
            return HandleString(evaluator, hsString, needle);

        if (haystack is IList hsList)
            return HandleIList(evaluator, hsList, needle);

        if (haystack is IDictionary<string, object> hsDictionary)
            return HandleIDictionary(evaluator, hsDictionary, needle);

        return null;
    }

    private static object HandleString(IEvaluator evaluator, string hsString, object needle)
    {
        if (needle is null)
            return false;

        var needleString = evaluator.StringConvert(needle);
        if (string.IsNullOrWhiteSpace(needleString))
            return false;

        return hsString.Contains(needleString);
    }

    private static object HandleIList(IEvaluator evaluator, IList hsList, object needle)
    {
        foreach (var hsListItem in hsList)
            if (evaluator.Compare(hsListItem, needle) == 0)
                return true;

        return false;
    }

    private static object HandleIDictionary(IEvaluator evaluator, IDictionary<string, object> hsDictionary,
        object needle)
    {
        if (needle is null)
            return false;

        var needleString = evaluator.StringConvert(needle);
        if (string.IsNullOrWhiteSpace(needleString))
            return false;

        return hsDictionary.ContainsKey(needleString);
    }
}