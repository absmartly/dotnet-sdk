using System.Text;
using ABSmartly.Services.Json;

namespace ABSmartly.Sdk.Tests.Services.Json;

[TestFixture]
public class JsonParserBaseTests: JsonParserBase
{
    [Test]
    public void TestParseJsonString()
    {
        ParseJsonString("{}").Should().NotBeNull();
        ParseJsonString(@"{""a"": 1}").Should().BeEquivalentTo(new Dictionary<string, object>{ ["a"] = 1});
        ParseJsonString(@"{""a"": ""1""}").Should().BeEquivalentTo(new Dictionary<string, object>{ ["a"] = "1"});
        ParseJsonString(@"{""a"": { ""b"": 1 }}").Should().BeEquivalentTo(new Dictionary<string, object>
            { ["a"] = new Dictionary<string, object> { ["b"] = 1 } });
        ParseJsonString(@"{""a"": [""b"", 1]}").Should().BeEquivalentTo(new Dictionary<string, object>
            { ["a"] = new List<object> { "b", 1 } });

        Action act;
        act = () => ParseJsonString("");
        act.Should().Throw<Exception>();
        
        act = () => ParseJsonString("[]");
        act.Should().Throw<Exception>();
        
        act = () => ParseJsonString("[{}]");
        act.Should().Throw<Exception>();
    }
    
    [Test]
    public void TestParseJsonStream()
    {
        ParseJsonStream(TextStream("{}")).Should().NotBeNull();
        ParseJsonStream(TextStream(@"{""a"": 1}")).Should().BeEquivalentTo(new Dictionary<string, object>{ ["a"] = 1});
        ParseJsonStream(TextStream(@"{""a"": ""1""}")).Should().BeEquivalentTo(new Dictionary<string, object>{ ["a"] = "1"});
        ParseJsonStream(TextStream(@"{""a"": { ""b"": 1 }}")).Should().BeEquivalentTo(new Dictionary<string, object>
            { ["a"] = new Dictionary<string, object> { ["b"] = 1 } });
        ParseJsonStream(TextStream(@"{""a"": [""b"", 1]}")).Should().BeEquivalentTo(new Dictionary<string, object>
            { ["a"] = new List<object> { "b", 1 } });
        
        Action act;
        act = () => ParseJsonStream(TextStream(""));
        act.Should().Throw<Exception>();
        
        act = () => ParseJsonStream(TextStream("[]"));
        act.Should().Throw<Exception>();
        
        act = () => ParseJsonStream(TextStream("[{}]"));
        act.Should().Throw<Exception>();

        Stream TextStream(string text)
        {
            var bytes = Encoding.UTF8.GetBytes(text);
            var stream = new MemoryStream(bytes);
            return stream;
        }
    }
}