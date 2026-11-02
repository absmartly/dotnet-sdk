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

    public static IABsmartlyHttpClientFactory Wrap(IABSdkHttpClientFactory factory)
    {
        if (factory == null) return null;
        return factory as IABsmartlyHttpClientFactory ?? new LegacyHttpClientFactoryAdapter(factory);
    }

    public IABsmartlyHttpClient CreateClient()
    {
        var client = _inner.CreateClient();
        return client as IABsmartlyHttpClient ?? new LegacyHttpClientAdapter(client);
    }
}

internal sealed class LegacyHttpClientAdapter : IABsmartlyHttpClient
{
    private readonly IABSdkHttpClient _inner;

    public LegacyHttpClientAdapter(IABSdkHttpClient inner)
    {
        _inner = inner;
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
