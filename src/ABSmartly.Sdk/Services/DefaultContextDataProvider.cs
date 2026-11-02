using System.Threading.Tasks;
using ABSmartly.Models;

namespace ABSmartly.Services;

public class DefaultContextDataProvider : IContextDataProvider
{
    private readonly IABsmartlyServiceClient _client;

    public DefaultContextDataProvider(IABsmartlyServiceClient client)
    {
        _client = client;
    }

    public async Task<ContextData> GetContextDataAsync()
    {
        return await _client.GetContextDataAsync();
    }
}