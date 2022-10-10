using System;
using System.Text.Json;
using ABSmartly.Internal;
using ABSmartly.Models;
using Microsoft.Extensions.Logging;

namespace ABSmartly.Services;

public class DefaultContextEventSerializer : IContextEventSerializer
{
    private readonly ILogger<DefaultContextEventSerializer> _logger;

    public DefaultContextEventSerializer(ILoggerFactory loggerFactory)
    {
        _logger = loggerFactory?.CreateLogger<DefaultContextEventSerializer>();
    }

    public byte[] Serialize(PublishEvent publishEvent)
    {
        if (publishEvent is null)
            throw new ArgumentNullException(nameof(publishEvent));

        try
        {
            return JsonSerializer.SerializeToUtf8Bytes(publishEvent, JsonOptionsProvider.Default());
        }
        catch (Exception e)
        {
            _logger?.LogError(e.Message);
            return null;
        }
    }
}