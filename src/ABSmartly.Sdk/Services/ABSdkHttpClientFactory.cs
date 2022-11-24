using System.Net.Http;
using System.Threading.Tasks;

namespace ABSmartly.Services;

public class ABSdkHttpClientFactory : IABSdkHttpClientFactory
{
    private readonly IHttpClientFactory _httpClientFactory;

    public ABSdkHttpClientFactory(IHttpClientFactory httpClientFactory) => _httpClientFactory = httpClientFactory;

    public IABSdkHttpClient CreateClient() => new HttpClientWrapper(_httpClientFactory.CreateClient(ABSdk.HttpClientName));

    public class HttpClientWrapper : IABSdkHttpClient
    {
        private readonly HttpClient _client;

        public HttpClientWrapper(HttpClient client) => _client = client;

        public Task<HttpResponseMessage> GetAsync(string requestUri) => _client.GetAsync(requestUri);

        public Task<HttpResponseMessage> PutAsync(string requestUri, HttpContent content) => _client.PutAsync(requestUri, content);

        public void AddHeader(string name, string value) => _client.DefaultRequestHeaders.Add(name, value);

        public void Dispose()
        {
            _client?.Dispose();
        }
    }
}