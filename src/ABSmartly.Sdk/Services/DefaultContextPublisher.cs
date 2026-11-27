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
        // A split flush records each request it sends as delivered: a request of one that failed in a
        // way a retry can fix is raised, so it and the later requests stay queued. Any other failure is
        // only logged, as in 1.3.0.
        if (!published && publishEvent.SplitRequest && publishEvent.RetryableFailure)
            throw new InvalidOperationException("Publish failed: the collector did not accept the events.");
    }
}
