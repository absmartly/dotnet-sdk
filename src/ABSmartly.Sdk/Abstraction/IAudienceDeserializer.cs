using System.Collections.Generic;

namespace ABSmartly;

public interface IAudienceDeserializer
{
    Dictionary<string, object> Deserialize(string audience);
}