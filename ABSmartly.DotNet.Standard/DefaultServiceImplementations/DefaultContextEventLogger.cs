using ABSmartlySdk.Definitions;
using ABSmartlySdk.Interfaces;

namespace ABSmartlySdk.DefaultServiceImplementations;

public class DefaultContextEventLogger : IContextEventLogger
{
    public void HandleEvent(Context context, EventType eventType, object data)
    {
        throw new System.NotImplementedException();
    }
}