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

        //if (expression is IDictionary<string, object> exprDict)
        if (expression is IDictionary exprDict)
        {
            var dict = exprDict as Dictionary<string, object>;
            foreach (var kvp in dict)
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

        var type = expression.GetType();

        return null;
    }

    public bool BooleanConvert(object p)
    {
        if (p is null)
            return false;

        if (p is bool boolParam)
            return boolParam;
        

        if (p is string stringParam)
            return !stringParam.ToLower().Equals("false") && !stringParam.Equals("0") && !stringParam.Equals("");


        if (p is int intParam)
            return intParam != 0;

        return true;
    }

    // Todo: review, Number..
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
    //public Double numberConvert(Object x) {
    //    if (x instanceof Number) {
    //        return (x instanceof Double) ? (Double) x : ((Number) x).doubleValue();
    //    } else if (x instanceof Boolean) {
    //        return (Boolean) x ? 1.0 : 0.0;
    //    } else if (x instanceof String) {
    //        try {
    //            return Double.parseDouble((String) x); // use javascript semantics: numbers are doubles
    //        } catch (Throwable ignored) {}
    //    }

    //    return null;
    //}

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

    // Todo: finish
    public object ExtractVariable(string path)
    {
        var frags = path.Split('/');

        object target = vars ?? new Dictionary<string, object>();

        foreach (var frag in frags)
        {
            // Todo: target is list??

            object value = null;

            if (target is IList)
            {
                var targetList = target as List<object>;
                value = targetList[int.Parse(frag)];
            }
            else if (target is IDictionary<string, object> targetDictionary)
            {
                //var map = (Dictionary<string, object>)target;
                value = targetDictionary[frag];
            }
            else
            {
                var type = target.GetType();
            }

            if (value is not null)
            {
                target = value;
                continue;
            }

            return null;
        }


        return target;
    }

    // Todo: finish
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
            return lhsString.CompareTo(rhsString);
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

        //} else if (lhs instanceof String) {
        //    final String rvalue = stringConvert(rhs);
        //    if (rvalue != null) {
        //        return ((String) lhs).compareTo(rvalue);
        //    }
        //} else if (lhs instanceof Boolean) {
        //    final Boolean rvalue = booleanConvert(rhs);
        //    if (rvalue != null) {
        //        return ((Boolean) lhs).compareTo(rvalue);
        //    }
        //} else if ((lhs.getClass() == rhs.getClass()) && (lhs.equals(rhs))) {
        //    return 0;
        //}

        return null;
    }
}