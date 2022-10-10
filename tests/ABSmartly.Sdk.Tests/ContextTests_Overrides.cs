namespace ABSmartly.DotNet.Standard.UnitTests;

public partial class ContextTests
{

    [TestCaseSource(nameof(String_Int))]
    public void Override_DefaultContext_SetAndGet_Returns_ExpectedResult(string experimentName, int variant)
    {
        var context = ContextFactory.CreateDefault();
        context.SetOverride(experimentName, variant);

        var resultVariant = context.GetOverride(experimentName);

        Assert.That(resultVariant, Is.EqualTo(variant));
    }


    [TestCaseSource(nameof(DictionaryOfStringInt))]
    public void Overrides_DefaultContext_SetAndGet_Returns_ExpectedResult(Dictionary<string, int> overrides)
    {
        var context = ContextFactory.CreateDefault();
        context.SetOverrides(overrides);

        foreach (var kvp in overrides)
        {
            var resultOverride = context.GetOverride(kvp.Key);
            Assert.That(resultOverride, Is.EqualTo(kvp.Value));

        }
    }
}