using ABSmartlySdk.Definitions;

namespace ABSmartlySdk.Interfaces;

public interface IContextEventLogger
{
    void HandleEvent(Context context, EventType eventType, object data);
}