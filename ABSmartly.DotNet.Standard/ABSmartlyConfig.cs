﻿using ABSmartly.Temp;

namespace ABSmartly;

public class ABSmartlyConfig
{
    private readonly IContextDataProvider _contextDataProvider;
    private readonly IContextEventHandler _contextEventHandler;

    private readonly ContextEventLogger _contextEventLogger;
    private readonly VariableParser _variableParser;

    private readonly IAudienceDeserializer _audienceDeserializer;
    private readonly ScheduledExecutorService _scheduler;
    private readonly Client _client;

    public ABSmartlyConfig(
        IContextDataProvider contextDataProvider = null, 
        IContextEventHandler contextEventHandler = null,
        ContextEventLogger contextEventLogger = null, 
        VariableParser variableParser = null, 
        IAudienceDeserializer audienceDeserializer = null,
        ScheduledExecutorService scheduler = null,
        Client client = null
        )
    {
        _contextDataProvider = contextDataProvider ?? new IContextDataProvider();
        _contextEventHandler = contextEventHandler ?? new IContextEventHandler();
        _contextEventLogger = contextEventLogger ?? new ContextEventLogger();
        _variableParser = variableParser ?? new VariableParser();
        _audienceDeserializer = audienceDeserializer ?? new IAudienceDeserializer();
        _scheduler = scheduler ?? new ScheduledExecutorService();
        _client = client ?? new Client();
    }


    public static ABSmartlyConfig Create(
        IContextDataProvider contextDataProvider = null, 
        IContextEventHandler contextEventHandler = null,
        ContextEventLogger contextEventLogger = null, 
        VariableParser variableParser = null, 
        IAudienceDeserializer audienceDeserializer = null,
        ScheduledExecutorService scheduler = null,
        Client client = null
        )
    {
        return new ABSmartlyConfig(contextDataProvider, contextEventHandler, contextEventLogger, variableParser, audienceDeserializer, scheduler, client);
    }

    // Todo: Tegu: Setters probably not needed
    // Todo: Tegu: Add public Getters if needed

    public IContextDataProvider ContextDataProvider => _contextDataProvider;
    public IContextEventHandler ContextEventHandler => _contextEventHandler;

    public ContextEventLogger ContextEventLogger => _contextEventLogger;
    public VariableParser VariableParser => _variableParser;

    public IAudienceDeserializer AudienceDeserializer => _audienceDeserializer;
    public ScheduledExecutorService Scheduler => _scheduler;
    public Client Client => _client;

}