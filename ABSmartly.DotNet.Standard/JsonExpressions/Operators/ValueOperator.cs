﻿namespace ABSmartlySdk.JsonExpressions.Operators;

public class ValueOperator : IOperator
{
    public object Evaluate(IEvaluator evaluator, object value)
    {
        return value;
    }
}