namespace ABSmartly;

public class HttpClientConfig
{
    // ReSharper disable once MemberCanBePrivate.Global
    public HttpClientConfig()
    {
    }

    public static HttpClientConfig CreateDefault()
    {
        return new()
        {
            ConnectTimeout = 3000,
            ConnectionKeepAlive = 30000,
            RetryInterval = 333,
            MaxRetries = 5
        };
    }

    public long ConnectTimeout { get; set; }
    public long ConnectionKeepAlive { get; set; }
    public long RetryInterval { get; set; }
    public int MaxRetries { get; set; }
}