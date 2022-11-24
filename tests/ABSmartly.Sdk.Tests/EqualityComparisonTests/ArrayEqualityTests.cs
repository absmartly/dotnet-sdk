using ABSmartly.EqualityComparison;

namespace ABSmartly.Sdk.Tests.EqualityComparisonTests;

[TestFixture]
public class ArrayEqualityTests
{
    [Test]
    public void TestEquals()
    {
        var refArray = new[] { 1, 2, 3 };
        ArrayEquality.Equals(refArray, refArray).Should().BeTrue();
        ArrayEquality.Equals(null, new[] { 1, 2, 3 }).Should().BeFalse();
        ArrayEquality.Equals(new[] { 1, 2, 3 }, null).Should().BeFalse();

        ArrayEquality.Equals(new[] { 1, 2, 3 }, new[] { 1, 2, 3 }).Should().BeTrue();
        ArrayEquality.Equals(new[] { 1.0, 2.0, 3.0 }, new[] { 1.0, 2.0, 3.0 }).Should().BeTrue();
        ArrayEquality.Equals(new[] { "1", "2", "3" }, new[] { "1", "2", "3" }).Should().BeTrue();
        ArrayEquality.Equals(new[] { true, false, true }, new[] { true, false, true }).Should().BeTrue();
        ArrayEquality.Equals(new[] { new CustomEqualsClass(1), new CustomEqualsClass(2) },
            new[] { new CustomEqualsClass(1), new CustomEqualsClass(2) }).Should().BeTrue();

        ArrayEquality.Equals(new object[] { 1, 2, 3 }, new[] { 1, 2, 3 }).Should().BeTrue();
        ArrayEquality.Equals(new object[] { 1.0, 2.0, 3.0 }, new[] { 1.0, 2.0, 3.0 }).Should().BeTrue();
        ArrayEquality.Equals(new object[] { "1", "2", "3" }, new[] { "1", "2", "3" }).Should().BeTrue();
        ArrayEquality.Equals(new object[] { true, false, true }, new[] { true, false, true }).Should().BeTrue();
        ArrayEquality.Equals(new object[] { new CustomEqualsClass(1), new CustomEqualsClass(2) },
            new[] { new CustomEqualsClass(1), new CustomEqualsClass(2) }).Should().BeTrue();
        ArrayEquality.Equals(new object[] { 1, 2, 3 }, new object[] { 1, 2, 3 }).Should().BeTrue();
        ArrayEquality.Equals(new object[] { 1.0, 2.0, 3.0 }, new object[] { 1.0, 2.0, 3.0 }).Should().BeTrue();
        ArrayEquality.Equals(new object[] { "1", "2", "3" }, new object[] { "1", "2", "3" }).Should().BeTrue();
        ArrayEquality.Equals(new object[] { true, false, true }, new object[] { true, false, true }).Should().BeTrue();
        ArrayEquality.Equals(new object[] { new CustomEqualsClass(1), new CustomEqualsClass(2) },
            new object[] { new CustomEqualsClass(1), new CustomEqualsClass(2) }).Should().BeTrue();

        ArrayEquality.Equals(new object[] { 1, "2", true }, new object[] { 1, "2", true }).Should().BeTrue();


        ArrayEquality.Equals(new[] { 1, 2, 3 }, new[] { 1, 2 }).Should().BeFalse();

        ArrayEquality.Equals(new[] { 1, 2, 3 }, new[] { 1, 2, 4 }).Should().BeFalse();
        ArrayEquality.Equals(new[] { 1.0, 2.0, 3.0 }, new[] { 1.0, 2.0, 4.0 }).Should().BeFalse();
        ArrayEquality.Equals(new[] { "1", "2", "3" }, new[] { "1", "2", "4" }).Should().BeFalse();
        ArrayEquality.Equals(new[] { true, false, true }, new[] { true, false, false }).Should().BeFalse();
        ArrayEquality.Equals(new[] { new CustomEqualsClass(1), new CustomEqualsClass(2) },
            new[] { new CustomEqualsClass(1), new CustomEqualsClass(3) }).Should().BeFalse();

        ArrayEquality.Equals(new object[] { 1, 2, 3 }, new[] { 1, 2, 4 }).Should().BeFalse();
        ArrayEquality.Equals(new object[] { 1.0, 2.0, 3.0 }, new[] { 1.0, 2.0, 4.0 }).Should().BeFalse();
        ArrayEquality.Equals(new object[] { "1", "2", "3" }, new[] { "1", "2", "4" }).Should().BeFalse();
        ArrayEquality.Equals(new object[] { true, false, true }, new[] { true, false, false }).Should().BeFalse();
        ArrayEquality.Equals(new object[] { new CustomEqualsClass(1), new CustomEqualsClass(2) },
            new[] { new CustomEqualsClass(1), new CustomEqualsClass(3) }).Should().BeFalse();

        ArrayEquality.Equals(new object[] { 1, 2, 3 }, new object[] { 1, 2, 4 }).Should().BeFalse();
        ArrayEquality.Equals(new object[] { 1.0, 2.0, 3.0 }, new object[] { 1.0, 2.0, 4.0 }).Should().BeFalse();
        ArrayEquality.Equals(new object[] { "1", "2", "3" }, new object[] { "1", "2", "4" }).Should().BeFalse();
        ArrayEquality.Equals(new object[] { true, false, true }, new object[] { true, false, false }).Should()
            .BeFalse();
        ArrayEquality.Equals(new object[] { new CustomEqualsClass(1), new CustomEqualsClass(2) },
            new object[] { new CustomEqualsClass(1), new CustomEqualsClass(3) }).Should().BeFalse();

        ArrayEquality.Equals(new object[] { 1, "2", true }, new object[] { "1", 2, true }).Should().BeFalse();
    }

    private class CustomEqualsClass
    {
        public CustomEqualsClass(int value)
        {
            Value = value;
        }

        public int Value { get; }

        protected bool Equals(CustomEqualsClass other)
        {
            return Value == other.Value;
        }

        public override bool Equals(object? obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != GetType()) return false;
            return Equals((CustomEqualsClass)obj);
        }

        public override int GetHashCode()
        {
            return Value;
        }
    }
}