using System;
using System.Text.Json;
using ABSmartly.Interfaces;
using ABSmartly.Json;
using Microsoft.Extensions.Logging;

namespace ABSmartly.DefaultServiceImplementations;

public class DefaultContextEventSerializer : IContextEventSerializer
{
    private readonly ILogger<DefaultContextEventSerializer> _logger;

    public DefaultContextEventSerializer(ILoggerFactory loggerFactory)
    {
        _logger = loggerFactory?.CreateLogger<DefaultContextEventSerializer>();
    }

    public byte[] Serialize(PublishEvent publishEvent)
    {
        try
        {
            var serializedBytes = JsonSerializer.SerializeToUtf8Bytes(publishEvent);
            return serializedBytes;
        }
        catch (Exception e)
        {
            _logger?.LogError(e.Message);
            return null;
        }
    }
}