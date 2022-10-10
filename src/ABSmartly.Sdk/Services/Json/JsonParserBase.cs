#nullable enable
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace ABSmartly.Services.Json;

public abstract class JsonParserBase
{
    protected static Dictionary<string, object>? ParseJsonString(string json)
    {
        var stringReader = new StringReader(json);
        return ParseJsonFromReader(stringReader);
    }

    protected static Dictionary<string, object>? ParseJsonStream(Stream stream)
    {
        var streamReader = new StreamReader(stream, Encoding.UTF8);
        return ParseJsonFromReader(streamReader);
    }

    private static Dictionary<string, object>? ParseJsonFromReader(TextReader reader)
    {
        var jsonReader = new JsonTextReader(reader);
        var jObject = JObject.Load(jsonReader);
        var parsedObject = ParseToken(jObject);
        return parsedObject as Dictionary<string, object>;
    }

    private static object? ParseToken(JToken? token)
    {
        return token switch
        {
            JArray jArray => jArray.Select(ParseToken).ToList(),
            JObject jObject => (jObject as IDictionary<string, JToken?>).ToDictionary(x => x.Key,
                x => ParseToken(x.Value)),
            JValue jValue => jValue.Value,
            _ => token
        };
    }
}