namespace ABSmartly;

public interface IContextEventLogger
{
    void HandleEvent(Context context, EventType eventType, object data);
}