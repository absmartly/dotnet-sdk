using System;
using System.Collections.Generic;
using System.Diagnostics;
using ABSmartlySdk.JsonExpressions.Operators;

namespace ABSmartlySdk.JsonExpressions;

public class JsonExpr
{
    private static readonly Dictionary<string, IOperator> operators;

    static JsonExpr()
    {
        operators = new Dictionary<string, IOperator>
        {
            { "and", new AndCombinator() },
            { "or", new OrCombinator() },
            { "value", new ValueOperator() },
            { "var", new VarOperator() },
            { "null", new NullOperator() },
            { "not", new NotOperator() },
            { "in", new InOperator() },
            { "match", new MatchOperator() },
            { "eq", new EqualsOperator() },
            { "gt", new GreaterThanOperator() },
            { "gte", new GreaterThanOrEqualOperator() },
            { "lt", new LessThanOperator() },
            { "lte", new LessThanOrEqualOperator() }
        };
    }

    public bool EvaluateBooleanExpr(object expression, Dictionary<string, object> variables)
    {
        try
        {
            var evaluator = new ExprEvaluator(variables, operators);
            var evaluateResult = evaluator.Evaluate(expression);
            var booleanConvertResult = evaluator.BooleanConvert(evaluateResult);
            return booleanConvertResult;
        }
        catch (Exception e)
        {
            Debug.WriteLine(e);
            return false;
        }
    }

    public object EvaluateExpression(object expression, Dictionary<string, object> variables)
    {
        var evaluator = new ExprEvaluator(variables, operators);
        return evaluator.Evaluate(expression);
    }
}