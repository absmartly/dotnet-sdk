using ABSmartly.Temp;

namespace ABSmartly;

public class ClientConfig
{
    private string _endpoint;
    private string _apiKey;
    private string _environment;
    private string _application;
    private IContextDataDeserializer _deserializer;
    private IContextEventSerializer _serializer;
    // Todo: https://www.codeproject.com/Questions/1273200/Is-there-an-equivalent-of-javas-executorservice-cl
    //private Executor _executor;

    public ClientConfig(string endpoint = null, string apiKey = null, string environment = null,
        string application = null, IContextDataDeserializer deserializer = null,
        IContextEventSerializer serializer = null)
    {
        _endpoint = endpoint;
        _apiKey = apiKey;
        _environment = environment;
        _application = application;
        _deserializer = deserializer;
        _serializer = serializer;
    }

    public static ClientConfig Create()
    {
        return new ClientConfig();
    }

    public static ClientConfig Create(ClientConfiguration configuration)
    {
        return Create(configuration, string.Empty);
    }

    public static ClientConfig Create(ClientConfiguration configuration, string prefix)
    {
        return new ClientConfig(
            endpoint: configuration.Endpoint,
            environment: configuration.Environment,
            application: configuration.Application,
            apiKey: configuration.ApiKey
        );
    }
}