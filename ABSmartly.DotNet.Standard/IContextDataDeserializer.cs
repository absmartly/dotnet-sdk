using ABSmartly.Json;

namespace ABSmartly;

public interface IContextDataDeserializer
{
    ContextData Deserialize(byte[] bytes, int offset, int length);
}