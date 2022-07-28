using ABSmartly.Temp;

namespace ABSmartly.DefaultServiceImplementations;

public class DefaultHTTPClientConfig
{
    private Provider _securityProvider;
    private long _connectTimeout;
    private long _connectionKeepAlive;
    private long _connectionRequestTimeout;
    private long _retryInterval;
    private int _maxRetries;

    public DefaultHTTPClientConfig()
    {
        _connectTimeout = 3000;
        _connectionKeepAlive = 30000;
        _connectionRequestTimeout = 1000;
        _retryInterval = 333;
        _maxRetries = 5;
    }

    public static DefaultHTTPClientConfig Create()
    {
        return new DefaultHTTPClientConfig();
    }

    public Provider GetSecurityProvider()
    {
        return _securityProvider;
    }

    public DefaultHTTPClientConfig SetSecurityProvider(Provider securityProvider)
    {
        _securityProvider = securityProvider;
        return this;
    }

    public long GetConnectTimeout()
    {
        return _connectTimeout;
    }

    public DefaultHTTPClientConfig SetConnectTimeout(long connectTimeoutMs)
    {
        _connectTimeout = connectTimeoutMs;
        return this;
    }

    public long GetConnectionKeepAlive()
    {
        return _connectionKeepAlive;
    }

    public DefaultHTTPClientConfig SetConnectionKeepAlive(long connectionKeepAliveMs)
    {
        _connectionKeepAlive = connectionKeepAliveMs;
        return this;
    }

    public long GetConnectionRequestTimeout()
    {
        return _connectionRequestTimeout;
    }

    public DefaultHTTPClientConfig SetConnectionRequestTimeout(long connectionRequestTimeoutMs)
    {
        _connectionRequestTimeout = connectionRequestTimeoutMs;
        return this;
    }

    public int GetMaxRetries()
    {
        return _maxRetries;
    }

    public DefaultHTTPClientConfig SetMaxRetries(int maxRetries)
    {
        _maxRetries = maxRetries;
        return this;
    }

    public long GetRetryInterval()
    {
        return _retryInterval;
    }

    public DefaultHTTPClientConfig SetRetryInterval(long retryIntervalMs)
    {
        _retryInterval = retryIntervalMs;
        return this;
    }
}