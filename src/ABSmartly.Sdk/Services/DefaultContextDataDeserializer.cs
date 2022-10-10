using System;
using System.IO;
using ABSmartly.Internal;
using ABSmartly.Models;
using Microsoft.Extensions.Logging;
using JsonSerializer = System.Text.Json.JsonSerializer;

namespace ABSmartly.Services;

public class DefaultContextDataDeserializer : IContextDataDeserializer
{
    private readonly ILogger<DefaultContextDataDeserializer> _logger;

    public DefaultContextDataDeserializer(ILoggerFactory loggerFactory)
    {
        _logger = loggerFactory?.CreateLogger<DefaultContextDataDeserializer>();
    }

    public ContextData Deserialize(Stream stream)
    {
        try
        {
            return JsonSerializer.Deserialize<ContextData>(stream, JsonOptionsProvider.Default());
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
                JsonOptionsProvider.Default());
        }
        catch (Exception e)
        {
            _logger?.LogError("{Message}", e.Message);
            return null;
        }
    }
}