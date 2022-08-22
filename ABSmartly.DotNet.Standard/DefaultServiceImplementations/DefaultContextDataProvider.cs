using System.Threading.Tasks;
using ABSmartlySdk.Interfaces;
using ABSmartlySdk.Json;

namespace ABSmartlySdk.DefaultServiceImplementations;

public class DefaultContextDataProvider : IContextDataProvider
{
    private readonly IClient _client;

    public DefaultContextDataProvider(IClient client)
    {
        _client = client;
    }

    public async Task<ContextData> GetContextDataAsync()
    {
        return await _client.GetContextDataAsync();
    }
}