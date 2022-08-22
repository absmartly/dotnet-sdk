using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using ABSmartlySdk.Interfaces;
using ABSmartlySdk.Utils.Converters;
using ABSmartlySdk.Utils.NewtonsoftJsonUtils;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using JsonSerializer = Newtonsoft.Json.JsonSerializer;

namespace ABSmartlySdk.DefaultServiceImplementations;

public class DefaultAudienceDeserializer : IAudienceDeserializer
{
    private readonly ILogger<DefaultAudienceDeserializer> _logger;

    public DefaultAudienceDeserializer(ILoggerFactory loggerFactory)
    {
        _logger = loggerFactory?.CreateLogger<DefaultAudienceDeserializer>();
    }

    public Dictionary<string, object> Deserialize(byte[] bytes, int offset, int length)
    {
        try
        {
            var jsonBytes = ByteConverter.Convert(bytes, offset, length);

            using var stream = new MemoryStream(jsonBytes);
            using var reader = new StreamReader(stream, Encoding.UTF8);
            JsonReader jsReader = new JsonTextReader(reader);

            var dictionaryRawJson = JsonSerializer.Create().Deserialize<Dictionary<string, object>>(jsReader);
            var dictionaryParsed = JsonUtils.ParseJsonDictionaryOfStringObject(dictionaryRawJson);
            return dictionaryParsed;
        }
        catch (Exception e)
        {
            _logger?.LogError(e.Message);
            return null;
        }
    }
}