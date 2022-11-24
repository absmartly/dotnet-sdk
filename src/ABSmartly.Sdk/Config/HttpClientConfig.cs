namespace ABSmartly;

public class HttpClientConfig
{
    // ReSharper disable once MemberCanBePrivate.Global

    public long ConnectTimeout { get; set; }
    public long ConnectionKeepAlive { get; set; }
    public long RetryInterval { get; set; }
    public int MaxRetries { get; set; }

    public static HttpClientConfig CreateDefault()
    {
        return new HttpClientConfig
        {
            ConnectTimeout = 3000,
            ConnectionKeepAlive = 30000,
            RetryInterval = 333,
            MaxRetries = 5
        };
    }
}