using System;
using System.Net.Http;

namespace ABSmartly.Services;

[Obsolete("ABSdkHttpClientFactory has been renamed to ABsmartlyHttpClientFactory. Please use ABsmartlyHttpClientFactory instead.")]
#pragma warning disable CS0618
public class ABSdkHttpClientFactory : ABsmartlyHttpClientFactory, IABSdkHttpClientFactory
#pragma warning restore CS0618
{
    public ABSdkHttpClientFactory(IHttpClientFactory httpClientFactory)
        : base(httpClientFactory)
    {
    }

#pragma warning disable CS0618
    IABSdkHttpClient IABSdkHttpClientFactory.CreateClient()
    {
        return (IABSdkHttpClient)base.CreateClient();
    }
#pragma warning restore CS0618
}

[Obsolete("AbsmartlyHttpClientFactory has been renamed to ABsmartlyHttpClientFactory. Please use ABsmartlyHttpClientFactory instead.")]
public class AbsmartlyHttpClientFactory : ABsmartlyHttpClientFactory
{
    public AbsmartlyHttpClientFactory(IHttpClientFactory httpClientFactory)
        : base(httpClientFactory)
    {
    }
}
