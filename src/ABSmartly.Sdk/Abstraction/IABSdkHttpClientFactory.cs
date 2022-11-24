#nullable enable
using System;
using System.Net.Http;
using System.Threading.Tasks;

namespace ABSmartly;

public interface IABSdkHttpClientFactory
{
    IABSdkHttpClient CreateClient();
}

public interface IABSdkHttpClient: IDisposable
{
    Task<HttpResponseMessage> GetAsync(string? requestUri);
    Task<HttpResponseMessage> PutAsync(string? requestUri, HttpContent content);
    void AddHeader(string name, string? value);
}