using System.Threading.Tasks;
using ABSmartly.Json;

namespace ABSmartly;

public class DefaultContextEventHandler : IContextEventHandler
{
    private readonly Client _client;

    public DefaultContextEventHandler(Client client)
    {
        _client = client;
    }

    public async Task PublishAsync(Context context, PublishEvent publishEvent)
    {
        await _client.PublishAsync(publishEvent);
    }
}