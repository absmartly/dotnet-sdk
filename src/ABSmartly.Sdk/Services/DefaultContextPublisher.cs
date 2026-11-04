using System.Threading.Tasks;
using ABSmartly.Extensions;
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
        // Not bound to the caller's SynchronizationContext: a close blocked on the UI
        // thread waits for this publication, which must not need that thread to finish.
        await _client.PublishAsync(publishEvent).ConfigureUnboundContinuation();
    }
}
