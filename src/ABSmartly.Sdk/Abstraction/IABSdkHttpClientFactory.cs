#nullable enable
using System;
using System.Net.Http;
using System.Threading.Tasks;

namespace ABSmartly;

[Obsolete("IABSdkHttpClientFactory has been renamed to IABsmartlyHttpClientFactory. Please use IABsmartlyHttpClientFactory instead.")]
public interface IABSdkHttpClientFactory
{
    IABSdkHttpClient CreateClient();
}

[Obsolete("IABSdkHttpClient has been renamed to IABsmartlyHttpClient. Please use IABsmartlyHttpClient instead.")]
public interface IABSdkHttpClient : IDisposable
{
    Task<HttpResponseMessage> GetAsync(string? requestUri);
    Task<HttpResponseMessage> PutAsync(string? requestUri, HttpContent content);
    void AddHeader(string name, string? value);
}
