using System.Threading.Tasks;
using ABSmartly.Interfaces;
using ABSmartly.Json;

namespace ABSmartly.DefaultServiceImplementations;

public class DefaultContextEventHandler : IContextEventHandler
{
    private readonly IClient _client;

    public DefaultContextEventHandler(IClient client)
    {
        _client = client;
    }

    public async Task PublishAsync(Context context, PublishEvent publishEvent)
    {
        await _client.PublishAsync(publishEvent);
    }
}