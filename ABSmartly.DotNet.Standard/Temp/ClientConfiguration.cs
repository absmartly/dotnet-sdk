namespace ABSmartlySdk.Temp;

// Todo: Move to appsettings.json!!
public class ClientConfiguration
{
    public ClientConfiguration()
    {

    }

    public ClientConfiguration(string prefix, string environment, string application, string endpoint, string apiKey)
    {
        Prefix = prefix;
        Environment = environment;
        Application = application;
        Endpoint = endpoint;
        ApiKey = apiKey;
    }

    public string Prefix { get; set; }
    public string Environment { get; set; }
    public string Application { get; set; }
    public string Endpoint { get; set; }
    public string ApiKey { get; set; }
}