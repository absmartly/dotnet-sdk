using System;
using System.Diagnostics;
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

    private IClient _client;
    private IContextDataProvider _contextDataProvider;
    private IContextEventHandler _contextEventHandler;
    private IContextEventLogger _contextEventLogger;
    private IVariableParser _variableParser;

    private IContextDataDeserializer _contextDataDeserializer;
    private IContextEventSerializer _contextEventSerializer;
    private IExecutor _executor;

    private IAudienceDeserializer _audienceDeserializer;
    private IScheduledExecutorService _scheduler;    

    #endregion

    #region Constructor & Initialization

    /// <summary>
    /// Constructor for .Net Core 3+ / .Net 5+ DI Service
    /// </summary> 
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

            client: config?.Client,
            contextDataProvider: config?.ContextDataProvider,
            contextEventHandler: config?.ContextEventHandler,
            contextEventLogger: config?.ContextEventLogger,

            contextDataDeserializer: config?.ContextDataDeserializer,
            contextEventSerializer: config?.ContextEventSerializer,
            executor: config?.Executor,

            variableParser: config?.VariableParser,
            audienceDeserializer: config?.AudienceDeserializer,
            scheduler: config?.Scheduler
            );
    }
    
    private void Init(
        ClientConfiguration clientConfiguration,
        IHttpClientFactory httpClientFactory,
        ILoggerFactory loggerFactory = null,
        IClient client = null,

        IContextDataProvider contextDataProvider = null,
        IContextEventHandler contextEventHandler = null,
        IContextEventLogger contextEventLogger = null,

        IContextDataDeserializer contextDataDeserializer = null,
        IContextEventSerializer contextEventSerializer = null,
        IExecutor executor = null,

        IVariableParser variableParser = null,
        IAudienceDeserializer audienceDeserializer = null,
        IScheduledExecutorService scheduler = null)
    {
        _loggerFactory = loggerFactory ?? new LoggerFactory();
        _httpClientFactory = httpClientFactory;

        _client = client ?? new Client(_httpClientFactory, _loggerFactory, 
            clientConfiguration,
            _contextDataDeserializer, _contextEventSerializer, _executor
        );

        _contextDataProvider = contextDataProvider ?? new DefaultContextDataProvider(client);
        _contextEventHandler = contextEventHandler ?? new DefaultContextEventHandler(client);
        _contextEventLogger = contextEventLogger ?? new DefaultContextEventLogger();

        _contextDataDeserializer = contextDataDeserializer ?? new DefaultContextDataDeserializer(_loggerFactory);
        _contextEventSerializer = contextEventSerializer ?? new DefaultContextEventSerializer(_loggerFactory);
        _executor = executor ?? new DefaultExecutor();

        _variableParser = variableParser ?? new DefaultVariableParser(_loggerFactory);
        _audienceDeserializer = audienceDeserializer ?? new DefaultAudienceDeserializer(_loggerFactory);
        _scheduler = scheduler ?? new ScheduledThreadPoolExecutor(1);
    }

    #endregion

    #region Context

    public Context CreateContext(ContextConfig config = null)
    {
        var context = new Context(
            clock: Clock.SystemUTC(),
            config: config ?? new ContextConfig(),
            scheduledExecutorService: _scheduler,
            dataTask: _contextDataProvider.GetContextDataAsync(),
            dataProvider: _contextDataProvider,
            eventHandler: _contextEventHandler,
            eventLogger: _contextEventLogger,
            variableParser: _variableParser,
            audienceMatcher: new AudienceMatcher(_audienceDeserializer)
            );

        return context;
    }

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
            catch (Exception e)
            {
                Debug.WriteLine(e);
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