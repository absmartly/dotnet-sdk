using System.Text.Json;
using System.Text.Json.Serialization;

namespace ABSmartly.Services.Json;

public class JsonOptionsProvider : IJsonOptionsProvider
{
    private static IJsonOptionsProvider defaultOptions;

    public static IJsonOptionsProvider Default =>
        defaultOptions ??= new JsonOptionsProvider
        {
            SerializerOptions = new JsonSerializerOptions
            {
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                DictionaryKeyPolicy = JsonNamingPolicy.CamelCase,
                PropertyNameCaseInsensitive = true
            }
        };

    public JsonSerializerOptions SerializerOptions { get; private set; }
}