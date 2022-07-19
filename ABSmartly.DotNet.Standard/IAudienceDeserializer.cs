using System.Collections.Generic;

namespace ABSmartly;

public interface IAudienceDeserializer
{
    Dictionary<string, object> Deserialize(byte[] bytes, int offset, int length);
}