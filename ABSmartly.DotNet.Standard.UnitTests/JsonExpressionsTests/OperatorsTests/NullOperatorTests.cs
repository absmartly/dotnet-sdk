using ABSmartlySdk.JsonExpressions.Operators;

namespace ABSmartly.DotNet.Standard.UnitTests.JsonExpressionsTests.OperatorsTests;

[TestFixture]
public class NullOperatorTests : TestCases
{
#pragma warning disable CS8618
    private NullOperator nullOperator;
#pragma warning restore CS8618

    [SetUp]
    public void Init()
    {
        nullOperator = new NullOperator();
    }

    [TestCaseSource(nameof(RandomNotIListValues))]
    public void Unary_Returns_ExpectedResult(object parameter)
    {
        var result = nullOperator.Unary(null, parameter);

        var expectedResult = parameter is null;

        Assert.That(result, Is.EqualTo(expectedResult)); 
    }

}