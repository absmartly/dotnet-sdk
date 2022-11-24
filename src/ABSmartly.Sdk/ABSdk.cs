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

public class ABSdk
{
    public const string HttpClientName = "ABSmartlySDK.HttpClient";

    private ILoggerFactory _loggerFactory;
    private IABSdkHttpClientFactory _httpClientFactory;

    private IABSmartlyServiceClient _client;
    private IContextDataProvider _contextDataProvider;
    private IContextEventHandler _contextEventHandler;
    private IContextEventLogger _contextEventLogger;
    private IVariableParser _variableParser;

    private IContextDataDeserializer _contextDataDeserializer;
    private IContextEventSerializer _contextEventSerializer;

    private IAudienceDeserializer _audienceDeserializer;

    [ActivatorUtilitiesConstructor]
    public ABSdk(IABSdkHttpClientFactory httpClientFactory,
        IOptions<ABSmartlyServiceConfiguration> serviceConfiguration,
        IOptions<ABSdkConfig> configOptions,
        ILoggerFactory loggerFactory)
    {
        Init(httpClientFactory, serviceConfiguration.Value, configOptions?.Value, loggerFactory);
    }

    public ABSdk(IABSdkHttpClientFactory httpClientFactory,
        ABSmartlyServiceConfiguration serviceConfiguration,
        ABSdkConfig config = null,
        ILoggerFactory loggerFactory = null)
    {
        Init(httpClientFactory, serviceConfiguration, config, loggerFactory);
    }
    
    private void Init(IABSdkHttpClientFactory httpClientFactory,
        ABSmartlyServiceConfiguration serviceConfiguration,
        ABSdkConfig config = null,
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
        _contextEventHandler = config?.ContextEventHandler ?? new DefaultContextEventHandler(_client);
        _contextEventLogger = config?.ContextEventLogger;

        _variableParser = config?.VariableParser ?? new DefaultVariableParser(_loggerFactory);
        _audienceDeserializer = config?.AudienceDeserializer ?? new DefaultAudienceDeserializer(_loggerFactory);
    }

    public IContext CreateContext(ContextConfig config)
    {
        return AsyncHelpers.RunSync(async () => await CreateContextAsync(config));
    }

    public async Task<IContext> CreateContextAsync(ContextConfig config)
    {
        var context = new Context(config,
            await _contextDataProvider.GetContextDataAsync().ConfigureUnboundContinuation(),
            Clock.SystemUtc(),
            _contextDataProvider,
            _contextEventHandler,
            _contextEventLogger,
            _variableParser, 
            new AudienceMatcher(_audienceDeserializer),
            _loggerFactory);

        return context;
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