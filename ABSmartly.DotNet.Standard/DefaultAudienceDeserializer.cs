using System;
using System.Collections.Generic;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace ABSmartly;

public class DefaultAudienceDeserializer : IAudienceDeserializer
{
    private readonly ILogger<DefaultAudienceDeserializer> _logger;

    public DefaultAudienceDeserializer(ILogger<DefaultAudienceDeserializer> logger)
    {
        _logger = logger;
    }

    public Dictionary<string, object> Deserialize(byte[] bytes, int offset, int length)
    {
        try
        {
            // Todo: Manual implementation to support offset and length on the byte array, review!!

            var jsonBytes = new byte[length];

            var b = 0;
            for (var i = offset; i < length; i++)
            {
                jsonBytes[b] = bytes[i];
                b++;
            }

            var jsonUtfReader = new Utf8JsonReader(jsonBytes);
            var dict = JsonSerializer.Deserialize<Dictionary<string, object>>(ref jsonUtfReader);
            return dict;
        }
        catch (Exception e)
        {
            _logger.LogError(e.Message);
            return null;
        }
    }
}