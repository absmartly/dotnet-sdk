#nullable enable
using System;
using System.Net.Http;
using System.Threading.Tasks;

namespace ABSmartly;

public interface IABsmartlyHttpClientFactory
{
    IABsmartlyHttpClient CreateClient();
}

public interface IABsmartlyHttpClient : IDisposable
{
    Task<HttpResponseMessage> GetAsync(string? requestUri);
    Task<HttpResponseMessage> PutAsync(string? requestUri, HttpContent content);
    void AddHeader(string name, string? value);
}
