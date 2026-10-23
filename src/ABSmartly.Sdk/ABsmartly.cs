using System;
using System.Threading.Tasks;
using ABSmartly.Concurrency;
using ABSmartly.Extensions;
using ABSmartly.Models;
using ABSmartly.Services;
using ABSmartly.Time;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ABSmartly;

public class ABsmartly
{
    public const string HttpClientName = "ABSmartlySDK.HttpClient";

    private IAudienceDeserializer _audienceDeserializer;

    private IABSmartlyServiceClient _client;

    private IContextDataDeserializer _contextDataDeserializer;
    private IContextDataProvider _contextDataProvider;
    private IContextPublisher _contextEventHandler;
    private IContextEventLogger _contextEventLogger;
    private IContextEventSerializer _contextEventSerializer;
    private IABsmartlyHttpClientFactory _httpClientFactory;

    private ILoggerFactory _loggerFactory;
    private IVariableParser _variableParser;

    [ActivatorUtilitiesConstructor]
    public ABsmartly(IABsmartlyHttpClientFactory httpClientFactory,
        IOptions<ABSmartlyServiceConfiguration> serviceConfiguration,
        IOptions<ABsmartlyConfig> configOptions,
        ILoggerFactory loggerFactory)
    {
        Init(httpClientFactory, serviceConfiguration.Value, configOptions?.Value, loggerFactory);
    }

    public ABsmartly(IABsmartlyHttpClientFactory httpClientFactory,
        ABSmartlyServiceConfiguration serviceConfiguration,
        ABsmartlyConfig config = null,
        ILoggerFactory loggerFactory = null)
    {
        Init(httpClientFactory, serviceConfiguration, config, loggerFactory);
    }

    private void Init(IABsmartlyHttpClientFactory httpClientFactory,
        ABSmartlyServiceConfiguration serviceConfiguration,
        ABsmartlyConfig config = null,
        ILoggerFactory loggerFactory = null)
    {
        _loggerFactory = loggerFactory ?? new LoggerFactory();
        _httpClientFactory = httpClientFactory ??
                             throw new ArgumentNullException(nameof(httpClientFactory),
                                 "Missing HTTP client factory configuration");

        _contextDataDeserializer =
            config?.ContextDataDeserializer ?? new DefaultContextDataDeserializer(_loggerFactory);
        _contextEventSerializer = config?.ContextEventSerializer ?? new DefaultContextEventSerializer(_loggerFactory);

        _client = new ABSmartlyService(
            serviceConfiguration,
            _httpClientFactory,
            _contextDataDeserializer,
            _contextEventSerializer,
            _loggerFactory);

        _contextDataProvider = config?.ContextDataProvider ?? new DefaultContextDataProvider(_client);
        _contextEventHandler = config?.ContextPublisher ?? new DefaultContextPublisher(_client);
        _contextEventLogger = config?.ContextEventLogger;

        _variableParser = config?.VariableParser ?? new DefaultVariableParser(_loggerFactory);
        _audienceDeserializer = config?.AudienceDeserializer ?? new DefaultAudienceDeserializer(_loggerFactory);
    }

    public IContext CreateContext(ContextConfig config)
    {
        return AsyncHelpers.RunSync(async () => await CreateContextAsync(config));
    }

    // Returns once the context data has loaded (ready) or failed to load (IsFailed()
    // and ReadyError set). A failed load does not throw.
    public async Task<IContext> CreateContextAsync(ContextConfig config)
    {
        return await CreatePendingContext(config).WaitUntilReadyAsync().ConfigureUnboundContinuation();
    }

    // Returns immediately with a context that is not ready yet; the data loads in
    // the background. Until it is ready, GetTreatment returns 0 without queuing an
    // exposure, variable reads return the default, key/experiment lists are empty,
    // and Track/SetUnit/SetAttribute/SetOverride work as usual (goals are queued and
    // published once ready). Use WaitUntilReady/WaitUntilReadyAsync to wait for it.
    public IContext CreatePendingContext(ContextConfig config)
    {
        if (config == null) throw new ArgumentNullException(nameof(config), "Context configuration is required");

        // Run the fetch off the caller's synchronization context, so a later blocking
        // WaitUntilReady() cannot deadlock on a continuation posted back to it.
        var dataTask = Task.Run(() => _contextDataProvider.GetContextDataAsync());

        return Context.CreatePending(config,
            dataTask,
            Clock.SystemUtc(),
            _contextDataProvider,
            _contextEventHandler,
            _contextEventLogger,
            _variableParser,
            new AudienceMatcher(_audienceDeserializer),
            _loggerFactory);
    }

    public IContext CreateContextWith(ContextConfig config, ContextData data)
    {
        var context = new Context(config,
            data,
            Clock.SystemUtc(),
            _contextDataProvider,
            _contextEventHandler,
            _contextEventLogger,
            _variableParser,
            new AudienceMatcher(_audienceDeserializer),
            _loggerFactory);

        return context;
    }
}
