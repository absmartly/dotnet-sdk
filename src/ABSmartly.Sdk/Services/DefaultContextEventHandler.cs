using System.Threading.Tasks;
using ABSmartly.Models;

namespace ABSmartly.Services;

public class DefaultContextEventHandler : IContextEventHandler
{
    private readonly IABSmartlyServiceClient _client;

    public DefaultContextEventHandler(IABSmartlyServiceClient client)
    {
        _client = client;
    }

    public async Task PublishAsync(Context context, PublishEvent publishEvent)
    {
        await _client.PublishAsync(publishEvent);
    }
}