namespace ABSmartly.Sdk.Tests;

[TestFixture]
public class ContextConfigTests
{
    [SetUp]
    public void SetUp()
    {
        _config = new ContextConfig();
    }

    private ContextConfig _config = null!;

    [Test]
    public void TestDefaults()
    {
        _config.Attributes.Should().NotBeNull().And.BeEmpty();
        _config.Overrides.Should().NotBeNull().And.BeEmpty();
        _config.Units.Should().NotBeNull().And.BeEmpty();
        _config.CustomAssignments.Should().NotBeNull().And.BeEmpty();

        _config.ContextEventLogger.Should().BeNull();

        _config.PublishDelay.Should().Be(TimeSpan.FromMilliseconds(100));
        _config.RefreshInterval.Should().Be(TimeSpan.FromMilliseconds(0));
    }

    [Test]
    public void TestSetUnit()
    {
        _config.SetUnit("session_id", "0ab1e23f4eee");
        _config.GetUnit("session_id").Should().Be("0ab1e23f4eee");
    }

    [Test]
    public void TestSetUnits()
    {
        var dictionary = new Dictionary<string, string>
        {
            ["a"] = "aaa",
            ["b"] = "bbb"
        };

        _config.SetUnits(dictionary);

        _config.Units.Should().BeEquivalentTo(dictionary);
    }

    [Test]
    public void TestSetAttribute()
    {
        _config.SetAttribute("group", 123);
        _config.GetAttribute("group").Should().Be(123);
    }

    [Test]
    public void TestSetAttributes()
    {
        var dictionary = new Dictionary<string, object>
        {
            ["a"] = "aaa",
            ["b"] = 555
        };

        _config.SetAttributes(dictionary);

        _config.Attributes.Should().BeEquivalentTo(dictionary);
    }

    [Test]
    public void TestSetOverride()
    {
        _config.SetOverride("test", 55);
        _config.GetOverride("test").Should().Be(55);
    }

    [Test]
    public void TestSetOverrides()
    {
        var dictionary = new Dictionary<string, int>
        {
            ["a"] = 1,
            ["b"] = 2
        };

        _config.SetOverrides(dictionary);

        _config.Overrides.Should().BeEquivalentTo(dictionary);
    }

    [Test]
    public void TestSetCustomAssignment()
    {
        _config.SetCustomAssignment("test", 55);
        _config.GetCustomAssignment("test").Should().Be(55);
    }

    [Test]
    public void TestSetCustomAssignments()
    {
        var dictionary = new Dictionary<string, int>
        {
            ["a"] = 1,
            ["b"] = 2
        };

        _config.SetCustomAssignments(dictionary);

        _config.CustomAssignments.Should().BeEquivalentTo(dictionary);
    }
}