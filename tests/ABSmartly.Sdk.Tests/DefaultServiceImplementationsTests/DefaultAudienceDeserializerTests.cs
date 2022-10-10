using System.Text;
using ABSmartly.DotNet.Standard.UnitTests._TestUtils;
using ABSmartlySdk.DefaultServiceImplementations;

namespace ABSmartly.DotNet.Standard.UnitTests.DefaultServiceImplementationsTests;

[TestFixture]
public class DefaultAudienceDeserializerTests
{
    [Test]
    public void Deserialize_Returns_ExpectedResult()
    {
        // Arrange
        var deserializer = new DefaultAudienceDeserializer(null);
        var audienceBytes = Encoding.UTF8.GetBytes(TestData.Json.Audience);

        // Act
        var resultDictionary = deserializer.Deserialize(audienceBytes, 0, audienceBytes.Length);
        
        // Assert
        var filterList = resultDictionary["filter"] as List<object>;
        var filterDict = filterList?[0] as Dictionary<string, object>;

        var gteList = filterDict?["gte"] as List<object>;
        var gteListItem0 = gteList?[0] as Dictionary<string, object>;
        var gteListItem1 = gteList?[1] as Dictionary<string, object>;

        var gteListItem0Value = gteListItem0?["var"];
        var gteListItem1Value = gteListItem1?["value"];

        Assert.That(gteListItem0Value, Is.EqualTo("age"));
        Assert.That(gteListItem1Value, Is.EqualTo(20));
    }

    [Test]
    public void Deserialize_InvalidValue_Returns_Null()
    {
        // Arrange
        var deserializer = new DefaultAudienceDeserializer(null);
        var audienceBytes = Encoding.UTF8.GetBytes(TestData.Json.Audience);

        // Act
        // 'ruin' the bytes with random offset and length..
        var resultDictionary = deserializer.Deserialize(audienceBytes, 0 + 2, audienceBytes.Length - 3);

        // Assert
        Assert.That(resultDictionary, Is.Null);
    }
}