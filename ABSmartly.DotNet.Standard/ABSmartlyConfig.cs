using ABSmartlySdk.Interfaces;
using ABSmartlySdk.Temp;
using Microsoft.Extensions.Logging;

namespace ABSmartlySdk;

public class ABSmartlyConfig
{
    public ABSmartlyConfig()
    {
        
    }

    //public ABSmartlyConfig(IOptions<ClientConfiguration> clientConfiguratuin)
    //{

    //}

    //public ABSmartlyConfig(IOptions<ClientConfiguration> clientConfiguratuin,
    //    IHttpClientFactory httpClientFactory)
    //{

    //}

    //public ABSmartlyConfig(
    //    ClientConfiguration clientConfiguration,
    //    IHttpClientFactory httpClientFactory,
    //    ILoggerFactory loggerFactory = null,
    //    Client client = null,

    //    IContextDataProvider contextDataProvider = null,
    //    IContextEventHandler contextEventHandler = null,
    //    IContextEventLogger contextEventLogger = null,
    //    IVariableParser variableParser = null,
    //    IAudienceDeserializer audienceDeserializer = null,
    //    IScheduledExecutorService scheduler = null
    //)
    //{
    //    LoggerFactory = loggerFactory ?? new LoggerFactory();
    //    Client = client ?? new Client(new ClientConfig(clientConfiguration), httpClientFactory, loggerFactory);

    //    ContextDataProvider = contextDataProvider ?? new DefaultContextDataProvider(Client);
    //    ContextEventHandler = contextEventHandler ?? new DefaultContextEventHandler(Client);
    //    ContextEventLogger = contextEventLogger ?? new DefaultContextEventLogger();
    //    VariableParser = variableParser ?? new DefaultVariableParser(loggerFactory);
    //    AudienceDeserializer = audienceDeserializer ?? new DefaultAudienceDeserializer(loggerFactory);
    //    Scheduler = scheduler ?? new ScheduledThreadPoolExecutor(1);
    //}


    //public static ABSmartlyConfig Create(
    //    IHttpClientFactory httpClientFactory,
    //    ILoggerFactory loggerFactory = null,
    //    Client client = null,

    //    IContextDataProvider contextDataProvider = null, 
    //    IContextEventHandler contextEventHandler = null,
    //    IContextEventLogger contextEventLogger = null, 
    //    IVariableParser variableParser = null, 
    //    IAudienceDeserializer audienceDeserializer = null,
    //    IScheduledExecutorService scheduler = null
    //)
    //{
    //    return new ABSmartlyConfig(httpClientFactory, loggerFactory, client, contextDataProvider, contextEventHandler, contextEventLogger, variableParser, audienceDeserializer, scheduler);
    //}

    public IClient Client { get; set; }

    public ILoggerFactory LoggerFactory { get; set; }

    public IContextDataProvider ContextDataProvider { get; set; }

    public IContextEventHandler ContextEventHandler { get; set; }

    public IContextEventLogger ContextEventLogger { get; set; }

    public IVariableParser VariableParser { get; set;  }

    public IAudienceDeserializer AudienceDeserializer { get; set;  }

    public IScheduledExecutorService Scheduler { get; set;  }
}