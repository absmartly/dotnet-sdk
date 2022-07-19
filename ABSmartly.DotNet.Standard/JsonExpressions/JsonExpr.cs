using System.Collections.Generic;
using ABSmartly.JsonExpressions.Operators;

namespace ABSmartly.JsonExpressions;

public class JsonExpr
{
    private static Dictionary<string, IOperator> operators;

    static JsonExpr()
    {
        operators = new Dictionary<string, IOperator>();
        operators.Add("and", new AndCombinator());
        operators.Add("or", new OrCombinator());
        operators.Add("var", new VarOperator());
        operators.Add("null", new NullOperator());
        operators.Add("not", new NotOperator());
        operators.Add("in", new InOperator());
        operators.Add("match", new MatchOperator());
        operators.Add("eq", new EqualsOperator());
        operators.Add("gt", new GreaterThanOperator());
        operators.Add("gte", new GreaterThanOrEqualOperator());
        operators.Add("lt", new LessThanOperator());
        operators.Add("lte", new LessThanOrEqualOperator());
    }

    public bool EvaluateBooleanExpr(object expression, Dictionary<string, object> variables)
    {
        var evaluator = new ExprEvaluator(operators, variables);
        return evaluator.BooleanConvert(evaluator.Evaluate(expression));
    }

    public object EvaluateExpression(object expression, Dictionary<string, object> variables)
    {
        var evaluator = new ExprEvaluator(operators, variables);
        return evaluator.Evaluate(expression);
    }
}