﻿namespace ABSmartly.JsonExpressions.Operators;

public class LessThanOperator : BinaryOperator
{
    public override object Binary(IEvaluator evaluator, object lhs, object rhs)
    {
        var result = evaluator.Compare(lhs, rhs);
        if (result is null)
            return null;

        return result < 0;
    }
}