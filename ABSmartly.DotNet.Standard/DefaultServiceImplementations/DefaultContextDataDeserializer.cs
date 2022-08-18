using ABSmartly.Interfaces;
using ABSmartly.Json;
using Microsoft.Extensions.Logging;
using System;
using System.IO;
using System.Text;
using ABSmartly.Utils.Converters;
using Newtonsoft.Json;

namespace ABSmartly.DefaultServiceImplementations;

public class DefaultContextDataDeserializer : IContextDataDeserializer
{
    private readonly ILogger<DefaultContextDataDeserializer> _logger;

    public DefaultContextDataDeserializer(ILoggerFactory loggerFactory)
    {
        _logger = loggerFactory?.CreateLogger<DefaultContextDataDeserializer>();
    }

    public ContextData Deserialize(byte[] bytes, int offset, int length)
    {
        try
        {
            var jsonBytes = ByteConverter.Convert(bytes, offset, length);

            using var stream = new MemoryStream(jsonBytes);
            using var reader = new StreamReader(stream, Encoding.UTF8);
            JsonReader jsReader = new JsonTextReader(reader);

            var dictionaryRawJson = JsonSerializer.Create().Deserialize<ContextData>(jsReader);
            return dictionaryRawJson;
        }
        catch (Exception e)
        {
            _logger?.LogError(e.Message);
            return null;
        }
    }
}