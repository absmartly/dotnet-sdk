using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using ABSmartly.Models;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.WebUtilities;

namespace ABSmartly;

public class ABSmartlyService : IABSmartlyServiceClient
{
    private readonly string _url;
    private readonly ABSmartlyServiceConfiguration _config;
    private readonly IABSdkHttpClientFactory _httpClientFactory;

    private readonly IContextDataDeserializer _dataDeserializer;
    private readonly IContextEventSerializer _eventSerializer;
    private readonly ILogger<ABSmartlyService> _logger;

    public ABSmartlyService(ABSmartlyServiceConfiguration config,
        IABSdkHttpClientFactory httpClientFactory,
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

        _dataDeserializer = dataDeserializer;
        _eventSerializer = eventSerializer;

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
            return _dataDeserializer.Deserialize(responseStream);
        }
        catch (Exception e)
        {
            _logger?.LogError("Fetch context data: {EMessage}", e.Message);
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
            
            var content = new ByteArrayContent(serializedEvent);
            content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
            
            var result = await httpClient.PutAsync(_url, content);

            if (!result.IsSuccessStatusCode)
            {
                _logger?.LogError(
                    "Publish event unsuccessful request: reason '{ReasonPhrase}', response content = '{S}'",
                    result.ReasonPhrase, await result.Content.ReadAsStringAsync());
                return false;
            }

            return true;
        }
        catch (Exception e)
        {
            _logger?.LogError("Exception when Publish event: {E}", e);
            return false;
        }
    }

    private void SetupDefaultHeaders(IABSdkHttpClient client)
    {
        client.AddHeader("X-API-Key", _config.ApiKey);
        client.AddHeader("X-Application", _config.Application);
        client.AddHeader("X-Environment", _config.Environment);
        client.AddHeader("X-Application-Version", "0");
        client.AddHeader("X-Agent", "absmartly-dotnet-sdk");
    }

    private Dictionary<string, string> GetDefaultQueryParameters()
    {
        return new()
        {
            ["application"] = _config.Application,
            ["environment"] = _config.Environment
        };
    }
}