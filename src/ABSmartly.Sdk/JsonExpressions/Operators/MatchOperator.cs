using System;
using System.Text.RegularExpressions;

namespace ABSmartly.JsonExpressions.Operators;

public class MatchOperator : BinaryOperator
{
    protected override object Binary(IEvaluator evaluator, object lhs, object rhs)
    {
        var text = evaluator.StringConvert(lhs);
        if (text == null) return null;

        var pattern = evaluator.StringConvert(rhs);
        if (pattern == null) return null;

        try
        {
            var match = Regex.Match(text, pattern, RegexOptions.None, TimeSpan.FromMilliseconds(100));
            return match.Success;
        }
        catch (RegexMatchTimeoutException)
        {
            return null;
        }
        catch (ArgumentException)
        {
            return null;
        }
    }
}