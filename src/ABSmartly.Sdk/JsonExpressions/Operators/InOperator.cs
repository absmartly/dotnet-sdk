using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace ABSmartly.JsonExpressions.Operators;

public class InOperator : BinaryOperator
{
    protected override object Binary(IEvaluator evaluator, object haystack, object needle) =>
        haystack switch
        {
            string hsString => HandleString(evaluator, hsString, needle),
            IList hsList => HandleIList(evaluator, hsList, needle),
            IDictionary<string, object> hsDictionary => HandleIDictionary(evaluator, hsDictionary, needle),
            _ => null
        };

    private static object HandleString(IEvaluator evaluator, string hsString, object needle) => evaluator.StringConvert(needle) is { } needleString && hsString.Contains(needleString);

    private static object HandleIList(IEvaluator evaluator, IList hsList, object needle)
    {
        return hsList.Cast<object>().Any(hsListItem => evaluator.Compare(hsListItem, needle) == 0);
    }

    private static object HandleIDictionary(IEvaluator evaluator, IDictionary<string, object> hsDictionary, object needle)
    {
        if (needle is null)
            return false;

        var needleString = evaluator.StringConvert(needle);
        if (string.IsNullOrWhiteSpace(needleString))
            return false;

        return hsDictionary.ContainsKey(needleString);
    }
}