using System;
using System.Text.Json;
using ABSmartlySdk.Interfaces;
using ABSmartlySdk.Json;
using Microsoft.Extensions.Logging;

namespace ABSmartlySdk.DefaultServiceImplementations;

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
            if (publishEvent is null)
                throw new ArgumentNullException(nameof(publishEvent));

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