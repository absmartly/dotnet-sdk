using System;
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
        var published = await _client.PublishAsync(publishEvent).ConfigureUnboundContinuation();
        // With switchbacks one flush can send several requests: a request the client could not
        // deliver fails the flush, so it and every later request stay queued for a retry instead of
        // being recorded as delivered. Without switchbacks a failure is only logged, as in 1.3.0.
        if (!published && publishEvent.SwitchbackWireAttributes)
            throw new InvalidOperationException("Publish failed: the collector did not accept the events.");
    }
}
