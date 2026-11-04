using System.Threading.Tasks;
using ABSmartly.Extensions;
using ABSmartly.Models;

namespace ABSmartly.Services;

public class DefaultContextDataProvider : IContextDataProvider
{
    private readonly IABSmartlyServiceClient _client;

    public DefaultContextDataProvider(IABSmartlyServiceClient client)
    {
        _client = client;
    }

    // With the SDK's own service client, fetch through the path that throws the real
    // cause of a failure (it becomes the context's ReadyError) instead of the public
    // GetContextDataAsync, which returns null on failure as in 1.3.0.
    public async Task<ContextData> GetContextDataAsync()
    {
        if (_client is ABSmartlyService service)
            return await service.FetchContextDataAsync().ConfigureUnboundContinuation();

        return await _client.GetContextDataAsync();
    }
}