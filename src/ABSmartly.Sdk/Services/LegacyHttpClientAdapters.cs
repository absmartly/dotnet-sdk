using System.Net.Http;
using System.Threading.Tasks;

namespace ABSmartly.Services;

#pragma warning disable CS0618
internal sealed class LegacyHttpClientFactoryAdapter : IABsmartlyHttpClientFactory
{
    private readonly IABSdkHttpClientFactory _inner;

    private LegacyHttpClientFactoryAdapter(IABSdkHttpClientFactory inner)
    {
        _inner = inner;
    }

    // A factory passed to a 1.3.0 entry point is always called through
    // IABSdkHttpClientFactory, as in 1.3.0, even when it also implements the new
    // interface: a 1.3.0 subclass of ABSdkHttpClientFactory that re-implements
    // IABSdkHttpClientFactory.CreateClient (for a custom transport) must not be bypassed.
    public static IABsmartlyHttpClientFactory Wrap(IABSdkHttpClientFactory factory)
    {
        if (factory == null) return null;
        return new LegacyHttpClientFactoryAdapter(factory);
    }

    public IABsmartlyHttpClient CreateClient() => LegacyHttpClientAdapter.Wrap(_inner.CreateClient());
}

internal sealed class LegacyHttpClientAdapter : IABsmartlyHttpClient
{
    private readonly IABSdkHttpClient _inner;

    public LegacyHttpClientAdapter(IABSdkHttpClient inner)
    {
        _inner = inner;
    }

    // The SDK's own wrappers (exactly those types) implement both client interfaces with
    // the same members, so they are used directly. Any other legacy client, including a
    // subclass that re-implements IABSdkHttpClient, is called through that interface.
    public static IABsmartlyHttpClient Wrap(IABSdkHttpClient client)
    {
        if (client == null) return null;
        var type = client.GetType();
        if (type == typeof(ABsmartlyHttpClientFactory.HttpClientWrapper) ||
            type == typeof(ABSdkHttpClientFactory.HttpClientWrapper))
            return (IABsmartlyHttpClient)client;

        return new LegacyHttpClientAdapter(client);
    }

    public Task<HttpResponseMessage> GetAsync(string requestUri) => _inner.GetAsync(requestUri);

    public Task<HttpResponseMessage> PutAsync(string requestUri, HttpContent content) =>
        _inner.PutAsync(requestUri, content);

    public void AddHeader(string name, string value) => _inner.AddHeader(name, value);

    public void Dispose() => _inner.Dispose();
}

// Lets a 1.3.0-style IContextEventHandler (one that is not also an IContextPublisher)
// be used where the SDK now expects an IContextPublisher.
internal sealed class LegacyContextEventHandlerAdapter : IContextPublisher
{
    private LegacyContextEventHandlerAdapter(IContextEventHandler inner)
    {
        Inner = inner;
    }

    internal IContextEventHandler Inner { get; }

    public static IContextPublisher Wrap(IContextEventHandler handler)
    {
        if (handler == null) return null;
        return handler as IContextPublisher ?? new LegacyContextEventHandlerAdapter(handler);
    }

    public static IContextEventHandler Unwrap(IContextPublisher publisher) =>
        publisher is LegacyContextEventHandlerAdapter adapter ? adapter.Inner : publisher;

    public Task PublishAsync(IContext context, ABSmartly.Models.PublishEvent publishEvent) =>
        Inner.PublishAsync(context, publishEvent);
}
#pragma warning restore CS0618
