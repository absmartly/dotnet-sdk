using System.Net.Http;

namespace ABSmartly;

public interface IABSdkHttpClientFactory
{
    HttpClient CreateClient();
}