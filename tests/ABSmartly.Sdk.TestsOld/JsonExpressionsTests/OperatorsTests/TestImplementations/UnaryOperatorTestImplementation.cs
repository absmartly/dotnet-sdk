namespace ABSmartly.DotNet.Standard.UnitTests.JsonExpressionsTests.OperatorsTests.TestImplementations;

public class UnaryOperatorTestImplementation : UnaryOperator
{
    public static object ValidResult => "Unary Pass";

    public override object Unary(IEvaluator evaluator, object arg)
    {
        return ValidResult;
    }
}