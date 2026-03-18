using System.Threading.Tasks;
using ABSmartly.Models;

namespace ABSmartly.Services;

public class DefaultContextPublisher : IContextPublisher
{
    private readonly IABSmartlyServiceClient _client;

    public DefaultContextPublisher(IABSmartlyServiceClient client)
    {
        _client = client;
    }

    public async Task PublishAsync(IContext context, PublishEvent publishEvent)
    {
        await _client.PublishAsync(publishEvent);
    }
}
