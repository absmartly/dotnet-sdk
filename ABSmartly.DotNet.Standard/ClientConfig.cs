using ABSmartly.Temp;

namespace ABSmartly;

public class ClientConfig
{
    // Todo: https://www.codeproject.com/Questions/1273200/Is-there-an-equivalent-of-javas-executorservice-cl

    public ClientConfig(string endpoint = null, string apiKey = null, string environment = null, string application = null, IContextDataDeserializer dataDeserializer = null, IContextEventSerializer serializer = null, IExecutor executor = null)
    {
        Endpoint = endpoint;
        ApiKey = apiKey;
        Environment = environment;
        Application = application;
        DataDeserializer = dataDeserializer;
        Serializer = serializer;
        Executor = executor;
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




    public string Endpoint { get; }

    public string ApiKey { get; }

    public string Application { get; }

    public string Environment { get; }

    public IContextDataDeserializer DataDeserializer { get; }

    public IContextEventSerializer Serializer { get; }

    public IExecutor Executor { get; }
}