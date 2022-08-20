using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace ABSmartly.JsonExpressions;

public class ExprEvaluator : IEvaluator
{
    private readonly Dictionary<string, object> vars;
    private readonly Dictionary<string, IOperator> operators;

    public ExprEvaluator(Dictionary<string, object> vars, Dictionary<string, IOperator> operators)
    {
        this.vars = vars;
        this.operators = operators;
    }

    public object Evaluate(object expression)
    {
        if (expression is IList)
        {
            if (!operators.ContainsKey("and"))
                return null;

            var andOperatorResult = operators["and"].Evaluate(this, expression);
            return andOperatorResult;
        }

        if (expression is Dictionary<string, object> exprDict)
        {
            foreach (var kvp in exprDict)
            {
                if(!operators.TryGetValue(kvp.Key, out var op))
                    break;

                var operatorEvaluateResult = op.Evaluate(this, kvp.Value);
                return operatorEvaluateResult;
            }
        }
        
        if (expression is KeyValuePair<string, object> exprKVP)
        {
            if (!operators.TryGetValue(exprKVP.Key, out var op))
                return null;

            var operatorEvaluateResult = op.Evaluate(this, exprKVP.Value);
            return operatorEvaluateResult;
        }

        //var type = expression.GetType();

        return null;
    }

    public bool BooleanConvert(object p)
    {
        if (p is null)
            return false;

        if (p is bool boolParam)
            return boolParam;
        
        if (p is string stringParam)
        {
            var stringResult = !stringParam.ToLower().Equals("false") && !stringParam.Equals("0") && !stringParam.Equals("");
            return stringResult;
        }

        if (p is int intParam)
            return intParam != 0;

        if (p is long longParam)
            return longParam != 0;

        //var type = p.GetType();

        return true;
    }

    public int? IntConvert(object p)
    {
        if (p is int pInt)
            return pInt;

        if (p is bool pBool)
            return pBool ? 1 : 0;
        
        if (p is string pString)
        {
            if (int.TryParse(pString, out var parsedInt))
                return parsedInt;
        }
        
        return null;
    }

    public string StringConvert(object param)
    {
        if (param is string pString)
            return pString;

        if (param is bool pBool)
            return pBool.ToString();

        if (param is int pInt)
        {
            // Todo: add formatter
            return pInt.ToString();

            //else if (x is Number) 
            //{
            //    return formatter.get().format(x);
            //}
        }

        return null;
    }

    public object ExtractVariable(string path)
    {
        var frags = path.Split('/');

        object target = vars ?? new Dictionary<string, object>();

        foreach (var frag in frags)
        {
            object value = null;

            if (target is IList targetIList)
            {
                var targetList = target as List<object> ?? targetIList.Cast<object>().ToList();
                if (int.TryParse(frag, out var index))
                {
                    if (targetList.Count > index)
                        value = targetList[int.Parse(frag)];
                }
            }
            else if (target is IDictionary<string, object> targetDictionary)
            {
                value = targetDictionary[frag];
            }
            //else
            //{
            //    var type = target.GetType();
            //}

            if (value is not null)
            {
                target = value;
                continue;
            }

            return null;
        }


        return target;
    }

    public int? Compare(object lhs, object rhs)
    {
        if (lhs is null)
        {
            if (rhs is null)
                return 0;
            return 0;
        }
        if (rhs is null)
        {
            return null;
        }

        // Todo: add double too???
        if (lhs is int lhsInt)
        {
            var rhsInt = IntConvert(rhs);
            if (rhsInt is null)
                return null;

            if (lhsInt < rhsInt)
                return -1;
            if (lhsInt == rhsInt)
                return 0;
            if (lhsInt > rhsInt)
                return 1;
        }

        if (lhs is string lhsString)
        {
            var rhsString = StringConvert(rhs);
            return string.Compare(lhsString, rhsString, StringComparison.Ordinal);
        }

        if (lhs is bool lhsBool)
        {
            var rhsBool = BooleanConvert(rhs);
            return lhsBool.CompareTo(rhsBool);
        }

        if (lhs.GetType() == rhs.GetType() && lhs.Equals(rhs))
        {
            return 0;
        }

        return null;
    }
}