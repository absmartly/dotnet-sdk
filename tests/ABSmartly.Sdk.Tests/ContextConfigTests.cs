using ABSmartlySdk;

namespace ABSmartly.DotNet.Standard.UnitTests;

[TestFixture]
public class ContextConfigTests : TestCases
{
    [Test]
    public void Constructor_InitializesPublishDelay()
    {
        var config = new ContextConfig();
        var publishDelay = config.GetPublishDelay();

        Assert.That(publishDelay, Is.EqualTo(100));
    }
    [Test]
    public void Constructor_InitializesRefreshInterval()
    {
        var config = new ContextConfig();
        var refreshInterval = config.GetRefreshInterval();

        Assert.That(refreshInterval, Is.EqualTo(0));
    }


    [TestCase("session_id", "0ab1e23f4eee")]
    [TestCase("testType", "123456")]
    public void Unit_SetAndGet_Returns_ExpectedResult(string unitType, string uid)
    {
        var config = new ContextConfig();
        config.SetUnit(unitType, uid);

        var resultUid = config.GetUnit(unitType);

        Assert.That(resultUid, Is.EqualTo(uid));
    }

    [TestCase("session_id", "0ab1e23f4eee")]
    [TestCase("testType", "123456")]
    public void Unit_Get_NotContainedKey_Returns_EmptyString(string unitType, string uid)
    {
        var config = new ContextConfig();
        config.SetUnit(unitType, uid);

        var resultUid = config.GetUnit("not-contained-key");

        Assert.That(resultUid, Is.EqualTo(string.Empty));
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

    [TestCaseSource(nameof(String_Object))]
    public void Attribute_Get_NotContainedKey_Returns_Null(string name, object value)
    {
        var config = new ContextConfig();
        config.SetAttribute(name, value);

        var resultValue = config.GetAttribute("not-contained-key");

        Assert.That(resultValue, Is.EqualTo(null));
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

    [TestCaseSource(nameof(String_Int))]
    public void Override_Get_NotContainedKey_Returns_Null(string experimentName, int variant)
    {
        var config = new ContextConfig();
        config.SetOverride(experimentName, variant);

        var resultVariant = config.GetOverride("not-contained-key");

        Assert.That(resultVariant, Is.EqualTo(null));
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


    [TestCaseSource(nameof(String_Int))]
    public void CustomAssignment_SetAndGet_Returns_ExpectedResult(string experimentName, int variant)
    {
        var config = new ContextConfig();
        config.SetCustomAssignment(experimentName, variant);

        var resultVariant = config.GetCustomAssignment(experimentName);

        Assert.That(resultVariant, Is.EqualTo(variant));
    }

    [TestCaseSource(nameof(String_Int))]
    public void CustomAssignment_Get_NotContainedKey_Returns_Null(string experimentName, int variant)
    {
        var config = new ContextConfig();
        config.SetCustomAssignment(experimentName, variant);

        var resultVariant = config.GetCustomAssignment("not-contained-key");

        Assert.That(resultVariant, Is.EqualTo(null));
    }

    [TestCaseSource(nameof(DictionaryOfStringInt))]
    public void CustomAssignments_SetAndGet_Returns_ExpectedResult(Dictionary<string, int> customAssignments)
    {
        var config = new ContextConfig();
        config.SetCustomAssignments(customAssignments);

        var resultCustomAssignments = config.GetCustomAssignments();

        Assert.That(resultCustomAssignments.Count, Is.EqualTo(customAssignments.Count));
        foreach (var kvp in customAssignments)
        {
            Assert.That(resultCustomAssignments[kvp.Key], Is.EqualTo(kvp.Value));
        }
    }


    [TestCase(0)]
    [TestCase(-10)]
    [TestCase(20)]
    [TestCase(long.MinValue)]
    [TestCase(long.MaxValue)]
    public void PublishDelay_SetAndGet_Returns_ExpectedResult(long delay)
    {
        var config = new ContextConfig();
        config.SetPublishDelay(delay);

        var resultDelay = config.GetPublishDelay();

        Assert.That(resultDelay, Is.EqualTo(delay));
    }

    [TestCase(0)]
    [TestCase(-10)]
    [TestCase(20)]
    [TestCase(long.MinValue)]
    [TestCase(long.MaxValue)]
    public void RefreshInterval_SetAndGet_Returns_ExpectedResult(long refreshInterval)
    {
        var config = new ContextConfig();
        config.SetRefreshInterval(refreshInterval);

        var resultRefreshInterval = config.GetRefreshInterval();

        Assert.That(resultRefreshInterval, Is.EqualTo(refreshInterval));
    }
}