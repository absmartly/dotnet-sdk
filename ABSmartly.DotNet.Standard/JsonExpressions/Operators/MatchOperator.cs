using System;
using System.Diagnostics;
using System.Text.RegularExpressions;

namespace ABSmartly.JsonExpressions.Operators;

public class MatchOperator : BinaryOperator
{
    public override object Binary(IEvaluator evaluator, object lhs, object rhs)
    {
        var text = evaluator.StringConvert(lhs);
        if (string.IsNullOrWhiteSpace(text)) 
            return null;
        
        var pattern = evaluator.StringConvert(rhs);
        if (pattern is null) 
            return null;
            
        try
        {
            var match = Regex.Match(text, pattern);
            return match.Success;
        }
        catch (Exception e)
        {
            Debug.WriteLine(e);
            return null;
        }
    }
}