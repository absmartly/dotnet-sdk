using System;
using System.Net.Http;
using ABSmartlySdk.DefaultServiceImplementations;
using ABSmartlySdk.DotNet.Time;
using ABSmartlySdk.Interfaces;
using ABSmartlySdk.Temp;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ABSmartlySdk;

public class ABSmartly : IDisposable
{
    #region Private Services

    private ILoggerFactory _loggerFactory;
    private IHttpClientFactory _httpClientFactory;

    private Client _client;
    private IContextDataProvider _contextDataProvider;
    private IContextEventHandler _contextEventHandler;
    private IContextEventLogger _contextEventLogger;
    private IVariableParser _variableParser;

    private IAudienceDeserializer _audienceDeserializer;
    private IScheduledExecutorService _scheduler;    

    #endregion


    //public static ABSmartly Create(ABSmartlyConfig config)
    //{
    //    return new ABSmartly(config);
    //}

    //public ABSmartly(IOptions<ABSmartlyConfig> c)
    //{
    //    Init(c.Value);
    //}

    //public ABSmartly()
    //{
        
    //}

    //public ABSmartly(ABSmartlyConfig config)
    //{
    //    //Init(config);
    //}



    public ABSmartly(
        IHttpClientFactory httpClientFactory, 
        ILoggerFactory loggerFactory,
        IOptions<ABSmartlyConfig> configOptions, 
        IOptions<ClientConfiguration> clientConfiguration)
    {
        var config = configOptions?.Value;
        Init(clientConfiguration.Value, 
            httpClientFactory: httpClientFactory,
            loggerFactory: loggerFactory,

            client: null,
            contextDataProvider: config?.ContextDataProvider,
            contextEventHandler: config?.ContextEventHandler,
            contextEventLogger: config?.ContextEventLogger,
            variableParser: config?.VariableParser,
            audienceDeserializer: config?.AudienceDeserializer,
            scheduler: config?.Scheduler
            );
    }




    private void Init(
        ClientConfiguration clientConfiguration,
        IHttpClientFactory httpClientFactory,
        ILoggerFactory loggerFactory = null,
        Client client = null,

        IContextDataProvider contextDataProvider = null,
        IContextEventHandler contextEventHandler = null,
        IContextEventLogger contextEventLogger = null,
        IVariableParser variableParser = null,
        IAudienceDeserializer audienceDeserializer = null,
        IScheduledExecutorService scheduler = null)
    {
        _loggerFactory = loggerFactory ?? new LoggerFactory();
        _httpClientFactory = httpClientFactory;

        _client = client ?? new Client(new ClientConfig(clientConfiguration), httpClientFactory, loggerFactory);

        _contextDataProvider = contextDataProvider ?? new DefaultContextDataProvider(client);
        _contextEventHandler = contextEventHandler ?? new DefaultContextEventHandler(client);
        _contextEventLogger = contextEventLogger ?? new DefaultContextEventLogger();
        _variableParser = variableParser ?? new DefaultVariableParser(loggerFactory);
        _audienceDeserializer = audienceDeserializer ?? new DefaultAudienceDeserializer(loggerFactory);
        _scheduler = scheduler ?? new ScheduledThreadPoolExecutor(1);


        //_client = config.Client;
        //_contextDataProvider = config.ContextDataProvider;
        //_contextEventHandler = config.ContextEventHandler;
        //_contextEventLogger = config.ContextEventLogger;
        //_variableParser = config.VariableParser;
        //_audienceDeserializer = config.AudienceDeserializer;
        //_scheduler = config.Scheduler;

        //if (_contextDataProvider == null || _contextEventHandler == null)
        //{
        //    _client = config.Client;
        //    if (_client is null) 
        //    {
        //        throw new ArgumentNullException(nameof(_client), "Missing Client instance");
        //    }

        //    _contextDataProvider ??= new DefaultContextDataProvider(_client);
        //    _contextEventHandler ??= new DefaultContextEventHandler(_client);
        //}

        //_variableParser ??= new DefaultVariableParser(config.LoggerFactory);
        //_audienceDeserializer ??= new DefaultAudienceDeserializer(config.LoggerFactory);
        //_scheduler ??= new ScheduledThreadPoolExecutor(1);
    }

    //private void Init(ABSmartlyConfig config, ClientConfiguration clientConfiguration)
    //{



    //    _client = config.Client;
    //    _contextDataProvider = config.ContextDataProvider;
    //    _contextEventHandler = config.ContextEventHandler;
    //    _contextEventLogger = config.ContextEventLogger;
    //    _variableParser = config.VariableParser;
    //    _audienceDeserializer = config.AudienceDeserializer;
    //    _scheduler = config.Scheduler;

    //    //if (_contextDataProvider == null || _contextEventHandler == null)
    //    //{
    //    //    _client = config.Client;
    //    //    if (_client is null) 
    //    //    {
    //    //        throw new ArgumentNullException(nameof(_client), "Missing Client instance");
    //    //    }

    //    //    _contextDataProvider ??= new DefaultContextDataProvider(_client);
    //    //    _contextEventHandler ??= new DefaultContextEventHandler(_client);
    //    //}

    //    //_variableParser ??= new DefaultVariableParser(config.LoggerFactory);
    //    //_audienceDeserializer ??= new DefaultAudienceDeserializer(config.LoggerFactory);
    //    //_scheduler ??= new ScheduledThreadPoolExecutor(1);
    //}

    #region Context

    public Context CreateContext(ContextConfig config)
    {
        var context = new Context(
            clock: Clock.SystemUTC(),
            config: config,
            scheduledExecutorService: _scheduler,
            dataTask: _contextDataProvider.GetContextDataAsync(),
            dataProvider: _contextDataProvider,
            eventHandler: _contextEventHandler,
            eventLogger: _contextEventLogger,
            variableParser: _variableParser,
            audienceMatcher: new AudienceMatcher(_audienceDeserializer)
            );

        return context;
        //return Context.Create(Clock.SystemUTC(), config, _scheduler, _contextDataProvider.GetContextDataAsync(),
        //    _contextDataProvider, _contextEventHandler, _contextEventLogger, _variableParser,
        //    new AudienceMatcher(_audienceDeserializer));
    }

    //public Context CreateContext(ContextConfig config, ContextData data) 
    //{
    //    var context = new Context(
    //        clock: Clock.SystemUTC(),
    //        config: config,
    //        scheduledExecutorService: _scheduler,
    //        dataTask: Task.FromResult(data),
    //        dataProvider: _contextDataProvider,
    //        eventHandler: _contextEventHandler,
    //        eventLogger: _contextEventLogger,
    //        variableParser: _variableParser,
    //        audienceMatcher: new AudienceMatcher(_audienceDeserializer)
    //    );

    //    return context;
    //    //return Context.Create(Clock.SystemUTC(), config, _scheduler, Task.FromResult(data),
    //    //    _contextDataProvider, _contextEventHandler, _contextEventLogger, _variableParser,
    //    //    new AudienceMatcher(_audienceDeserializer));
    //}

    //public Task<ContextData> GetContextDataAsync()
    //{
    //    return _contextDataProvider.GetContextDataAsync();
    //}    

    #endregion

    #region IDisposable

    public void Close()
    {
        if (_client is not null)
        {
            //_client.Close();
            _client = null;
        }

        if (_scheduler is not null) 
        {
            try
            {
                //_scheduler.AwaitTermination(5000, TimeUnit.MILLISECONDS);
            }
            catch (Exception ignored)
            {

            }

            _scheduler = null;
        }
    }

    public void Dispose()
    {
        Close();
    }

    #endregion
}