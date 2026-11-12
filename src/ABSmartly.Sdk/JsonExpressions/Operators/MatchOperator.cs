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

        // No match timeout, as in 1.3.0 and the Java SDK: a deliberate owner decision.
        // Pattern operands can reference application attributes; see "Security
        // considerations" in the README for the trust assumptions.
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