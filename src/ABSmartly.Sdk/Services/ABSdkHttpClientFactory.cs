using System;
using System.Net.Http;

namespace ABSmartly.Services;

/// <summary>
/// Obsolete: Use <see cref="ABsmartlyHttpClientFactory"/> instead.
/// </summary>
/// <remarks>
/// Keeps its 1.3.0 public surface: <see cref="CreateClient"/> returns <see cref="IABSdkHttpClient"/>
/// and the nested <see cref="HttpClientWrapper"/> type still exists. It also implements
/// <see cref="IABsmartlyHttpClientFactory"/> (through its base class).
/// </remarks>
[Obsolete("ABSdkHttpClientFactory has been renamed to ABsmartlyHttpClientFactory. Please use ABsmartlyHttpClientFactory instead.")]
#pragma warning disable CS0618
public class ABSdkHttpClientFactory : ABsmartlyHttpClientFactory, IABSdkHttpClientFactory, IABsmartlyHttpClientFactory
#pragma warning restore CS0618
{
    private readonly IHttpClientFactory _httpClientFactory;

    public ABSdkHttpClientFactory(IHttpClientFactory httpClientFactory)
        : base(httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    public new IABSdkHttpClient CreateClient()
    {
        return new HttpClientWrapper(_httpClientFactory.CreateClient(ABsmartly.HttpClientName));
    }

    // Dispatches through IABSdkHttpClientFactory, so a 1.3.0 subclass that re-implements
    // IABSdkHttpClientFactory.CreateClient (for a custom transport) is used wherever the SDK
    // now asks for an IABsmartlyHttpClientFactory.
    IABsmartlyHttpClient IABsmartlyHttpClientFactory.CreateClient()
    {
        return LegacyHttpClientAdapter.Wrap(((IABSdkHttpClientFactory)this).CreateClient());
    }

    /// <summary>
    /// 1.3.0 wrapper type, kept for compatibility. Same as
    /// <see cref="ABsmartlyHttpClientFactory.HttpClientWrapper"/>.
    /// </summary>
    public new class HttpClientWrapper : ABsmartlyHttpClientFactory.HttpClientWrapper
    {
        public HttpClientWrapper(HttpClient client) : base(client)
        {
        }
    }
}
