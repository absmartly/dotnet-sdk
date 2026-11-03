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

        // No match timeout, as in 1.3.0 and the Java SDK: patterns come from experiment
        // definitions authored on the Web Console (trusted configuration). This is a
        // deliberate owner decision; see "Security considerations" in the README.
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