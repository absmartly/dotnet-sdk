using System;
using System.Collections.Generic;
using ABSmartly.Services.Json;
using Microsoft.Extensions.Logging;

namespace ABSmartly.Services;

public class DefaultAudienceDeserializer : JsonParserBase, IAudienceDeserializer
{
    private readonly ILogger<DefaultAudienceDeserializer> _logger;

    public DefaultAudienceDeserializer(ILoggerFactory loggerFactory = null)
    {
        _logger = loggerFactory?.CreateLogger<DefaultAudienceDeserializer>();
    }

    public Dictionary<string, object> Deserialize(string audience)
    {
        if (string.IsNullOrWhiteSpace(audience))
        {
            return null;
        }

        try
        {
            var result = ParseJsonString(audience);
            return result;
        }
        catch (Exception e)
        {
            _logger?.LogWarning(e, "Failed to deserialize audience filter - treating as no filter (everyone matches): {Message}", e.Message);
            return null;
        }
    }
}