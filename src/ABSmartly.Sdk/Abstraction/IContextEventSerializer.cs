using ABSmartly.Models;

namespace ABSmartly;

public interface IContextEventSerializer
{
    byte[] Serialize(PublishEvent publishEvent);
}