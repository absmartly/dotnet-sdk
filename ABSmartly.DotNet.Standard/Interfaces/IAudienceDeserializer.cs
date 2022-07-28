using System.Collections.Generic;

namespace ABSmartly.Interfaces;

public interface IAudienceDeserializer
{
    Dictionary<string, object> Deserialize(byte[] bytes, int offset, int length);
}