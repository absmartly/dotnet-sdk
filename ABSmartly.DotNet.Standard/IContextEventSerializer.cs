using ABSmartly.Json;

namespace ABSmartly;

public interface IContextEventSerializer
{
    byte[] Serialize(PublishEvent publishEvent);
}