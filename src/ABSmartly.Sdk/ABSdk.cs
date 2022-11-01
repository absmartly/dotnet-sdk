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

    public async Task<Context> CreateContextAsync(ContextConfig config)
    {
        var context = new Context(config ?? new ContextConfig(),
            await _contextDataProvider.GetContextDataAsync().ConfigureUnboundContinuation(),
            Clock.SystemUtc(),
            _contextDataProvider,
            _contextEventHandler,
            _contextEventLogger,
            _variableParser, new AudienceMatcher(_audienceDeserializer), _loggerFactory);

        return context;
    }

    public Context CreateContextWith(ContextConfig config, ContextData data)
    {
        var context = new Context(config,
            data,
            Clock.SystemUtc(),
            _contextDataProvider,
            _contextEventHandler,
            _contextEventLogger,
            _variableParser, new AudienceMatcher(_audienceDeserializer), _loggerFactory);

        return context;
    }
}