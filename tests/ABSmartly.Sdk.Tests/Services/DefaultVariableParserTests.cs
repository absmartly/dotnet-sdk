using System.Text;
using ABSmartly.Services;

namespace ABSmartly.Sdk.Tests.Services;

[TestFixture]
public class DefaultVariableParserTests
{
    private readonly DefaultVariableParser _parser = new();
    
    [Test]
    public void TestParse()
    {
        var context = Mock.Of<IContext>();
        using var jsonStream = GetType().Assembly.GetManifestResourceStream("ABSmartly.Sdk.Tests.Resources.variables.json");
        using var streamReader = new StreamReader(jsonStream!, Encoding.UTF8);
        var configValue = streamReader.ReadToEnd();

        var actual = _parser.Parse(context, "test_exp", "B", configValue);

        var expected = T.MapOf(
            "a", 1,
            "b", "test",
            "c", T.MapOf(
                "test", 2,
                "double", 19.123,
                "list", T.ListOf("x", "y", "z"),
                "point", T.MapOf(
                    "x", -1.0,
                    "y", 0.0,
                    "z", 1.0)),
            "d", true,
            "f", T.ListOf(9234567890L, "a", true, false),
            "g", 9.123);

        actual.Should().BeEquivalentTo(expected);
    }
    
    [Test]
    public void TestParseDoesNotThrow()
    {
        var context = Mock.Of<IContext>();
        var act = () => _parser.Parse(context, null, null, null);

        act.Should().NotThrow();
    }
}