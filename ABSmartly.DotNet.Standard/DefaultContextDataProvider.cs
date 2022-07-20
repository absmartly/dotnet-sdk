using System.Threading.Tasks;
using ABSmartly.Json;

namespace ABSmartly;

public class DefaultContextDataProvider : IContextDataProvider
{
    private readonly Client _client;

    public DefaultContextDataProvider(Client client)
    {
        _client = client;
    }

    public async Task<ContextData> GetContextDataAsync()
    {
        return await _client.GetContextDataAsync();
    }
}