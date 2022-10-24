using ABSmartly.JsonExpressions.EqualityComparison;

namespace ABSmartly.Sdk.Tests.JsonExpressionsTests.EqualityComparison;

[TestFixture]
public class EqualityComparerSelectorsTests
{
    [Test]
    public void TestDictionaryNestedCollections()
    {
        var comparer = new DictionaryComparer(EqualityComparerSelectors.Default);

        comparer.Equals(
                T.MapOf<string, object>("a", T.MapOf<string, object>("b", 1)), 
                T.MapOf<string, object>("a", T.MapOf<string, object>("b", 1)))
            .Should().Be(true);
        
        comparer.Equals(
                T.MapOf<string, object>("a", T.ListOf<object>(1)), 
                T.MapOf<string, object>("a", T.ListOf<object>(1)))
            .Should().Be(true);
        
        comparer.Equals(
                T.MapOf<string, object>("a", T.ListOf<object>(1)), 
                T.MapOf<string, object>("a", T.ListOf<object>(2)))
            .Should().Be(false);
        
        comparer.Equals(
                T.MapOf<string, object>("a", T.MapOf<string, object>("b", 1)), 
                T.MapOf<string, object>("a", T.MapOf<string, object>("c", 1)))
            .Should().Be(false);
        
        comparer.Equals(
                T.MapOf<string, object>("a", T.MapOf<string, object>("b", 1)), 
                T.MapOf<string, object>("a", T.MapOf<string, object>("b", 2)))
            .Should().Be(false);
        
        comparer.Equals(
                T.MapOf<string, object>("a", T.MapOf<string, object>("b", 1)), 
                T.MapOf<string, object>("a", T.ListOf<object>(2)))
            .Should().Be(false);
        
        comparer.Equals(
                T.MapOf<string, object>("a", T.MapOf<string, object>("b", 1)), 
                T.MapOf<string, object>("a", 1))
            .Should().Be(false);
        
        comparer.Equals(
                T.MapOf<string, object>("a", T.ListOf<object>(T.MapOf<string, object>("b", 1))), 
                T.MapOf<string, object>("a", T.ListOf<object>(T.MapOf<string, object>("b", 1))))
            .Should().Be(true);
        
        comparer.Equals(
                T.MapOf<string, object>("a", T.ListOf<object>(T.MapOf<string, object>("b", 1))), 
                T.MapOf<string, object>("a", T.ListOf<object>(T.MapOf<string, object>("b", 2))))
            .Should().Be(false);
    }
    
    [Test]
    public void TestListNestedCollections()
    {
        var comparer = new ListComparer(EqualityComparerSelectors.Default);

        comparer.Equals(
                T.ListOf<object>("a", T.ListOf<object>("b")), 
                T.ListOf<object>("a", T.ListOf<object>("b")))
            .Should().Be(true);
        
        comparer.Equals(
                T.ListOf<object>("a", T.MapOf<string, object>("b", 1)), 
                T.ListOf<object>("a", T.MapOf<string, object>("b", 1)))
            .Should().Be(true);
        
        comparer.Equals(
                T.ListOf<object>("a", T.ListOf<object>(1)), 
                T.ListOf<object>("a", T.ListOf<object>(2)))
            .Should().Be(false);
        
        comparer.Equals(
                T.ListOf<object>("a", T.MapOf<string, object>("b", 1)), 
                T.ListOf<object>("a", T.MapOf<string, object>("b", 2)))
            .Should().Be(false);
        
        comparer.Equals(
                T.ListOf<object>("a", T.ListOf<object>("b")), 
                T.ListOf<object>("a", T.ListOf<object>("c")))
            .Should().Be(false);
        
        comparer.Equals(
                T.ListOf<object>("a", T.ListOf<object>(1)), 
                T.ListOf<object>("a", T.MapOf<string, object>("b", 2)))
            .Should().Be(false);
        
        comparer.Equals(
                T.ListOf<object>("a", T.ListOf<object>(1)), 
                T.ListOf<object>("a", 1))
            .Should().Be(false);
        
        comparer.Equals(
                T.ListOf<object>("a", T.MapOf<string, object>("b", T.ListOf<object>(1))), 
                T.ListOf<object>("a", T.MapOf<string, object>("b", T.ListOf<object>(1))))
            .Should().Be(true);
        
        comparer.Equals(
                T.ListOf<object>("a", T.MapOf<string, object>("b", T.ListOf<object>(1))), 
                T.ListOf<object>("a", T.MapOf<string, object>("b", T.ListOf<object>(2))))
            .Should().Be(false);
    }
}