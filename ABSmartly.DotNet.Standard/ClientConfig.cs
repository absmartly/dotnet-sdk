using ABSmartlySdk.DefaultServiceImplementations;
using ABSmartlySdk.Interfaces;
using ABSmartlySdk.Temp;

namespace ABSmartlySdk;

public class ClientConfig
{
    // Todo: https://www.codeproject.com/Questions/1273200/Is-there-an-equivalent-of-javas-executorservice-cl

    public ClientConfig(
        ClientConfiguration configuration,
        IContextDataDeserializer contextDataDeserializer = null,
        IContextEventSerializer contextEventSerializer = null, 
        IExecutor executor = null)
    {
        Endpoint = configuration.Endpoint ?? string.Empty;
        ApiKey = configuration.ApiKey ?? string.Empty;
        Environment = configuration.Environment ?? string.Empty ;
        Application = configuration.Application ?? string.Empty;
        DataDeserializer = contextDataDeserializer ?? new DefaultContextDataDeserializer(null);
        EventSerializer = contextEventSerializer ?? new DefaultContextEventSerializer(null);
        Executor = executor ?? new DefaultExecutor();

        if (!string.IsNullOrWhiteSpace(configuration.Prefix))
        {
            if (!string.IsNullOrWhiteSpace(Endpoint))
                Endpoint = configuration.Prefix + Endpoint;

            if (!string.IsNullOrWhiteSpace(ApiKey))
                ApiKey = configuration.Prefix + ApiKey;
            
            if (!string.IsNullOrWhiteSpace(Environment))
                Environment = configuration.Prefix + Environment;

            if (!string.IsNullOrWhiteSpace(Application))
                Application = configuration.Prefix + Application;
        }
    }

    public string Endpoint { get; }

    public string ApiKey { get; }

    public string Environment { get; }

    public string Application { get; }

    public IContextDataDeserializer DataDeserializer { get; }

    public IContextEventSerializer EventSerializer { get; }

    public IExecutor Executor { get; }
}