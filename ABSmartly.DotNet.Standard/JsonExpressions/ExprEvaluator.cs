﻿using System;
using System.Collections;
using System.Collections.Generic;

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
            return !stringParam.ToLower().Equals("false") && !stringParam.Equals("0") && !stringParam.Equals("");


        if (p is int intParam)
            return intParam != 0;

        return true;
    }

    // Todo: review, Number..
    public int? IntConvert(object x)
    {
        if (x is int) 
        {
            //return (x is Double) ? (Double) x : ((Number) x).doubleValue();
        } 
        else if (x is Boolean) 
        {
            return (bool) x ? 1 : 0;
        }
        else if (x is string) 
        {
            try
            {
                return int.Parse((string)x); // use javascript semantics: numbers are doubles
            }
            catch (Exception ex)
            {

            }
        }

        return null;
    }

    public string StringConvert(object x)
    {
        if (x is string) 
        {
            return (string) x;
        }

        if (x is bool) 
        {
            return x.ToString();
        }

        //else if (x is Number) 
        //{
        //    return formatter.get().format(x);
        //}

        return null;
    }

    // Todo: finish
    public object ExtractVariable(string path)
    {
        throw new System.NotImplementedException();
    }

    // Todo: finish
    public int? Compare(object lhs, object rhs)
    {
        throw new System.NotImplementedException();
    }
}