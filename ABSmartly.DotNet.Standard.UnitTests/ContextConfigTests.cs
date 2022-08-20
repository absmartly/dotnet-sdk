namespace ABSmartly.DotNet.Standard.UnitTests;

[TestFixture]
public class ContextConfigTests : TestCases
{
    [TestCase("session_id", "0ab1e23f4eee")]
    [TestCase("testType", "123456")]
    public void Unit_SetAndGet_Returns_ExpectedResult(string unitType, string uid)
    {
        var config = new ContextConfig();
        config.SetUnit(unitType, uid);

        var resultUid = config.GetUnit(unitType);

        Assert.That(resultUid, Is.EqualTo(uid));
    }

    [TestCaseSource(nameof(DictionaryOfStringString))]
    public void Units_SetAndGet_Returns_ExpectedResult(Dictionary<string, string> units)
    {
        var config = new ContextConfig();
        config.SetUnits(units);

        var resultUnits = config.GetUnits();

        Assert.That(resultUnits.Count, Is.EqualTo(units.Count));
        foreach (var kvp in units)
        {
            Assert.That(resultUnits[kvp.Key], Is.EqualTo(kvp.Value));
        }
    }


    [TestCaseSource(nameof(String_Object))]
    public void Attribute_SetAndGet_Returns_ExpectedResult(string name, object value)
    {
        var config = new ContextConfig();
        config.SetAttribute(name, value);

        var resultValue = config.GetAttribute(name);

        Assert.That(resultValue, Is.EqualTo(value));
    }

    [TestCaseSource(nameof(DictionaryOfStringObject))]
    public void Attributes_SetAndGet_Returns_ExpectedResult(Dictionary<string, object> attributes)
    {
        var config = new ContextConfig();
        config.SetAttributes(attributes);

        var resultAttributes = config.GetAttributes();

        Assert.That(resultAttributes.Count, Is.EqualTo(attributes.Count));
        foreach (var kvp in attributes)
        {
            Assert.That(resultAttributes[kvp.Key], Is.EqualTo(kvp.Value));
        }
    }


    [TestCaseSource(nameof(String_Int))]
    public void Override_SetAndGet_Returns_ExpectedResult(string experimentName, int variant)
    {
        var config = new ContextConfig();
        config.SetOverride(experimentName, variant);

        var resultVariant = config.GetOverride(experimentName);

        Assert.That(resultVariant, Is.EqualTo(variant));
    }

    [TestCaseSource(nameof(DictionaryOfStringInt))]
    public void Overrides_SetAndGet_Returns_ExpectedResult(Dictionary<string, int> overrides)
    {
        var config = new ContextConfig();
        config.SetOverrides(overrides);

        var resultOverrides = config.GetOverrides();

        Assert.That(resultOverrides.Count, Is.EqualTo(overrides.Count));
        foreach (var kvp in overrides)
        {
            Assert.That(resultOverrides[kvp.Key], Is.EqualTo(kvp.Value));
        }
    }



}