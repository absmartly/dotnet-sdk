using System.Net.Http;

namespace ABSmartly.Services;

public class ABSdkHttpClientFactory : IABSdkHttpClientFactory
{
    private readonly IHttpClientFactory _httpClientFactory;

    public ABSdkHttpClientFactory(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    public HttpClient CreateClient()
    {
        return _httpClientFactory.CreateClient(ABSdk.HttpClientName);
    }
}