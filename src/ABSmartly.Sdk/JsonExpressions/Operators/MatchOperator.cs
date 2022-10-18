using System;
using System.Text.RegularExpressions;

namespace ABSmartly.JsonExpressions.Operators;

public class MatchOperator : BinaryOperator
{
    protected override object Binary(IEvaluator evaluator, object lhs, object rhs)
    {
        var text = evaluator.StringConvert(lhs);
        if (string.IsNullOrEmpty(text))
            return null;

        var pattern = evaluator.StringConvert(rhs);
        if (string.IsNullOrEmpty(pattern))
            return null;

        try
        {
            var match = Regex.Match(text, pattern);
            return match.Success;
        }
        catch (Exception)
        {
            return null;
        }
    }
}