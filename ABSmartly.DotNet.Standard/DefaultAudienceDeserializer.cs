using System.Collections.Generic;

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
        //System.Text.j
    }
}