using System.Collections.Generic;
using ABSmartly.JsonExpressions.Operators;

namespace ABSmartly.JsonExpressions;

public class JsonExpr
{
    private static readonly Dictionary<string, IOperator> operators;

    static JsonExpr()
    {
        operators = new Dictionary<string, IOperator>
        {
            { "and", new AndCombinator() },
            { "or", new OrCombinator() },
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
        var evaluator = new ExprEvaluator(variables, operators);
        return evaluator.BooleanConvert(evaluator.Evaluate(expression));
    }

    public object EvaluateExpression(object expression, Dictionary<string, object> variables)
    {
        var evaluator = new ExprEvaluator(variables, operators);
        return evaluator.Evaluate(expression);
    }
}