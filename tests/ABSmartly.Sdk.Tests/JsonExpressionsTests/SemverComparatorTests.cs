using System.Globalization;
using ABSmartly.JsonExpressions;

namespace ABSmartly.Sdk.Tests.JsonExpressionsTests;

// Cases ported from java-sdk#17 (SemverComparatorTest), which mirror the JavaScript SDK.
[TestFixture]
public class SemverComparatorTests
{
    private readonly ExprEvaluator _evaluator = new(new Dictionary<string, IOperator>(), new Dictionary<string, object>());

    private int? Cmp(object? lhs, object? rhs) => SemverComparator.Compare(_evaluator, lhs!, rhs!);

    [TestCase("1.0.0", "1.0.0", 0)]
    [TestCase("0.0.0", "0.0.0", 0)]
    [TestCase("999.999.999", "999.999.999", 0)]
    [TestCase("2.0.0", "1.0.0", 1)]
    [TestCase("1.0.0", "2.0.0", -1)]
    [TestCase("1.2.0", "1.1.0", 1)]
    [TestCase("1.1.0", "1.2.0", -1)]
    [TestCase("1.0.2", "1.0.1", 1)]
    [TestCase("1.0.1", "1.0.2", -1)]
    [TestCase("1.10.0", "1.9.0", 1)]
    [TestCase("1.9.0", "1.10.0", -1)]
    [TestCase("10.0.0", "9.0.0", 1)]
    [TestCase("1.2", "1.2.0", 0)]
    [TestCase("1", "1.0.0", 0)]
    [TestCase("1.2.0", "1.2", 0)]
    [TestCase("1.0.0", "1", 0)]
    [TestCase("1.02.0", "1.2.0", 0)]
    [TestCase("1.002.030", "1.2.30", 0)]
    [TestCase("01.0.0", "1.0.0", 0)]
    [TestCase("1.02.0", "1.3.0", -1)]
    [TestCase("1.00.0", "1.0.0", 0)]
    [TestCase("1.0.0", "1.0.0-alpha", 1)]
    [TestCase("1.0.0-alpha", "1.0.0", -1)]
    [TestCase("1.0.0-alpha", "1.0.0-alpha", 0)]
    [TestCase("1.0.0-alpha", "1.0.0-beta", -1)]
    [TestCase("1.0.0-beta", "1.0.0-alpha", 1)]
    [TestCase("1.0.0-alpha.1", "1.0.0-alpha.2", -1)]
    [TestCase("1.0.0-alpha.2", "1.0.0-alpha.1", 1)]
    [TestCase("1.0.0-1", "1.0.0-2", -1)]
    [TestCase("1.0.0-2", "1.0.0-1", 1)]
    [TestCase("1.0.0-rc-1", "1.0.0-rc-1", 0)]
    [TestCase("1.0.0-1", "1.0.0-alpha", -1)]
    [TestCase("1.0.0-alpha", "1.0.0-1", 1)]
    [TestCase("1.0.0-alpha", "1.0.0-alpha.1", -1)]
    [TestCase("1.0.0-alpha.1", "1.0.0-alpha", 1)]
    [TestCase("1.0.0-alpha.10", "1.0.0-alpha.9", 1)]
    [TestCase("v1.0.0", "1.0.0", 0)]
    [TestCase("V1.0.0", "1.0.0", 0)]
    [TestCase("v1.0.0", "V1.0.0", 0)]
    [TestCase("1.0.0+build1", "1.0.0+build2", 0)]
    [TestCase("1.0.0+build1", "1.0.0", 0)]
    [TestCase("1.0.0-alpha+build1", "1.0.0-alpha+build2", 0)]
    [TestCase("1.0.0-alpha+build1", "1.0.0-beta", -1)]
    [TestCase("1.0.0-alpha+build1", "1.0.0", -1)]
    [TestCase("1.0.0-", "1.0.0", 0)]
    [TestCase("1.x", "1.0", 1)]
    public void ComparesVersions(string lhs, string rhs, int expected)
    {
        Cmp(lhs, rhs).Should().Be(expected);
    }

    [TestCase("", "1.0.0")]
    [TestCase("1.0.0", "")]
    [TestCase("", "")]
    [TestCase("v", "1.0.0")]
    [TestCase("V", "1.0.0")]
    [TestCase("+build", "1.0.0")]
    [TestCase("v+build", "1.0.0")]
    [TestCase("1.0.0", "v")]
    [TestCase("-alpha", "1.0.0")]
    public void EmptyVersionCoreIsNotComparable(string lhs, string rhs)
    {
        Cmp(lhs, rhs).Should().BeNull();
    }

    [Test]
    public void NullOrNonScalarOperandsAreNotComparable()
    {
        Cmp(null, "1.0.0").Should().BeNull();
        Cmp("1.0.0", null).Should().BeNull();
        Cmp(null, null).Should().BeNull();
        Cmp(new Dictionary<string, object>(), "1.0.0").Should().BeNull();
        Cmp("1.0.0", new Dictionary<string, object>()).Should().BeNull();
        Cmp(new List<object>(), "1.0.0").Should().BeNull();
    }

    [Test]
    public void NumbersAndBooleansAreConvertedToStrings()
    {
        Cmp(1, "1.0.0").Should().Be(0);
        Cmp("1.0.0", 1L).Should().Be(0);
        Cmp(true, "true").Should().Be(0);
        Cmp(2.5, "2.5.0").Should().Be(0);
    }

    [Test]
    public void NumericOperandIsCultureIndependent()
    {
        var previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("de-DE");
            Cmp(2.5, "2.5.0").Should().Be(0);
            Cmp(2.5, "2.10").Should().Be(-1);
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }
}
