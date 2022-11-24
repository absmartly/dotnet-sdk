using System.Dynamic;
using System.Text;
using ABSmartly.Models;
using ABSmartly.Services;
using Attribute = ABSmartly.Models.Attribute;

namespace ABSmartly.Sdk.Tests.Services;

[TestFixture]
public class DefaultContextEventSerializerTests
{
    private readonly DefaultContextEventSerializer _serializer = new();
    
    [Test]
    public void TestSerialize()
    {
        using var jsonStream = GetType().Assembly.GetManifestResourceStream("ABSmartly.Sdk.Tests.Resources.publishEvent.json");
        using var streamReader = new StreamReader(jsonStream!, Encoding.UTF8);
        var expectedJson = streamReader.ReadToEnd();

        var testEvent = GetTestEvent();
        var actualBytes = _serializer.Serialize(testEvent);
        actualBytes.Should().NotBeNull();
        actualBytes.Should().NotBeEmpty();

        var actualJson = Encoding.UTF8.GetString(actualBytes); 
        actualJson.Should().Be(expectedJson);
    }
    
    [Test]
    public void TestSerializeDoesNotThrow()
    {
        var testEvent = new PublishEvent
        {
            Attributes = new []
            {
                new Attribute{ Name = "1", Value = GetType() } // Type cannot be serialized at the moment
            }
        };
        
        var actualBytes = _serializer.Serialize(testEvent);
        actualBytes.Should().BeNull();
    }
    
    private PublishEvent GetTestEvent()
    {
        var @event = new PublishEvent
        {
            Hashed = true,
            PublishedAt = 123456789L,
            Units = new Unit[]
            {
                new() { Type = "session_id", Uid = "pAE3a1i5Drs5mKRNq56adA" },
                new() { Type = "user_id", Uid = "JfnnlDI7RTiF9RgfG2JNCw" },
            }
        };

        var stringMap = T.MapOf(
            "amount", 6,
            "value", 5.0,
            "tries", 1,
            "nested", T.MapOf("value", 5),
            "nested_arr", T.MapOf("nested", T.ListOf(1, 2, "test")));

        @event.Exposures = new[]
        {
            new Exposure
            {
                Id = 1, Name = "exp_test_ab", Unit = "session_id", Variant = 1, ExposedAt = 123470000L, Assigned = true,
                Eligible = true, Overridden = false, FullOn = false, Custom = false, AudienceMismatch = true
            },
        };

        @event.Goals = new[]
        {
            new GoalAchievement
                { Name = "goal1", AchievedAt = 123456000L, Properties = new Dictionary<string, object>(stringMap) },
            new GoalAchievement { Name = "goal2", AchievedAt = 123456789L, Properties = null },
        };

        @event.Attributes = new[]
        {
            new Attribute { Name = "attr1", Value = "value1", SetAt = 123456000L },
            new Attribute { Name = "attr2", Value = "value2", SetAt = 123456789L },
            new Attribute { Name = "attr2", Value = null, SetAt = 123450000L },
            new Attribute { Name = "attr3", Value = T.MapOf("nested", T.MapOf("value", 5)), SetAt = 123470000L },
            new Attribute { Name = "attr4", Value = T.MapOf("nested", T.ListOf(1, 2, "test")), SetAt = 123480000L },
        };

        return @event;
    }
}