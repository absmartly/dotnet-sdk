using ABSmartly.Definitions;
using ABSmartly.Interfaces;

namespace ABSmartly.DefaultServiceImplementations;

public class DefaultContextEventLogger : IContextEventLogger
{
    public void HandleEvent(Context context, EventType eventType, object data)
    {
        throw new System.NotImplementedException();
    }
}