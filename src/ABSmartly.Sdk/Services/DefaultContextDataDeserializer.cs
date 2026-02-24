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
                var message = "Deserialization returned null - invalid or empty context data";
                _logger?.LogError(message);
                Console.Error.WriteLine($"[ABSmartly] ERROR: {message}");
            }
            return result;
        }
        catch (Exception e)
        {
            var message = $"Failed to deserialize context data: {e.Message}";
            _logger?.LogError(e, message);
            Console.Error.WriteLine($"[ABSmartly] ERROR: {message}");
            return null;
        }
    }
}