using System.Text.Json;
using ABSmartly.DefaultServiceImplementations;
using ABSmartly.DotNet.Standard.UnitTests.TestUtils;

namespace ABSmartly.DotNet.Standard.UnitTests.DefaultServiceImplementationsTests;

[TestFixture]
public class DefaultVariableParserTests
{
    [Test]
    public void Test1()
    {
        var parser = new DefaultVariableParser();

        var resultDictionary = parser.Parse(null, null, null, TestData.Json.Variables);

        Assert.That(resultDictionary["a"], Is.EqualTo(1));
    }
}