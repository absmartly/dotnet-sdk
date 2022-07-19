using ABSmartly.Temp;

namespace ABSmartly;

public class ABSmartlyConfig
{
    private readonly ContextDataProvider _contextDataProvider;
    private readonly ContextEventHandler _contextEventHandler;

    private readonly ContextEventLogger _contextEventLogger;
    private readonly VariableParser _variableParser;

    private readonly AudienceDeserializer _audienceDeserializer;
    private readonly ScheduledExecutorService _scheduler;
    private readonly Client _client;

    public ABSmartlyConfig(
        ContextDataProvider contextDataProvider = null, 
        ContextEventHandler contextEventHandler = null,
        ContextEventLogger contextEventLogger = null, 
        VariableParser variableParser = null, 
        AudienceDeserializer audienceDeserializer = null,
        ScheduledExecutorService scheduler = null,
        Client client = null
        )
    {
        _contextDataProvider = contextDataProvider ?? new ContextDataProvider();
        _contextEventHandler = contextEventHandler ?? new ContextEventHandler();
        _contextEventLogger = contextEventLogger ?? new ContextEventLogger();
        _variableParser = variableParser ?? new VariableParser();
        _audienceDeserializer = audienceDeserializer ?? new AudienceDeserializer();
        _scheduler = scheduler ?? new ScheduledExecutorService();
        _client = client ?? new Client();
    }


    public static ABSmartlyConfig Create(
        ContextDataProvider contextDataProvider = null, 
        ContextEventHandler contextEventHandler = null,
        ContextEventLogger contextEventLogger = null, 
        VariableParser variableParser = null, 
        AudienceDeserializer audienceDeserializer = null,
        ScheduledExecutorService scheduler = null,
        Client client = null
        )
    {
        return new ABSmartlyConfig(contextDataProvider, contextEventHandler, contextEventLogger, variableParser, audienceDeserializer, scheduler, client);
    }

    // Todo: Tegu: Setters probably not needed
    // Todo: Tegu: Add public Getters if needed

    public ContextDataProvider ContextDataProvider => _contextDataProvider;
    public ContextEventHandler ContextEventHandler => _contextEventHandler;

    public ContextEventLogger ContextEventLogger => _contextEventLogger;
    public VariableParser VariableParser => _variableParser;

    public AudienceDeserializer AudienceDeserializer => _audienceDeserializer;
    public ScheduledExecutorService Scheduler => _scheduler;
    public Client Client => _client;

}