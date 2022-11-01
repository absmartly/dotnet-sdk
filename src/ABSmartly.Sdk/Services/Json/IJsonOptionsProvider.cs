using System.Text.Json;

namespace ABSmartly.Services.Json;

public interface IJsonOptionsProvider
{
    JsonSerializerOptions SerializerOptions { get; }
}