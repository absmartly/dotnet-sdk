using System.Text;
using System.Text.Json;
using ABSmartly.DotNet.Standard.UnitTests._TestUtils;
using ABSmartlySdk.DefaultServiceImplementations;
using ABSmartlySdk.Json;
using Attribute = ABSmartlySdk.Json.Attribute;

namespace ABSmartly.DotNet.Standard.UnitTests.DefaultServiceImplementationsTests;

[TestFixture]
public class DefaultContextEventSerializerTests
{
    private PublishEvent publishEvent;

    [SetUp]
    public void Setup()
    {
        var units = new[]
        {
            new Unit("session_id", "pAE3a1i5Drs5mKRNq56adA"),
            new Unit("user_id", "JfnnlDI7RTiF9RgfG2JNCw"),
        };

        var exposures = new[]
        {
            new Exposure(1,
                "exp_test_ab", "session_id", 
                1, 123470000L, 
                true, true, false, 
                false, false, true),
        };

        var stringMap = new Dictionary<string, object>
        {
            { "amount", 6 },
            { "value", 5.0 },
            { "tries", 1 },
            { "nested", new Dictionary<string, object>
                {
                    { "value", 5 }
                }
            },
            { "nested_arr", new Dictionary<string, object>
                {
                    { "nested", new List<object> { 1, 2, "test" } }
                }
            }
        };

        var goals = new[]
        {
            new GoalAchievement("goal1", 123456000L, stringMap),
            new GoalAchievement("goal2", 123456789L, null),
        };

        var attributes = new[]
        {
            new Attribute("attr1", "value1", 123456000L),
            new Attribute("attr2", "value2", 123456789L),
            new Attribute("attr2", null, 123450000L),
            new Attribute(
                name: "attr3",
                value: new Dictionary<string, object>
                {
                    { "nested", new Dictionary<string, object> { { "value", 5 } } }
                },
                setAt: 123470000L
            ),
            new Attribute(
                name: "attr4",
                value: new Dictionary<string, object>
                {
                    { "nested", new List<object> { 1, 2, "test" } }
                },
                setAt: 123480000L
            )
        };

        publishEvent = new PublishEvent(
            hashed: true,
            publishedAt: 123456789L,
            units: units,
            exposures: exposures,
            goals: goals,
            attributes: attributes
        );
    }

    [Test]
    public void Serialize_Returns_ExpectedBytes()
    {
        // Arrange
        var serializer = new DefaultContextEventSerializer(null);

        // Act
        var resultBytes = serializer.Serialize(publishEvent);

        // Assert
        var resultString = Encoding.UTF8.GetString(resultBytes);
        var resultPublishEvent = JsonSerializer.Deserialize<PublishEvent>(resultString);
        var resultPublishEventString = resultPublishEvent!.ToString();

        var referencePublishEvent = JsonSerializer.Deserialize<PublishEvent>(TestData.Json.PublishEvent);
        var referencePublishEventString = referencePublishEvent!.ToString();

        Assert.That(resultPublishEventString, Is.EqualTo(referencePublishEventString));
    }

    [Test]
    public void Serialize_NullValue_Returns_Null()
    {
        var serializer = new DefaultContextEventSerializer(null);

        var resultBytes = serializer.Serialize(null);

        Assert.That(resultBytes, Is.Null);
    }
}