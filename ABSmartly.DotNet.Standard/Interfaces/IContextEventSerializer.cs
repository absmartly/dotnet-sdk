using ABSmartlySdk.Json;

namespace ABSmartlySdk.Interfaces;

public interface IContextEventSerializer
{
    byte[] Serialize(PublishEvent publishEvent);
}