using ABSmartly.DefaultServiceImplementations;
using ABSmartly.DotNet.Standard.UnitTests.TestUtils;
using Newtonsoft.Json.Linq;

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
        Assert.That(resultDictionary["b"], Is.EqualTo("test"));

        Assert.Multiple(() =>
        {

        });

        Assert.That(resultDictionary["d"], Is.EqualTo(true));

        Assert.Multiple(() =>
        {
            Assert.That(((IEnumerable<object>)resultDictionary["f"]).ElementAt(0), Is.EqualTo(9234567890));
            Assert.That(((IEnumerable<object>)resultDictionary["f"]).ElementAt(1), Is.EqualTo("a"));
            Assert.That(((IEnumerable<object>)resultDictionary["f"]).ElementAt(2), Is.EqualTo(true));
            Assert.That(((IEnumerable<object>)resultDictionary["f"]).ElementAt(3), Is.EqualTo(false));
        });

        Assert.That(resultDictionary["g"], Is.EqualTo(9.123));
    }
}