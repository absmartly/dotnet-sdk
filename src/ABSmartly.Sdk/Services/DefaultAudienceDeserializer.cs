using System;
using System.Collections.Generic;
using ABSmartly.Services.Json;
using Microsoft.Extensions.Logging;

namespace ABSmartly.Services;

public class DefaultAudienceDeserializer : JsonParserBase, IAudienceDeserializer
{
    private readonly ILogger<DefaultAudienceDeserializer> _logger;

    public DefaultAudienceDeserializer(ILoggerFactory loggerFactory)
    {
        _logger = loggerFactory?.CreateLogger<DefaultAudienceDeserializer>();
    }

    public Dictionary<string, object> Deserialize(string audience)
    {
        try
        {
            return ParseJsonString(audience);
        }
        catch (Exception e)
        {
            _logger?.LogError(e.Message);
            return null;
        }
    }
}