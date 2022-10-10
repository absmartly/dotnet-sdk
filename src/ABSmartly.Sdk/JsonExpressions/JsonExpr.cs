using System;
using System.Collections.Generic;
using ABSmartly.JsonExpressions.Operators;

namespace ABSmartly.JsonExpressions;

public static class JsonExpr
{
    private static readonly Dictionary<string, IOperator> Operators = new()
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

    public static bool EvaluateBooleanExpr(object expression, Dictionary<string, object> variables)
    {
        try
        {
            var evaluator = new ExprEvaluator(variables, Operators);
            var evaluateResult = evaluator.Evaluate(expression);
            var booleanConvertResult = evaluator.BooleanConvert(evaluateResult);
            return booleanConvertResult;
        }
        catch (Exception)
        {
            return false;
        }
    }

    public static object EvaluateExpression(object expression, Dictionary<string, object> variables)
    {
        var evaluator = new ExprEvaluator(variables, Operators);
        return evaluator.Evaluate(expression);
    }
}