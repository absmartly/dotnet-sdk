using System;
using System.Net.Http;

namespace ABSmartly.Services;

[Obsolete("ABSdkHttpClientFactory has been renamed to ABsmartlyHttpClientFactory. Please use ABsmartlyHttpClientFactory instead.")]
public class ABSdkHttpClientFactory : ABsmartlyHttpClientFactory
{
    public ABSdkHttpClientFactory(IHttpClientFactory httpClientFactory)
        : base(httpClientFactory)
    {
    }
}

[Obsolete("AbsmartlyHttpClientFactory has been renamed to ABsmartlyHttpClientFactory. Please use ABsmartlyHttpClientFactory instead.")]
public class AbsmartlyHttpClientFactory : ABsmartlyHttpClientFactory
{
    public AbsmartlyHttpClientFactory(IHttpClientFactory httpClientFactory)
        : base(httpClientFactory)
    {
    }
}
