using ABSmartly.DefaultServiceImplementations;
using ABSmartly.Interfaces;
using ABSmartly.Temp;

namespace ABSmartly;

public class ClientConfig
{
    // Todo: https://www.codeproject.com/Questions/1273200/Is-there-an-equivalent-of-javas-executorservice-cl

    public ClientConfig(
        string prefix = null,
        string endpoint = null, 
        string apiKey = null, 
        string environment = null,
        string application = null, 
        IContextDataDeserializer contextDataDeserializer = null,
        IContextEventSerializer contextEventSerializer = null, 
        IExecutor executor = null)
    {
        Endpoint = endpoint ?? string.Empty;
        ApiKey = apiKey ?? string.Empty;
        Environment = environment ?? string.Empty ;
        Application = application ?? string.Empty;
        DataDeserializer = contextDataDeserializer ?? new DefaultContextDataDeserializer(null);
        EventSerializer = contextEventSerializer ?? new DefaultContextEventSerializer(null);
        Executor = executor ?? new DefaultExecutor();

        if (!string.IsNullOrWhiteSpace(prefix))
        {
            if (!string.IsNullOrWhiteSpace(Endpoint))
                Endpoint = prefix + Endpoint;

            if (!string.IsNullOrWhiteSpace(ApiKey))
                ApiKey = prefix + ApiKey;
            
            if (!string.IsNullOrWhiteSpace(Environment))
                Environment = prefix + Environment;

            if (!string.IsNullOrWhiteSpace(Application))
                Application = prefix + Application;
        }
    }

    //public static ClientConfig Create(
    //    string endpoint = null, 
    //    string apiKey = null, 
    //    string environment = null,
    //    string application = null, 
    //    IContextDataDeserializer dataDeserializer = null,
    //    IContextEventSerializer serializer = null, 
    //    IExecutor executor = null)
    //{
    //    return new ClientConfig(endpoint, apiKey, environment, application, dataDeserializer, serializer, executor);
    //}

    //public static ClientConfig Create(ClientConfiguration configuration)
    //{
    //    return Create(configuration, string.Empty);
    //}

    //public static ClientConfig Create(ClientConfiguration configuration, string prefix)
    //{
    //    return new ClientConfig(
    //        endpoint: prefix + configuration.Endpoint,
    //        environment: prefix + configuration.Environment,
    //        application: prefix + configuration.Application,
    //        apiKey: prefix + configuration.ApiKey
    //    );
    //}

    public string Endpoint { get; }

    public string ApiKey { get; }

    public string Environment { get; }

    public string Application { get; }

    public IContextDataDeserializer DataDeserializer { get; }

    public IContextEventSerializer EventSerializer { get; }

    public IExecutor Executor { get; }
}