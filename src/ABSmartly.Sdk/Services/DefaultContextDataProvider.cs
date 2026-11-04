using System.Threading.Tasks;
using ABSmartly.Extensions;
using ABSmartly.Models;

namespace ABSmartly.Services;

public class DefaultContextDataProvider : IContextDataProvider, IThrowingContextDataProvider
{
    private readonly IABSmartlyServiceClient _client;

    public DefaultContextDataProvider(IABSmartlyServiceClient client)
    {
        _client = client;
    }

    // As 1.3.0: delegates to the client's public GetContextDataAsync, so with the SDK's
    // own service a failed fetch returns null to direct callers.
    public async Task<ContextData> GetContextDataAsync()
    {
        return await _client.GetContextDataAsync();
    }

    // Used by the SDK's own context loading and refresh: with the SDK's own service
    // client it throws the real cause of a failed fetch (it becomes the context's
    // ReadyError, or the error a refresh throws) instead of returning null.
    async Task<ContextData> IThrowingContextDataProvider.FetchContextDataAsync()
    {
        if (_client is ABSmartlyService service)
            return await service.FetchContextDataAsync().ConfigureUnboundContinuation();

        return await _client.GetContextDataAsync().ConfigureUnboundContinuation();
    }
}

// Implemented by the SDK's default provider: a fetch that surfaces the real cause of a
// failure, for the SDK's own loading, separate from the public 1.3.0 null-on-failure
// GetContextDataAsync.
internal interface IThrowingContextDataProvider
{
    Task<ContextData> FetchContextDataAsync();
}