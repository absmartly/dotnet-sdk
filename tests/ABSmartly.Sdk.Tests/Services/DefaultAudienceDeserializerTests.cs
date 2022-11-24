using ABSmartly.Services;

namespace ABSmartly.Sdk.Tests.Services;

[TestFixture]
public class DefaultAudienceDeserializerTests
{
    private readonly DefaultAudienceDeserializer _deserializer = new();
    private const string Audience = @"{""filter"":[{""gte"":[{""var"":""age""},{""value"":20}]}]}";

    [Test]
    public void TestDeserialize()
    {
        var expected = T.MapOf("filter",
            T.ListOf(T.MapOf("gte", T.ListOf(T.MapOf("var", "age"), T.MapOf("value", 20)))));

        var actual = _deserializer.Deserialize(Audience);
        actual.Should().BeEquivalentTo(expected);
    }

    [Test]
    public void TestDeserializeDoesNotThrow()
    {
        var act = () => _deserializer.Deserialize(Audience[5..]);
        act.Should().NotThrow().And.Subject().Should().BeNull();
    }
}