using System;
using System.Collections.Generic;
using ABSmartly.Temp;

namespace ABSmartly;

public class Client
{
    private string _url;
    private Dictionary<string, string> _query;
    private Dictionary<string, string> _headers;
    private IHTTPClient _httpClient;
    private IExecutor _executor;
    private IContextDataDeserializer _deserializer;
    private IContextEventSerializer _serializer;



    public Client(ClientConfig config, IHTTPClient httpClient)
    {
        var endpoint = config.Endpoint();
        var apiKey = config.ApiKey();
        var application = config.Application();
        var environment = config.Environment();

        if (string.IsNullOrWhiteSpace(endpoint))
            throw new ArgumentNullException(nameof(endpoint), "Missing Endpoint configuration");

        if (string.IsNullOrWhiteSpace(apiKey))
            throw new ArgumentNullException(nameof(apiKey), "Missing APIKey configuration");

        if (string.IsNullOrWhiteSpace(application))
            throw new ArgumentNullException(nameof(application), "Missing Application configuration");

        if (string.IsNullOrWhiteSpace(environment))
            throw new ArgumentNullException(nameof(environment), "Missing Environment configuration");

        _url = endpoint + "/context";
        _httpClient = httpClient;
        _deserializer = 
    }
}