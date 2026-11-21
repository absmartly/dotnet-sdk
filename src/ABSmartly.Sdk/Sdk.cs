using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ABSmartly;
using ABSmartly.Concurrency;
using ABSmartly.Extensions;
using ABSmartly.Models;
using ABSmartly.Services;
using ABSmartly.Time;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ABsmartly;

/// <summary>
/// The SDK entry point: creates contexts.
/// </summary>
public class Sdk
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

    // Non-empty application/environment names from the service config. The collector also
    // attaches both to every exposure server-side, so audiences can target them locally
    // without the SDK publishing anything extra. User-set attributes still overlay these.
    private IReadOnlyDictionary<string, object> _systemAttributes;
    private IReadOnlyDictionary<string, object> _collectorAttributes;

    [ActivatorUtilitiesConstructor]
    public Sdk(IABsmartlyHttpClientFactory httpClientFactory,
        IOptions<ABSmartlyServiceConfiguration> serviceConfiguration,
        IOptions<ABsmartlyConfig> configOptions,
        ILoggerFactory loggerFactory)
    {
        Init(httpClientFactory, serviceConfiguration.Value, configOptions?.Value, loggerFactory);
    }

    public Sdk(IABsmartlyHttpClientFactory httpClientFactory,
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

        // One copy for the service's requests and for the values switchback cluster lookups read
        // (BuildCollectorAttributes), so the headers the Collector replays from always match what
        // served, whatever the caller later does with its configuration object.
        var credentials = serviceConfiguration;
        serviceConfiguration = Snapshot(serviceConfiguration);
        _client = new ABSmartlyService(
            serviceConfiguration,
            _httpClientFactory,
            _contextDataDeserializer,
            _contextEventSerializer,
            _loggerFactory) { ApiKeySource = () => credentials.ApiKey };

        _contextDataProvider = config?.ContextDataProvider ?? new DefaultContextDataProvider(_client);
        _contextEventHandler = config?.ContextPublisher ?? new DefaultContextPublisher(_client);
        _contextEventLogger = config?.ContextEventLogger;

        _variableParser = config?.VariableParser ?? new DefaultVariableParser(_loggerFactory);
        _audienceDeserializer = config?.AudienceDeserializer ?? new DefaultAudienceDeserializer(_loggerFactory);

        _systemAttributes = BuildSystemAttributes(serviceConfiguration);
        _collectorAttributes = BuildCollectorAttributes(serviceConfiguration);
    }

    // System attributes made available to every context's audience evaluation: the
    // application name and the environment name, exactly as configured, skipping any that
    // is null or empty.
    private static IReadOnlyDictionary<string, object> BuildSystemAttributes(
        ABSmartlyServiceConfiguration serviceConfiguration)
    {
        var systemAttributes = new Dictionary<string, object>(2);

        if (!string.IsNullOrEmpty(serviceConfiguration?.Application))
            systemAttributes["application"] = serviceConfiguration.Application;

        if (!string.IsNullOrEmpty(serviceConfiguration?.Environment))
            systemAttributes["environment"] = serviceConfiguration.Environment;

        return systemAttributes;
    }

    private static ABSmartlyServiceConfiguration Snapshot(ABSmartlyServiceConfiguration configuration) =>
        configuration == null
            ? null
            : new ABSmartlyServiceConfiguration
            {
                Environment = configuration.Environment,
                Application = configuration.Application,
                Endpoint = configuration.Endpoint,
                ApiKey = configuration.ApiKey,
                ApplicationVersion = configuration.ApplicationVersion
            };

    // The attributes the Collector adds to every published request that lacks them, from the
    // headers this SDK sends (ContextAPI.extractAttributes): application and environment unless
    // blank, and application_version when application is set and the version header is not 0.
    // Switchback cluster lookups read them, so serving matches the Collector's replay.
    internal static IReadOnlyDictionary<string, object> BuildCollectorAttributes(
        ABSmartlyServiceConfiguration serviceConfiguration)
    {
        var attributes = new Dictionary<string, object>(3);
        // Header values arrive without surrounding spaces and tabs (RFC 9110 field values).
        var application = serviceConfiguration?.Application?.Trim(' ', '\t');
        var environment = serviceConfiguration?.Environment?.Trim(' ', '\t');
        if (!IsJavaBlank(application))
        {
            attributes["application"] = application;
            var version = long.Parse(ABSmartlyService.ApplicationVersionHeader(serviceConfiguration.ApplicationVersion),
                System.Globalization.CultureInfo.InvariantCulture);
            if (version != 0) attributes["application_version"] = version;
        }

        if (!IsJavaBlank(environment))
            attributes["environment"] = environment;

        return attributes;
    }

    // Java String.isBlank(): empty or only Character.isWhitespace characters, that is a space, line
    // or paragraph separator other than a non-breaking space, or one of U+0009-U+000D, U+001C-U+001F.
    private static bool IsJavaBlank(string value)
    {
        if (value == null) return true;
        foreach (var c in value)
        {
            if (!IsJavaWhitespace(c)) return false;
        }

        return true;
    }

    private static bool IsJavaWhitespace(char c)
    {
        if (c >= '\u0009' && c <= '\u000d' || c >= '\u001c' && c <= '\u001f') return true;
        if (c == '\u00a0' || c == '\u2007' || c == '\u202f') return false;
        var category = char.GetUnicodeCategory(c);
        return category == System.Globalization.UnicodeCategory.SpaceSeparator ||
               category == System.Globalization.UnicodeCategory.LineSeparator ||
               category == System.Globalization.UnicodeCategory.ParagraphSeparator;
    }

    public IContext CreateContext(ContextConfig config)
    {
        return AsyncHelpers.RunSync(async () => await CreateContextAsync(config));
    }

    // Returns once the context data has loaded (ready) or failed to load (IsFailed()
    // and ReadyError set). A failed load does not throw.
    public async Task<IContext> CreateContextAsync(ContextConfig config)
    {
        var context = (Context)CreatePendingContext(config);
        await context.WaitUntilLoadSettledAsync().ConfigureUnboundContinuation();
        return context;
    }

    // Returns immediately with a context that is not ready yet; the data loads in
    // the background. Until it is ready, the read methods (GetTreatment,
    // PeekTreatment, GetVariableValue, PeekVariableValue, VariableKeys, Experiments
    // and the custom field methods) throw InvalidOperationException ("ABSmartly
    // Context is not yet ready") and queue no exposure; Track/SetUnit/SetAttribute/
    // SetOverride work as usual (goals are queued and published once ready). Use
    // WaitUntilReady/WaitUntilReadyAsync to wait for it.
    public IContextV2 CreatePendingContext(ContextConfig config)
    {
        if (config == null) throw new ArgumentNullException(nameof(config), "Context configuration is required");

        // Run the fetch off the caller's synchronization context, so a later blocking
        // WaitUntilReady() cannot deadlock on a continuation posted back to it.
        var dataTask = Task.Run(() => Context.FetchContextData(_contextDataProvider));

        return Context.CreatePending(config,
            dataTask,
            Clock.SystemUtc(),
            _contextDataProvider,
            _contextEventHandler,
            _contextEventLogger,
            _variableParser,
            new AudienceMatcher(_audienceDeserializer),
            _loggerFactory,
            _systemAttributes,
            _collectorAttributes);
    }

    public IContext CreateContextWith(ContextConfig config, ContextData data)
    {
        var context = Context.CreateWithData(config,
            data,
            Clock.SystemUtc(),
            _contextDataProvider,
            _contextEventHandler,
            _contextEventLogger,
            _variableParser,
            new AudienceMatcher(_audienceDeserializer),
            _loggerFactory,
            _systemAttributes,
            _collectorAttributes);

        return context;
    }
}
