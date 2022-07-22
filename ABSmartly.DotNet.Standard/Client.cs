using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using ABSmartly.Json;
using ABSmartly.Temp;
using Microsoft.Extensions.Logging;

namespace ABSmartly;

public class Client : IDisposable
{
    private string _url;
    //private Dictionary<string, string> _query;
    //private Dictionary<string, string> _headers;
    private IHttpClientFactory _httpClientFactory;
    private IExecutor _executor;
    private IContextDataDeserializer _deserializer;
    private IContextEventSerializer _serializer;
    private ClientConfig _config;

    public Client(ClientConfig config, IHttpClientFactory httpClientFactory, ILoggerFactory loggerFactory)
    {
        if (config is null)
            throw new ArgumentNullException(nameof(config), "Config is null..");

        if (string.IsNullOrWhiteSpace(_config.Endpoint))
            throw new ArgumentNullException(nameof(_config.Endpoint), "Missing Endpoint configuration");

        if (string.IsNullOrWhiteSpace(_config.ApiKey))
            throw new ArgumentNullException(nameof(_config.ApiKey), "Missing APIKey configuration");

        if (string.IsNullOrWhiteSpace(_config.Application))
            throw new ArgumentNullException(nameof(_config.Application), "Missing Application configuration");

        if (string.IsNullOrWhiteSpace(_config.Environment))
            throw new ArgumentNullException(nameof(_config.Environment), "Missing Environment configuration");

        _deserializer = config.DataDeserializer;
        _deserializer ??= new DefaultContextDataDeserializer();

        _serializer = config.Serializer;
        _serializer ??= new DefaultContextEventSerializer(loggerFactory);
        
        _executor = config.Executor;


        _url = config.Endpoint + "/context";
        _httpClientFactory = httpClientFactory;
    }

    public async Task<ContextData> GetContextData()
    {
        try
        {
            var httpClient = _httpClientFactory.CreateClient();
            httpClient.DefaultRequestHeaders.Add("X-API-Key", _config.ApiKey);
            httpClient.DefaultRequestHeaders.Add("X-Application", _config.Application );
            httpClient.DefaultRequestHeaders.Add("X-Environment", _config.Environment);
            httpClient.DefaultRequestHeaders.Add("X-Application-Version", "0");
            httpClient.DefaultRequestHeaders.Add("X-Agent", "absmartly-dotnet-sdk");
        
            // Todo: add query?

            var contextData = await httpClient.GetFromJsonAsync<ContextData>(_url);
            return contextData;
        }
        catch (Exception e)
        {
            return null;
        }
    }

    public async Task<bool> Publish(PublishEvent publishEvent)
    {
        try
        {
            var httpClient = _httpClientFactory.CreateClient();
            httpClient.DefaultRequestHeaders.Add("X-API-Key", _config.ApiKey);
            httpClient.DefaultRequestHeaders.Add("X-Application", _config.Application );
            httpClient.DefaultRequestHeaders.Add("X-Environment", _config.Environment);
            httpClient.DefaultRequestHeaders.Add("X-Application-Version", "0");
            httpClient.DefaultRequestHeaders.Add("X-Agent", "absmartly-dotnet-sdk");

            var result = await httpClient.PostAsJsonAsync(_url, publishEvent);
            result.EnsureSuccessStatusCode();

            return true;
        }
        catch (Exception e)
        {
            return false;
        }
    }







    #region IDisposable

    public void Dispose()
    {
        _httpClient?.Dispose();
    }

    #endregion
}