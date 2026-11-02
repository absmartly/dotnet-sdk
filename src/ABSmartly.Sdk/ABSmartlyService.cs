using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using ABSmartly.Extensions;
using ABSmartly.Models;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Logging;

namespace ABSmartly;

public class ABSmartlyService : IABSmartlyServiceClient
{
    private readonly ABSmartlyServiceConfiguration _config;

    private readonly IContextDataDeserializer _dataDeserializer;
    private readonly IContextEventSerializer _eventSerializer;
    private readonly IABsmartlyHttpClientFactory _httpClientFactory;
    private readonly ILogger<ABSmartlyService> _logger;
    private readonly string _url;

    /// <summary>
    /// 1.3.0 constructor, kept for source and binary compatibility. The legacy factory is
    /// wrapped in an adapter.
    /// </summary>
    [Obsolete("Use the constructor taking an IABsmartlyHttpClientFactory instead.")]
    public ABSmartlyService(ABSmartlyServiceConfiguration config,
        IABSdkHttpClientFactory httpClientFactory,
        IContextDataDeserializer dataDeserializer,
        IContextEventSerializer eventSerializer,
        ILoggerFactory loggerFactory = null)
        : this(config, Services.LegacyHttpClientFactoryAdapter.Wrap(httpClientFactory), dataDeserializer,
            eventSerializer, loggerFactory)
    {
    }

    /// <summary>
    /// Kept so that passing an <see cref="Services.ABSdkHttpClientFactory"/> (which implements both
    /// factory interfaces) or a <c>null</c> factory binds without ambiguity, as it did in 1.3.0.
    /// </summary>
    [Obsolete("Use the constructor taking an IABsmartlyHttpClientFactory instead.")]
    public ABSmartlyService(ABSmartlyServiceConfiguration config,
        Services.ABSdkHttpClientFactory httpClientFactory,
        IContextDataDeserializer dataDeserializer,
        IContextEventSerializer eventSerializer,
        ILoggerFactory loggerFactory = null)
        : this(config, (IABsmartlyHttpClientFactory)httpClientFactory, dataDeserializer, eventSerializer,
            loggerFactory)
    {
    }

    public ABSmartlyService(ABSmartlyServiceConfiguration config,
        IABsmartlyHttpClientFactory httpClientFactory,
        IContextDataDeserializer dataDeserializer,
        IContextEventSerializer eventSerializer,
        ILoggerFactory loggerFactory = null)
    {
        _config = config ??
                  throw new ArgumentNullException(nameof(config), $"{nameof(ABSmartlyService)} config is required");

        _httpClientFactory = httpClientFactory ??
                             throw new ArgumentNullException(nameof(httpClientFactory),
                                 "HTTP client factory is required");

        _dataDeserializer = dataDeserializer ??
                            throw new ArgumentNullException(nameof(dataDeserializer), "Data deserializer is required");
        _eventSerializer = eventSerializer ??
                           throw new ArgumentNullException(nameof(eventSerializer), "Event serializer is required");

        if (string.IsNullOrWhiteSpace(_config.Endpoint))
            throw new ArgumentNullException(nameof(_config.Endpoint), "Missing Endpoint configuration");

        if (string.IsNullOrWhiteSpace(_config.ApiKey))
            throw new ArgumentNullException(nameof(_config.ApiKey), "Missing APIKey configuration");

        if (string.IsNullOrWhiteSpace(_config.Application))
            throw new ArgumentNullException(nameof(_config.Application), "Missing Application configuration");

        if (string.IsNullOrWhiteSpace(_config.Environment))
            throw new ArgumentNullException(nameof(_config.Environment), "Missing Environment configuration");

        _logger = loggerFactory?.CreateLogger<ABSmartlyService>();

        // As the Java SDK: plain http:// is allowed (in-cluster collectors, sidecars,
        // local proxies) but warned about; any other scheme is a configuration error.
        if (!_config.Endpoint.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            if (_config.Endpoint.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
                _logger?.LogWarning(
                    "ABSmartly SDK endpoint is not using HTTPS. API keys will be transmitted in plaintext: {Endpoint}",
                    _config.Endpoint);
            else
                throw new ArgumentException("Endpoint must use http:// or https:// protocol: " + _config.Endpoint,
                    nameof(_config.Endpoint));
        }

        _url = config.Endpoint.TrimEnd('/') + "/context";
    }

    // Throws when the context data cannot be fetched or deserialized, so callers see
    // the real cause: a context created from it fails with that error as ReadyError,
    // and Context.Refresh rethrows it.
    public async Task<ContextData> GetContextDataAsync()
    {
        try
        {
            using var httpClient = _httpClientFactory.CreateClient();
            var uri = QueryHelpers.AddQueryString(_url, GetDefaultQueryParameters());
            using var response = await httpClient.GetAsync(uri).ConfigureUnboundContinuation();
            response.EnsureSuccessStatusCode();

            var responseStream = await response.Content.ReadAsStreamAsync().ConfigureUnboundContinuation();
            var result = _dataDeserializer.Deserialize(responseStream);

            if (result == null)
                throw new InvalidOperationException(
                    "Context data deserializer returned null - check logs for deserialization errors");

            return result;
        }
        catch (Exception e)
        {
            _logger?.LogError(e, "Error fetching context data: {Message}", e.Message);
            throw;
        }
    }

    public async Task<bool> PublishAsync(PublishEvent publishEvent)
    {
        try
        {
            using var httpClient = _httpClientFactory.CreateClient();
            SetupDefaultHeaders(httpClient);

            var serializedEvent = _eventSerializer.Serialize(publishEvent);
            if (serializedEvent == null)
            {
                var message = "Event serializer returned null";
                _logger?.LogError(message);
                return false;
            }

            var content = new ByteArrayContent(serializedEvent);
            content.Headers.ContentType = new MediaTypeHeaderValue("application/json");

            using var result = await httpClient.PutAsync(_url, content).ConfigureUnboundContinuation();

            if (!result.IsSuccessStatusCode)
            {
                _logger?.LogError("Publish event failed: HTTP {StatusCode} {ReasonPhrase}", (int)result.StatusCode, result.ReasonPhrase);
                return false;
            }

            return true;
        }
        catch (Exception e)
        {
            _logger?.LogError(e, "Error publishing event: {Message}", e.Message);
            return false;
        }
    }

    private void SetupDefaultHeaders(IABsmartlyHttpClient client)
    {
        client.AddHeader("X-API-Key", _config.ApiKey);
        client.AddHeader("X-Application", _config.Application);
        client.AddHeader("X-Environment", _config.Environment);
        client.AddHeader("X-Application-Version", "0");
        client.AddHeader("X-Agent", "absmartly-dotnet-sdk");
    }

    private Dictionary<string, string> GetDefaultQueryParameters()
    {
        return new Dictionary<string, string>
        {
            ["application"] = _config.Application,
            ["environment"] = _config.Environment
        };
    }
}