using System;
using System.Collections.Generic;
using System.Text.Json;

namespace ABSmartly;

public class DefaultAudienceDeserializer : IAudienceDeserializer
{
    //private ILogger<DefaultAudienceDeserializer> _logger;
    private object _reader;

    public DefaultAudienceDeserializer()
    {
        //_logger = new Logger<DefaultAudienceDeserializer>();
        //var objectMapper = new ObjectMapper();
        //_reader = new 
    }

    public Dictionary<string, object> Deserialize(byte[] bytes, int offset, int length)
    {
        throw new NotImplementedException();

        try
        {

        }
        catch (Exception e)
        {
            
        }

        //JsonSerializer.Deserialize<Dictionary<string, object>>()
        //System.Text.j
    }
}