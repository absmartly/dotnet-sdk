using System.Collections.Generic;

namespace ABSmartly.JsonExpressions.Operators;

// Todo: Tegu: cleanup
public class InOperator : BinaryOperator
{
    public override object Binary(IEvaluator evaluator, object haystack, object needle)
    {
        if (haystack is List<object> list) 
        {
            foreach (var listitem in list)
            {
                if (evaluator.Compare(listitem, needle) == 0)
                    return true;
            }

            return false;
        }

        if (haystack is string) 
        {
            var needleString = evaluator.StringConvert(needle);
            return needleString != null && ((string) haystack).Contains(needleString);
        }

        if (haystack is Dictionary<string, object>) 
        {
            var needleString = evaluator.StringConvert(needle);
            return needleString != null && ((Dictionary<string, object>) haystack).ContainsKey(needleString);
        }

        return null;
    }
}