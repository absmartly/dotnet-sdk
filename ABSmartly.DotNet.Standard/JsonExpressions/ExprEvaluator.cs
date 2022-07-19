using System;
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

    // Todo: finish
    public object Evaluate(object expression)
    {
        throw new System.NotImplementedException();
    }

    public bool BooleanConvert(object x)
    {
        if (x is bool) 
        {
            return (bool) x;
        }

        if (x is string) 
        {
            return !x.Equals("false") && !x.Equals("0") && !x.Equals("");
            // Todo: Number
        }

        if (x is int) 
        {
            return ((int) x) != 0;
            //return ((int) x).longValue() != 0;
        }

        return x != null;
    }

    // Todo: review, Number..
    public int? NumberConvert(object x)
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