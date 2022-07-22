using System.Threading.Tasks;
using ABSmartly.Json;

namespace ABSmartly;

public class DefaultContextDataDeserializer : IContextEventHandler
{
    private readonly Client _client;

    public DefaultContextDataDeserializer(Client client)
    {
        _client = client;
    }


    public async Task PublishAsync(Context context, PublishEvent publishEvent)
    {
        return await _client.Publish(publishEvent);
    }
}