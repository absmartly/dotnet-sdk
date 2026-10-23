using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;

namespace ABSmartly.JsonExpressions;

public class ExprEvaluator : IEvaluator
{
    private readonly Dictionary<string, IOperator> _operators;
    private readonly Dictionary<string, object> _vars;

    public ExprEvaluator(Dictionary<string, IOperator> operators, Dictionary<string, object> vars)
    {
        _vars = vars;
        _operators = operators;
    }

    public object Evaluate(object expression)
    {
        switch (expression)
        {
            case IList:
                return _operators.TryGetValue("and", out var andOperator)
                    ? andOperator.Evaluate(this, expression)
                    : null;
            case Dictionary<string, object> exprDictionary:
            {
                foreach (var kvp in exprDictionary)
                {
                    if (_operators.TryGetValue(kvp.Key, out var op))
                        return op.Evaluate(this, kvp.Value);

                    break;
                }

                break;
            }
        }

        return null;
    }

    public bool? BooleanConvert(object x)
    {
        return x switch
        {
            bool boolValue => boolValue,
            string stringValue => !stringValue.ToLower().Equals("false") && !stringValue.Equals("0") &&
                                  !stringValue.Equals(""),
            { } when GetNumber(x) is { } doubleValue => doubleValue != 0,
            _ => x != null
        };
    }

    public double? NumberConvert(object x)
    {
        return x switch
        {
            bool boolValue => boolValue ? 1 : 0,
            string stringValue when double.TryParse(stringValue, NumberStyles.Number, CultureInfo.InvariantCulture,
                out var result) => result,
            { } when GetNumber(x) is { } doubleValue => doubleValue,
            _ => null
        };
    }

    public string StringConvert(object x)
    {
        return x switch
        {
            bool boolValue => boolValue.ToString().ToLowerInvariant(),
            string stringValue => stringValue,
            { } when GetNumber(x) is { } doubleValue => doubleValue.ToString("G16",
                new NumberFormatInfo { NumberDecimalSeparator = "." }),
            _ => null
        };
    }

    public object ExtractVariable(string path)
    {
        var frags = path.Split('/');
        object target = _vars ?? new Dictionary<string, object>();

        foreach (var frag in frags)
        {
            var value = target switch
            {
                IList targetList when int.TryParse(frag, out var index) && targetList.Count > index
                    => targetList[index],
                IDictionary<string, object> targetDictionary when targetDictionary.TryGetValue(frag, out var v)
                    => v,
                _ => null
            };

            if (value is null) return null;

            target = Normalize(value);
        }

        return target;
    }

    public int? Compare(object lhs, object rhs)
    {
        lhs = Normalize(lhs);
        rhs = Normalize(rhs);

        if (lhs is null)
            return rhs is null ? 0 : null;

        if (rhs is null) return null;

        if (GetNumber(lhs) is { } lhsDoubleValue)
        {
            var rhsDoubleValue = NumberConvert(rhs);
            if (rhsDoubleValue is null) return null;

            return lhsDoubleValue.CompareTo(rhsDoubleValue);
        }

        if (lhs is string lhsStringValue)
        {
            var rhsStringValue = StringConvert(rhs);
            if (rhsStringValue is null) return null;

            return string.Compare(lhsStringValue, rhsStringValue, StringComparison.Ordinal);
        }

        if (lhs is bool lhsBoolValue)
        {
            var rhsBoolValue = BooleanConvert(rhs);
            if (rhsBoolValue is null) return null;
            return lhsBoolValue.CompareTo(rhsBoolValue);
        }

        if (lhs is Dictionary<string, object> || lhs is List<object>)
            return DeepEquals(lhs, rhs) ? 0 : null;

        if (lhs.GetType() == rhs.GetType())
            return lhs.Equals(rhs) ? 0 : null;

        return null;
    }

    private static bool DeepEquals(object lhs, object rhs)
    {
        lhs = Normalize(lhs);
        rhs = Normalize(rhs);

        if (lhs is null || rhs is null) return lhs is null && rhs is null;

        if (GetNumber(lhs) is { } lhsNumber)
            return GetNumber(rhs) is { } rhsNumber &&
                   (lhsNumber.Equals(rhsNumber) || (double.IsNaN(lhsNumber) && double.IsNaN(rhsNumber)));

        switch (lhs)
        {
            case List<object> lhsList:
                return rhs is List<object> rhsList && lhsList.Count == rhsList.Count &&
                       !lhsList.Where((t, i) => !DeepEquals(t, rhsList[i])).Any();
            case Dictionary<string, object> lhsDict:
                return rhs is Dictionary<string, object> rhsDict && lhsDict.Count == rhsDict.Count &&
                       lhsDict.All(kv => rhsDict.TryGetValue(kv.Key, out var v) && DeepEquals(kv.Value, v));
            default:
                return lhs.GetType() == rhs.GetType() && lhs.Equals(rhs);
        }
    }

    private static object Normalize(object value)
    {
        switch (value)
        {
            case JsonElement element:
                return element.ValueKind switch
                {
                    JsonValueKind.Object => element.EnumerateObject()
                        .ToDictionary(p => p.Name, p => Normalize(p.Value)),
                    JsonValueKind.Array => element.EnumerateArray().Select(e => Normalize(e)).ToList(),
                    JsonValueKind.String => element.GetString(),
                    JsonValueKind.Number => element.TryGetInt64(out var l) ? l : element.GetDouble(),
                    JsonValueKind.True => true,
                    JsonValueKind.False => false,
                    _ => null
                };
            case Dictionary<string, object>:
            case List<object>:
            case string:
                return value;
            case IDictionary dict:
            {
                var result = new Dictionary<string, object>();
                foreach (DictionaryEntry entry in dict)
                    result[Convert.ToString(entry.Key, CultureInfo.InvariantCulture)] = entry.Value;
                return result;
            }
            case IEnumerable enumerable:
                return enumerable.Cast<object>().ToList();
            default:
                return value;
        }
    }

    private static double? GetNumber(object input)
    {
        return input switch
        {
            double d => d,
            float f => f,
            byte => Convert.ToDouble(input),
            decimal => Convert.ToDouble(input),
            short => Convert.ToDouble(input),
            int => Convert.ToDouble(input),
            long => Convert.ToDouble(input),
            ushort => Convert.ToDouble(input),
            uint => Convert.ToDouble(input),
            ulong => Convert.ToDouble(input),
            _ => null
        };
    }
}