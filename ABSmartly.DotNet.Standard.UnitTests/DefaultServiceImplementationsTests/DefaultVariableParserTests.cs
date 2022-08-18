using ABSmartly.DefaultServiceImplementations;
using ABSmartly.DotNet.Standard.UnitTests.TestUtils;

namespace ABSmartly.DotNet.Standard.UnitTests.DefaultServiceImplementationsTests;

[TestFixture]
public class DefaultVariableParserTests
{
    [Test]
    public void Parse_SimpleObject()
    {
        var parser = new DefaultVariableParser();

        var resultDictionary = parser.Parse(null, null, null, TestData.Json.Variables);

        Assert.That(resultDictionary["a"], Is.EqualTo(1));
        Assert.That(resultDictionary["b"], Is.EqualTo("test"));
        Assert.That(resultDictionary["d"], Is.EqualTo(true));
        Assert.That(resultDictionary["g"], Is.EqualTo(9.123));
    }

    [Test]
    public void Parse_IEnumerableOfBaseValue()
    {
        var parser = new DefaultVariableParser();

        var resultDictionary = parser.Parse(null, null, null, TestData.Json.Variables);

        var enumerable = ((IEnumerable<object>)resultDictionary["f"]).ToList();

        Assert.That(enumerable[0], Is.EqualTo(9234567890));
        Assert.That(enumerable[1], Is.EqualTo("a"));
        Assert.That(enumerable[2], Is.EqualTo(true));
        Assert.That(enumerable[3], Is.EqualTo(false));
    }

    [Test]
    public void Parse_DictionaryOfBaseValue()
    {
        var parser = new DefaultVariableParser();

        var resultDictionary = parser.Parse(null, null, null, TestData.Json.Variables);

        var dictionary = (Dictionary<string, object>)resultDictionary["c"];

        Assert.That(dictionary["test"], Is.EqualTo(2));
        Assert.That(dictionary["double"], Is.EqualTo(19.123));
    }

    [Test]
    public void Parse_DictionaryOfIEnumerable()
    {
        var parser = new DefaultVariableParser();

        var resultDictionary = parser.Parse(null, null, null, TestData.Json.Variables);

        var dictionary = (Dictionary<string, object>)resultDictionary["c"];

        var dictenum = (IEnumerable<object>)dictionary["list"];
        var dictlist = dictenum.ToList();

        Assert.That(dictlist[0], Is.EqualTo("x"));
        Assert.That(dictlist[1], Is.EqualTo("y"));
        Assert.That(dictlist[2], Is.EqualTo("z"));
    }

    [Test]
    public void Parse_DictionaryOfDictionary()
    {
        var parser = new DefaultVariableParser();

        var resultDictionary = parser.Parse(null, null, null, TestData.Json.Variables);

        var dictionary = (Dictionary<string, object>)resultDictionary["c"];
        var dictdict = (Dictionary<string, object>)dictionary["point"];

        Assert.That(dictdict["x"], Is.EqualTo(-1));
        Assert.That(dictdict["y"], Is.EqualTo(0));
        Assert.That(dictdict["z"], Is.EqualTo(1));
    }
}