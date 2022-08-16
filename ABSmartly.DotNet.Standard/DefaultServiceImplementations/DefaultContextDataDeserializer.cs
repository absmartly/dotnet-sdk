using ABSmartly.Interfaces;
using ABSmartly.Json;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using System;

namespace ABSmartly.DefaultServiceImplementations;

public class DefaultContextDataDeserializer : IContextDataDeserializer
{
    private readonly ILogger<DefaultContextDataDeserializer> _logger;

    public DefaultContextDataDeserializer(ILoggerFactory loggerFactory)
    {
        _logger = loggerFactory.CreateLogger<DefaultContextDataDeserializer>();
    }

    public ContextData Deserialize(byte[] bytes, int offset, int length)
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
            var data = JsonSerializer.Deserialize<ContextData>(ref jsonUtfReader);
            return data;
        }
        catch (Exception e)
        {
            _logger.LogError(e.Message);
            return null;
        }
    }
}