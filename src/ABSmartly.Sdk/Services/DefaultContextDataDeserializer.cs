using System;
using System.IO;
using System.Text.Json;
using ABSmartly.Models;
using ABSmartly.Services.Json;
using Microsoft.Extensions.Logging;

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
            var result = JsonSerializer.Deserialize<ContextData>(stream, JsonOptionsProvider.Default.SerializerOptions);
            if (result == null)
            {
                _logger?.LogError("Deserialization returned null - invalid or empty context data");
            }
            return result;
        }
        catch (Exception e)
        {
            _logger?.LogError(e, "Failed to deserialize context data: {Message}", e.Message);
            return null;
        }
    }
}