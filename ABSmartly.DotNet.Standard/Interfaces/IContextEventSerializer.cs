using ABSmartly.Json;

namespace ABSmartly.Interfaces;

public interface IContextEventSerializer
{
    byte[] Serialize(PublishEvent publishEvent);
}