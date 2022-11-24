using ABSmartly.EqualityComparison;

namespace ABSmartly.Sdk.Tests.EqualityComparisonTests;

[TestFixture]
public class EqualityComparerSelectorsTests
{
    [Test]
    public void TestDictionaryNestedCollections()
    {
        var comparer = new DictionaryComparer(EqualityComparerSelectors.Default);

        comparer.Equals(
                T.MapOf("a", T.MapOf("b", 1)),
                T.MapOf("a", T.MapOf("b", 1)))
            .Should().Be(true);

        comparer.Equals(
                T.MapOf("a", T.ListOf(1)),
                T.MapOf("a", T.ListOf(1)))
            .Should().Be(true);

        comparer.Equals(
                T.MapOf("a", T.ListOf(1)),
                T.MapOf("a", T.ListOf(2)))
            .Should().Be(false);

        comparer.Equals(
                T.MapOf("a", T.MapOf("b", 1)),
                T.MapOf("a", T.MapOf("c", 1)))
            .Should().Be(false);

        comparer.Equals(
                T.MapOf("a", T.MapOf("b", 1)),
                T.MapOf("a", T.MapOf("b", 2)))
            .Should().Be(false);

        comparer.Equals(
                T.MapOf("a", T.MapOf("b", 1)),
                T.MapOf("a", T.ListOf(2)))
            .Should().Be(false);

        comparer.Equals(
                T.MapOf("a", T.MapOf("b", 1)),
                T.MapOf("a", 1))
            .Should().Be(false);

        comparer.Equals(
                T.MapOf("a", T.ListOf(T.MapOf("b", 1))),
                T.MapOf("a", T.ListOf(T.MapOf("b", 1))))
            .Should().Be(true);

        comparer.Equals(
                T.MapOf("a", T.ListOf(T.MapOf("b", 1))),
                T.MapOf("a", T.ListOf(T.MapOf("b", 2))))
            .Should().Be(false);
    }

    [Test]
    public void TestListNestedCollections()
    {
        var comparer = new ListComparer(EqualityComparerSelectors.Default);

        comparer.Equals(
                T.ListOf("a", T.ListOf("b")),
                T.ListOf("a", T.ListOf("b")))
            .Should().Be(true);

        comparer.Equals(
                T.ListOf("a", T.MapOf("b", 1)),
                T.ListOf("a", T.MapOf("b", 1)))
            .Should().Be(true);

        comparer.Equals(
                T.ListOf("a", T.ListOf(1)),
                T.ListOf("a", T.ListOf(2)))
            .Should().Be(false);

        comparer.Equals(
                T.ListOf("a", T.MapOf("b", 1)),
                T.ListOf("a", T.MapOf("b", 2)))
            .Should().Be(false);

        comparer.Equals(
                T.ListOf("a", T.ListOf("b")),
                T.ListOf("a", T.ListOf("c")))
            .Should().Be(false);

        comparer.Equals(
                T.ListOf("a", T.ListOf(1)),
                T.ListOf("a", T.MapOf("b", 2)))
            .Should().Be(false);

        comparer.Equals(
                T.ListOf("a", T.ListOf(1)),
                T.ListOf("a", 1))
            .Should().Be(false);

        comparer.Equals(
                T.ListOf("a", T.MapOf("b", T.ListOf(1))),
                T.ListOf("a", T.MapOf("b", T.ListOf(1))))
            .Should().Be(true);

        comparer.Equals(
                T.ListOf("a", T.MapOf("b", T.ListOf(1))),
                T.ListOf("a", T.MapOf("b", T.ListOf(2))))
            .Should().Be(false);
    }
}