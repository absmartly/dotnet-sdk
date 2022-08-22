using System.Collections.Generic;

namespace ABSmartlySdk.Interfaces;

public interface IAudienceDeserializer
{
    Dictionary<string, object> Deserialize(byte[] bytes, int offset, int length);
}