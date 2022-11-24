namespace ABSmartly;

public interface IContextEventLogger
{
    void HandleEvent(IContext context, EventType eventType, object data);
}