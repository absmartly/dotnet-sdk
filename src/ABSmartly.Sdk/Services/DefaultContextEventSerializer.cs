using System;
using System.Text.Json;
using ABSmartly.Models;
using ABSmartly.Services.Json;
using Microsoft.Extensions.Logging;

namespace ABSmartly.Services;

public class DefaultContextEventSerializer : IContextEventSerializer
{
    private readonly ILogger<DefaultContextEventSerializer> _logger;

    public DefaultContextEventSerializer(ILoggerFactory loggerFactory = null)
    {
        _logger = loggerFactory?.CreateLogger<DefaultContextEventSerializer>();
    }

    public byte[] Serialize(PublishEvent publishEvent)
    {
        try
        {
            return JsonSerializer.SerializeToUtf8Bytes(publishEvent, JsonOptionsProvider.Default.SerializerOptions);
        }
        catch (Exception e)
        {
            _logger?.LogError("{Message}", e.Message);
            return null;
        }
    }
}