using System;

namespace ABSmartly.JsonExpressions.Operators;

public class MatchOperator : BinaryOperator
{
    // Todo: Tegu: cleanup
    public override object Binary(IEvaluator evaluator, object lhs, object rhs)
    {
        var text = evaluator.StringConvert(lhs);
        if (text == null) 
            return null;
        
        var pattern = evaluator.StringConvert(rhs);
        if (pattern == null) 
            return null;
            
        try
        {
            var compiled = Pattern.Compile(pattern);
            var matcher = compiled.matcher(text);
            return matcher.find();
        }
        catch (Exception e)
        {
        }

        return null;
    }
}