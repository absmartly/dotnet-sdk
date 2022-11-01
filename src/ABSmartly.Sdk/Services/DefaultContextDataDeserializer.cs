using System;
using System.IO;
using ABSmartly.Models;
using ABSmartly.Services.Json;
using Microsoft.Extensions.Logging;
using JsonSerializer = System.Text.Json.JsonSerializer;

namespace ABSmartly.Services;

public class DefaultContextDataDeserializer : IContextDataDeserializer
{
    private readonly ILogger<DefaultContextDataDeserializer> _logger;

    public DefaultContextDataDeserializer(ILoggerFactory loggerFactory = null)
    {
        _logger = loggerFactory?.CreateLogger<DefaultContextDataDeserializer>();
    }

    public ContextData Deserialize(Stream stream)
    {
        try
        {
            return JsonSerializer.Deserialize<ContextData>(stream, JsonOptionsProvider.Default.SerializerOptions);
        }
        catch (Exception e)
        {
            _logger?.LogError("{Message}", e.Message);
            return null;
        }
    }

    public ContextData Deserialize(byte[] bytes, int offset, int length)
    {
        try
        {
            return JsonSerializer.Deserialize<ContextData>(new ReadOnlySpan<byte>(bytes, offset, length),
                JsonOptionsProvider.Default.SerializerOptions);
        }
        catch (Exception e)
        {
            _logger?.LogError("{Message}", e.Message);
            return null;
        }
    }
}