using ABSmartlySdk.Temp;

namespace ABSmartlySdk.DefaultServiceImplementations;

internal class DefaultHttpClientConfig
{
    #region Lifecycle

    public DefaultHttpClientConfig()
    {
        // Default values
        ConnectTimeout = 3000;
        ConnectionKeepAlive = 30000;
        ConnectionRequestTimeout = 1000;
        RetryInterval = 333;
        MaxRetries = 5;
    }

    public static DefaultHttpClientConfig Create()
    {
        return new DefaultHttpClientConfig();
    }    

    #endregion

    internal Provider SecurityProvider { get; set; }
    internal long ConnectTimeout { get; set; }
    internal long ConnectionKeepAlive { get; set; }
    internal long ConnectionRequestTimeout { get; set; }
    internal long RetryInterval { get; set; }
    internal int MaxRetries { get; set; }


    //public Provider GetSecurityProvider()
    //{
    //    return _securityProvider;
    //}

    //public DefaultHttpClientConfig SetSecurityProvider(Provider securityProvider)
    //{
    //    _securityProvider = securityProvider;
    //    return this;
    //}

    //public long GetConnectTimeout()
    //{
    //    return _connectTimeout;
    //}

    //public DefaultHttpClientConfig SetConnectTimeout(long connectTimeoutMs)
    //{
    //    _connectTimeout = connectTimeoutMs;
    //    return this;
    //}

    //public long GetConnectionKeepAlive()
    //{
    //    return _connectionKeepAlive;
    //}

    //public DefaultHttpClientConfig SetConnectionKeepAlive(long connectionKeepAliveMs)
    //{
    //    _connectionKeepAlive = connectionKeepAliveMs;
    //    return this;
    //}

    //public long GetConnectionRequestTimeout()
    //{
    //    return _connectionRequestTimeout;
    //}

    //public DefaultHttpClientConfig SetConnectionRequestTimeout(long connectionRequestTimeoutMs)
    //{
    //    _connectionRequestTimeout = connectionRequestTimeoutMs;
    //    return this;
    //}

    //public int GetMaxRetries()
    //{
    //    return _maxRetries;
    //}

    //public DefaultHttpClientConfig SetMaxRetries(int maxRetries)
    //{
    //    _maxRetries = maxRetries;
    //    return this;
    //}

    //public long GetRetryInterval()
    //{
    //    return _retryInterval;
    //}

    //public DefaultHttpClientConfig SetRetryInterval(long retryIntervalMs)
    //{
    //    _retryInterval = retryIntervalMs;
    //    return this;
    //}
}