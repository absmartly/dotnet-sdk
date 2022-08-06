using ABSmartly.DotNet.Standard.UnitTests.TestUtils;
using ABSmartly.JsonExpressions;
using ABSmartly.JsonExpressions.Operators;
using Moq;

namespace ABSmartly.DotNet.Standard.UnitTests.JsonExpressionsTests.OperatorsTests;

[TestFixture]
public class VarOperatorTests : TestCases
{
#pragma warning disable CS8618
    private Mock<IEvaluator> evaluator;
    private VarOperator varOperator;
#pragma warning restore CS8618

    [SetUp]
    public void Init()
    {
        evaluator = new Mock<IEvaluator>();

        //evaluator.Setup(e => e.BooleanConvert(It.IsAny<bool?>()))
        //    .Returns(NullableBoolConvert);

        varOperator = new VarOperator();
    }


    //public void Evaluate_





}