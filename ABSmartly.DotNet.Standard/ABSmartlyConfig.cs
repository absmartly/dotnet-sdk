using ABSmartlySdk.Interfaces;
using ABSmartlySdk.Temp;

namespace ABSmartlySdk;

public class ABSmartlyConfig
{
    //public ABSmartlyConfig()
    //{
        
    //}

    public IClient Client { get; set; }

    public IContextDataProvider ContextDataProvider { get; set; }

    public IContextEventHandler ContextEventHandler { get; set; }

    public IContextEventLogger ContextEventLogger { get; set; }

    public IContextDataDeserializer ContextDataDeserializer { get; set; }

    public IContextEventSerializer ContextEventSerializer { get; set; }

    public IExecutor Executor { get; set; }


    public IVariableParser VariableParser { get; set;  }

    public IAudienceDeserializer AudienceDeserializer { get; set;  }

    public IScheduledExecutorService Scheduler { get; set;  }
}