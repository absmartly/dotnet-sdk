using ABSmartly.Services;

namespace ABSmartly.Sdk.Tests.Services;

[TestFixture]
public class AudienceMatcherTests
{
    private readonly AudienceMatcher _matcher = new(new DefaultAudienceDeserializer());

    [Test]
    public void EvaluateReturnsNullOnEmptyAudience()
    {
        _matcher.Evaluate("", null).Should().BeNull();
        _matcher.Evaluate("{}", null).Should().BeNull();
        _matcher.Evaluate("null", null).Should().BeNull();
    }

    [Test]
    public void EvaluateReturnsNullIfFilterIsNotDictionaryOrList()
    {
        _matcher.Evaluate(@"{""filter"": null}", null).Should().BeNull();
        _matcher.Evaluate(@"{""filter"": false}", null).Should().BeNull();
        _matcher.Evaluate(@"{""filter"": 5}", null).Should().BeNull();
        _matcher.Evaluate(@"{""filter"": ""a""}", null).Should().BeNull();
    }

    [Test]
    public void EvaluateReturnsBoolean()
    {
        _matcher.Evaluate(@"{""filter"": [{""value"": 5}]}", null).Should().Be(true);
        _matcher.Evaluate(@"{""filter"": [{""value"": true}]}", null).Should().Be(true);
        _matcher.Evaluate(@"{""filter"": [{""value"": ""1""}]}", null).Should().Be(true);
        _matcher.Evaluate(@"{""filter"": [{""value"": 0}]}", null).Should().Be(false);
        _matcher.Evaluate(@"{""filter"": [{""value"": false}]}", null).Should().Be(false);
        _matcher.Evaluate(@"{""filter"": [{""value"": ""0""}]}", null).Should().Be(false);

        _matcher.Evaluate(
            @"{""filter"": [{""not"": {""var"": ""returning""}}]}",
            T.MapOf("returning", true)).Should().Be(false);

        _matcher.Evaluate(
            @"{""filter"": [{""not"": {""var"": ""returning""}}]}",
            T.MapOf("returning", false)).Should().Be(true);
    }
}