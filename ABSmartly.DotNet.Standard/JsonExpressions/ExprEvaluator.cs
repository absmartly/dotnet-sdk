using System;
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
            return operators["and"].Evaluate(this, expression);

        if (expression is IDictionary<string, object> exprDict)
        {
            foreach (var kvp in exprDict)
            {
                if(!operators.TryGetValue(kvp.Key, out var op))
                    break;

                return op.Evaluate(this, kvp.Value);
            }
        }

        return null;
    }

    public bool BooleanConvert(object p)
    {
        if (p is bool) 
        {
            return (bool) p;
        }

        if (p is string) 
        {
            return !p.Equals("false") && !p.Equals("0") && !p.Equals("");
            // Todo: Number
        }

        if (p is int) 
        {
            return ((int) p) != 0;
            //return ((int) x).longValue() != 0;
        }

        return p != null;
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

    // Todo: review, Number..
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