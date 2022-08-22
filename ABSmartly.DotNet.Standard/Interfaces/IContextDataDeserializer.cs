using ABSmartlySdk.Json;

namespace ABSmartlySdk.Interfaces;

public interface IContextDataDeserializer
{
    ContextData Deserialize(byte[] bytes, int offset, int length);
}