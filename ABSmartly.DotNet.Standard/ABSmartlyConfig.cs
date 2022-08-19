using System.Net.Http;
using ABSmartly.DefaultServiceImplementations;
using ABSmartly.Interfaces;
using ABSmartly.Temp;
using Microsoft.Extensions.Logging;

namespace ABSmartly;

public class ABSmartlyConfig
{
    public ABSmartlyConfig(
        IHttpClientFactory httpClientFactory,
        ILoggerFactory loggerFactory = null,
        Client client = null,

        IContextDataProvider contextDataProvider = null, 
        IContextEventHandler contextEventHandler = null,
        IContextEventLogger contextEventLogger = null, 
        IVariableParser variableParser = null, 
        IAudienceDeserializer audienceDeserializer = null,
        IScheduledExecutorService scheduler = null
    )
    {
        LoggerFactory = loggerFactory ?? new LoggerFactory();
        Client = client ?? new Client(new ClientConfig(), httpClientFactory, loggerFactory);

        ContextDataProvider = contextDataProvider ?? new DefaultContextDataProvider(Client);
        ContextEventHandler = contextEventHandler ?? new DefaultContextEventHandler(Client);
        ContextEventLogger = contextEventLogger ?? new DefaultContextEventLogger();
        VariableParser = variableParser ?? new DefaultVariableParser(loggerFactory);
        AudienceDeserializer = audienceDeserializer ?? new DefaultAudienceDeserializer(loggerFactory);
        Scheduler = scheduler ?? new ScheduledThreadPoolExecutor(1);
    }


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

    public Client Client { get; }

    public ILoggerFactory LoggerFactory { get; } 

    public IContextDataProvider ContextDataProvider { get; }

    public IContextEventHandler ContextEventHandler { get; }

    public IContextEventLogger ContextEventLogger { get; }

    public IVariableParser VariableParser { get; }

    public IAudienceDeserializer AudienceDeserializer { get; }

    public IScheduledExecutorService Scheduler { get; }
}