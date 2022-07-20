using System.Collections.Generic;
using Microsoft.Extensions.Logging;

namespace ABSmartly;

public class DefaultAudienceDeserializer : IAudienceDeserializer
{
    private ILogger logger;

    public Dictionary<string, object> Deserialize(byte[] bytes, int offset, int length)
    {
        throw new System.NotImplementedException();
    }
}