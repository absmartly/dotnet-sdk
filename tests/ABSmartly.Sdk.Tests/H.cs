namespace ABSmartly.Sdk.Tests;

public static class H
{
    public static ABsmartlyServiceConfiguration ServiceConfig(string application, string endpoint, string environment,
        string apiKey)
    {
        return new()
        {
            Application = application,
            Endpoint = endpoint,
            Environment = environment,
            ApiKey = apiKey
        };
    }
}