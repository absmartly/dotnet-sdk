using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
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

        if (!_config.Endpoint.StartsWith("https://", StringComparison.OrdinalIgnoreCase) &&
            !IsLocalEndpoint(_config.Endpoint))
        {
            throw new ArgumentException("Endpoint must use HTTPS to protect API key (localhost and private hosts are exempt for testing)", nameof(_config.Endpoint));
        }

        _url = config.Endpoint + "/context";
        _logger = loggerFactory?.CreateLogger<ABSmartlyService>();
    }

    public async Task<ContextData> GetContextDataAsync()
    {
        try
        {
            using var httpClient = _httpClientFactory.CreateClient();
            var uri = QueryHelpers.AddQueryString(_url, GetDefaultQueryParameters());
            var response = await httpClient.GetAsync(uri);
            response.EnsureSuccessStatusCode();

            var responseStream = await response.Content.ReadAsStreamAsync();
            var result = _dataDeserializer.Deserialize(responseStream);

            if (result == null)
            {
                var message = "Context data deserializer returned null - check logs for deserialization errors";
                _logger?.LogError(message);
                Console.Error.WriteLine($"[ABSmartly] ERROR: {message}");
            }

            return result;
        }
        catch (Exception e)
        {
            var message = $"Error fetching context data: {e.Message}";
            _logger?.LogError(e, message);
            Console.Error.WriteLine($"[ABSmartly] ERROR: {message}");
            return null;
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
                Console.Error.WriteLine($"[ABSmartly] ERROR: {message}");
                return false;
            }

            var content = new ByteArrayContent(serializedEvent);
            content.Headers.ContentType = new MediaTypeHeaderValue("application/json");

            var result = await httpClient.PutAsync(_url, content);

            if (!result.IsSuccessStatusCode)
            {
                var responseContent = await result.Content.ReadAsStringAsync();
                var message = $"Publish event failed: HTTP {(int)result.StatusCode} {result.ReasonPhrase}";
                _logger?.LogError("{Message}, response: {ResponseContent}", message, responseContent);
                Console.Error.WriteLine($"[ABSmartly] ERROR: {message}");
                return false;
            }

            return true;
        }
        catch (Exception e)
        {
            var message = $"Error publishing event: {e.Message}";
            _logger?.LogError(e, message);
            Console.Error.WriteLine($"[ABSmartly] ERROR: {message}");
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

    private static bool IsLocalEndpoint(string endpoint)
    {
        if (Uri.TryCreate(endpoint, UriKind.Absolute, out var uri))
        {
            var host = uri.Host;
            if (string.Equals(host, "localhost", StringComparison.OrdinalIgnoreCase))
                return true;
            if (host.StartsWith("127.", StringComparison.Ordinal))
                return true;
            if (string.Equals(host, "::1", StringComparison.Ordinal))
                return true;
            if (host.IndexOf('.') < 0)
                return true;
        }
        else
        {
            if (endpoint.IndexOf("localhost", StringComparison.OrdinalIgnoreCase) >= 0)
                return true;
            if (endpoint.IndexOf("127.0.0.1", StringComparison.Ordinal) >= 0)
                return true;
        }
        return false;
    }
}