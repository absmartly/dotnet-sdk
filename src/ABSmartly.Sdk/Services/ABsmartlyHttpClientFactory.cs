using System.Net.Http;
using System.Threading.Tasks;

namespace ABSmartly.Services;

public class ABsmartlyHttpClientFactory : IABsmartlyHttpClientFactory
{
    private readonly IHttpClientFactory _httpClientFactory;

    public ABsmartlyHttpClientFactory(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    public IABsmartlyHttpClient CreateClient()
    {
        return new HttpClientWrapper(_httpClientFactory.CreateClient(ABsmartly.HttpClientName));
    }

    public class HttpClientWrapper : IABsmartlyHttpClient
    {
        private readonly HttpClient _client;

        public HttpClientWrapper(HttpClient client)
        {
            _client = client;
        }

        public Task<HttpResponseMessage> GetAsync(string requestUri)
        {
            return _client.GetAsync(requestUri);
        }

        public Task<HttpResponseMessage> PutAsync(string requestUri, HttpContent content)
        {
            return _client.PutAsync(requestUri, content);
        }

        public void AddHeader(string name, string value)
        {
            _client.DefaultRequestHeaders.Add(name, value);
        }

        public void Dispose()
        {
            _client?.Dispose();
        }
    }
}
