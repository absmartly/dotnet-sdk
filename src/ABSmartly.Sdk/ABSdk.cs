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
    public ABSdk(
        IABSdkHttpClientFactory httpClientFactory,
        ILoggerFactory loggerFactory,
        IOptions<ABSdkConfig> configOptions,
        IOptions<ABSmartlyServiceConfiguration> serviceConfiguration)
    {
        Init(serviceConfiguration.Value, httpClientFactory, loggerFactory, configOptions?.Value);
    }

    public ABSdk(
        IABSdkHttpClientFactory httpClientFactory,
        ILoggerFactory loggerFactory,
        ABSdkConfig config,
        ABSmartlyServiceConfiguration serviceConfiguration)
    {
        Init(serviceConfiguration, httpClientFactory, loggerFactory, config);
    }
    
    private void Init(
        ABSmartlyServiceConfiguration serviceConfiguration,
        IABSdkHttpClientFactory httpClientFactory,
        ILoggerFactory loggerFactory = null,
        ABSdkConfig config = null)
    {
        _loggerFactory = loggerFactory ?? new LoggerFactory();
        _httpClientFactory = httpClientFactory;

        _contextDataDeserializer =
            config?.ContextDataDeserializer ?? new DefaultContextDataDeserializer(_loggerFactory);
        _contextEventSerializer = config?.ContextEventSerializer ?? new DefaultContextEventSerializer(_loggerFactory);

        _client = new ABSmartlyService(
            serviceConfiguration,
            _httpClientFactory,
            _loggerFactory,
            _contextDataDeserializer,
            _contextEventSerializer
        );

        _contextDataProvider = config?.ContextDataProvider ?? new DefaultContextDataProvider(_client);
        _contextEventHandler = config?.ContextEventHandler ?? new DefaultContextEventHandler(_client);
        _contextEventLogger = config?.ContextEventLogger;

        _variableParser = config?.VariableParser ?? new DefaultVariableParser(_loggerFactory);
        _audienceDeserializer = config?.AudienceDeserializer ?? new DefaultAudienceDeserializer(_loggerFactory);
    }

    public Context CreateContext(ContextConfig config = null)
    {
        return AsyncHelpers.RunSync(async () => await CreateContextAsync(config));
    }

    public async Task<Context> CreateContextAsync(ContextConfig config = null)
    {
        var context = new Context(
            _loggerFactory,
            await _contextDataProvider.GetContextDataAsync().ConfigureUnboundContinuation(),
            config ?? new ContextConfig(),
            Clock.SystemUtc(),
            _contextDataProvider,
            _contextEventHandler,
            _contextEventLogger,
            _variableParser,
            new AudienceMatcher(_audienceDeserializer)
        );

        return context;
    }

    public Context CreateContextWith(ContextData data, ContextConfig config = null)
    {
        var context = new Context(
            _loggerFactory,
            data,
            config ?? new ContextConfig(),
            Clock.SystemUtc(),
            _contextDataProvider,
            _contextEventHandler,
            _contextEventLogger,
            _variableParser,
            new AudienceMatcher(_audienceDeserializer)
        );

        return context;
    }
}