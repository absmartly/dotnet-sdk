using System.Collections;

namespace ABSmartly.Sdk.Tests.JsonExpressionsTests.EqualityComparison;

public abstract class ComparerTestBase
{
    protected readonly IEqualityComparer ValueComparer;

    protected ComparerTestBase()
    {
        ValueComparer = Mock.Of<IEqualityComparer>();

        Mock.Get(ValueComparer).Setup(x => x.Equals(It.IsAny<object>(), It.IsAny<object>())).Returns<object, object>(
            (lhs, rhs) =>
            {
                if (lhs is int lhsInt) return EqualityComparer<int>.Default.Equals(lhsInt, (int)rhs);
                if (lhs is double lhsDouble) return EqualityComparer<double>.Default.Equals(lhsDouble, (double)rhs);
                if (lhs is string lhsString) return EqualityComparer<string>.Default.Equals(lhsString, (string)rhs);
                return false;
            });
    }
    
    protected IEqualityComparer ValueComparerSelectorFn(object x)
    {
        return ValueComparer;
    }
}