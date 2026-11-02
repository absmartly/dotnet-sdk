using System.Threading.Tasks;
using ABSmartly.Models;

namespace ABSmartly.Services;

public class DefaultContextPublisher : IContextPublisher
{
    private readonly IABsmartlyServiceClient _client;

    public DefaultContextPublisher(IABsmartlyServiceClient client)
    {
        _client = client;
    }

    public async Task PublishAsync(IContext context, PublishEvent publishEvent)
    {
        await _client.PublishAsync(publishEvent);
    }
}
