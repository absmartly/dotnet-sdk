using System.Text.Json;
using System.Text.Json.Serialization;

namespace ABSmartly.Internal;

internal static class JsonOptionsProvider
{
    private static JsonSerializerOptions defaultOptions;

    public static JsonSerializerOptions Default()
    {
        return defaultOptions ??= new JsonSerializerOptions()
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DictionaryKeyPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true
        };
    }
}