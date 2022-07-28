using ABSmartly.Definitions;

namespace ABSmartly.Interfaces;

public interface IContextEventLogger
{
    void HandleEvent(Context context, EventType eventType, object data);
}