using ABSmartly.DotNet.Standard.UnitTests.TestUtils;
using ABSmartly.JsonExpressions.Operators;

namespace ABSmartly.DotNet.Standard.UnitTests.JsonExpressionsTests.OperatorsTests;

[TestFixture]
public class ValueOperatorTests : TestCases
{
#pragma warning disable CS8618
    private ValueOperator valueOperator;
#pragma warning restore CS8618

    [SetUp]
    public void Init()
    {
        valueOperator = new ValueOperator();
    }

    [TestCaseSource(nameof(RandomNotIListValues))]
    public void Evaluate_Returns_Parameter(object parameter)
    {
        var result = valueOperator.Evaluate(null, parameter);

        Assert.That(result, Is.EqualTo(parameter));
    }
}