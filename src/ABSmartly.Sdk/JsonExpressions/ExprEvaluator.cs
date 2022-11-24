using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using ABSmartly.EqualityComparison;

namespace ABSmartly.JsonExpressions;

public class ExprEvaluator : IEvaluator
{
    private readonly Dictionary<string, object> _vars;
    private readonly Dictionary<string, IOperator> _operators;
    private readonly DictionaryComparer _dictComparer = new (EqualityComparerSelectors.Default);
    private readonly ListComparer _listComparer = new (EqualityComparerSelectors.Default);

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
                return _operators.TryGetValue("and", out var andOperator) ? andOperator.Evaluate(this, expression) : null;
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

    public bool? BooleanConvert(object x) =>
        x switch
        {
            bool boolValue => boolValue,
            string stringValue => !stringValue.ToLower().Equals("false") && !stringValue.Equals("0") &&
                                  !stringValue.Equals(""),
            { } when GetNumber(x) is { } doubleValue => doubleValue != 0,
            _ => x != null
        };

    public double? NumberConvert(object x) =>
        x switch
        {
            bool boolValue => boolValue ? 1 : 0,
            string stringValue when double.TryParse(stringValue, NumberStyles.Number, CultureInfo.InvariantCulture,
                out var result) => result,
            { } when GetNumber(x) is { } doubleValue => doubleValue,
            _ => null
        };

    public string StringConvert(object x) =>
        x switch
        {
            bool boolValue => boolValue.ToString().ToLowerInvariant(),
            string stringValue => stringValue,
            { } when GetNumber(x) is { } doubleValue => doubleValue.ToString("G16",
                new NumberFormatInfo { NumberDecimalSeparator = "." }),
            _ => null
        };

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
            
            target = value;
        }

        return target;
    }

    public int? Compare(object lhs, object rhs)
    {
        if (lhs is null)
            return rhs is null ? 0 : null;

        if (rhs is null) return null;

        if (GetNumber(lhs) is {} lhsDoubleValue)
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

        if (lhs.GetType() == rhs.GetType())
        {
            return lhs switch
            {
                Dictionary<string, object> lhsDictionary => _dictComparer.Equals(lhsDictionary,
                    (Dictionary<string, object>)rhs)
                    ? 0
                    : null,
                List<object> lhsList => _listComparer.Equals(lhsList, (List<object>)rhs) ? 0 : null,
                _ => lhs.Equals(rhs) ? 0 : null
            };
        }

        return null;
    }

    private static double? GetNumber(object input) =>
        input switch
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