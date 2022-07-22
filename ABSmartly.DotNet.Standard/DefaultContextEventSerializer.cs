using System;
using System.Text.Json;
using ABSmartly.Json;
using Microsoft.Extensions.Logging;

namespace ABSmartly;

public class DefaultContextEventSerializer : IContextEventSerializer
{
    private readonly ILogger<DefaultContextEventSerializer> _logger;

    public DefaultContextEventSerializer(ILogger<DefaultContextEventSerializer> logger)
    {
        _logger = logger;
    }

    public byte[] Serialize(PublishEvent publishEvent)
    {
        try
        {
            // Todo: ObjectMapper.writeValueAsBytes ??
            return JsonSerializer.SerializeToUtf8Bytes(publishEvent);
        }
        catch (Exception e)
        {
            _logger.LogError(e.Message);
            return null;
        }
    }
}