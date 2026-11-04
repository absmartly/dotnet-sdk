#pragma warning disable CS0618
using System.Net.Http;
using ABSmartly.Models;
using ABSmartly.Services;
using ABSmartly.Time;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ABSmartly.Sdk.Tests;

// Source-compatibility checks against the 1.3.0 public API. Most of these tests matter
// because they compile: if a 1.3.0 signature is removed, renamed or made ambiguous, the
// test project stops building on every test TFM.
[TestFixture]
public class ApiCompatibilityTests
{
    private static ABSmartlyServiceConfiguration ServiceConfig() =>
        H.ServiceConfig("website", "https://example.absmartly.io/v1", "dev", "key");

    private ContextData _data = null!;

    [SetUp]
    public void SetUp()
    {
        using var stream =
            GetType().Assembly.GetManifestResourceStream("ABSmartly.Sdk.Tests.Resources.context.json");
        _data = new DefaultContextDataDeserializer().Deserialize(stream)!;
    }

    // A third-party IContext implementation with exactly the 1.3.0 member set. Adding any
    // member to IContext (abstract or default-implemented) is a break for these.
    private sealed class V130Context : IContext
    {
        public int PendingCount => 0;
        public bool IsReady() => true;
        public bool IsFailed() => false;
        public bool IsClosed() => false;
        public bool IsClosing() => false;
        public string[] GetExperiments() => Array.Empty<string>();
        public ContextData GetContextData() => new();
        public void SetAttribute(string name, object value) { }
        public void SetAttributes(Dictionary<string, object> attributes) { }
        public void SetCustomAssignment(string experimentName, int variant) { }
        public int? GetCustomAssignment(string experimentName) => null;
        public void SetCustomAssignments(Dictionary<string, int> customAssignments) { }
        public void SetOverride(string experimentName, int variant) { }
        public int? GetOverride(string experimentName) => null;
        public void SetOverrides(Dictionary<string, int> overrides) { }
        public int GetTreatment(string experimentName) => 0;
        public int PeekTreatment(string experimentName) => 0;
        public void SetUnit(string unitType, string uid) { }
        public void SetUnits(Dictionary<string, string> units) { }
        public Dictionary<string, string> GetVariableKeys() => new();
        public object GetVariableValue(string key, object defaultValue) => defaultValue;
        public object PeekVariableValue(string key, object defaultValue) => defaultValue;
        public void Publish() { }
        public Task PublishAsync() => Task.CompletedTask;
        public void Refresh() { }
        public Task RefreshAsync() => Task.CompletedTask;
        public void Track(string goalName, Dictionary<string, object> properties) { }
    }

    [Test]
    public void IContextHasExactlyThe130MemberSet()
    {
        IContext context = new V130Context();
        context.IsReady().Should().BeTrue();

        // Guards against default-interface members too, which the fake above would not catch.
        var names = typeof(IContext).GetMembers().Select(m => m.Name).Distinct().OrderBy(n => n).ToArray();
        names.Should().BeEquivalentTo(
            "get_PendingCount", "PendingCount", "IsReady", "IsFailed", "IsClosed", "IsClosing", "GetExperiments",
            "GetContextData", "SetAttribute", "SetAttributes", "SetCustomAssignment", "GetCustomAssignment",
            "SetCustomAssignments", "SetOverride", "GetOverride", "SetOverrides", "GetTreatment", "PeekTreatment",
            "SetUnit", "SetUnits", "GetVariableKeys", "GetVariableValue", "PeekVariableValue", "Publish",
            "PublishAsync", "Refresh", "RefreshAsync", "Track");
    }

    [Test]
    public void ContextImplementsIContextV2()
    {
        var sdk = new ABsmartly(Mock.Of<IABsmartlyHttpClientFactory>(), ServiceConfig(),
            new ABsmartlyConfig { ContextDataProvider = Mock.Of<IContextDataProvider>() });
        IContext context = sdk.CreateContextWith(new ContextConfig(), _data);
        context.Should().BeAssignableTo<IContextV2>();
        ((IContextV2)context).Experiments.Should().NotBeEmpty();
    }

    [Test]
    public void CreateContextMethodsKeepTheir130ReturnTypes()
    {
        typeof(ABsmartly).GetMethod(nameof(ABsmartly.CreateContext))!.ReturnType.Should().Be(typeof(IContext));
        typeof(ABsmartly).GetMethod(nameof(ABsmartly.CreateContextAsync))!.ReturnType.Should()
            .Be(typeof(Task<IContext>));
        typeof(ABsmartly).GetMethod(nameof(ABsmartly.CreateContextWith))!.ReturnType.Should().Be(typeof(IContext));
    }

    // 1.3.0 declared PublishAsync on IContextEventHandler, so explicit implementations name it.
    private sealed class ExplicitEventHandler : IContextEventHandler
    {
        public int Calls;

        Task IContextEventHandler.PublishAsync(IContext context, PublishEvent publishEvent)
        {
            Calls++;
            return Task.CompletedTask;
        }
    }

    private sealed class ExplicitPublisher : IContextPublisher
    {
        Task IContextEventHandler.PublishAsync(IContext context, PublishEvent publishEvent) => Task.CompletedTask;
    }

    private Context NewLegacyContext(IContextEventHandler handler) =>
        new(new ContextConfig().SetUnit("session_id", "e791e240fcd3df7d238cfc285f475e8152fcc0ec"), _data,
            Clock.Fixed(1_620_000_000_000L), Mock.Of<IContextDataProvider>(), handler, null!,
            new DefaultVariableParser(), new AudienceMatcher(new DefaultAudienceDeserializer()), new LoggerFactory());

    [Test]
    public async Task LegacyContextConstructorAcceptsAPlainIContextEventHandler()
    {
        var handler = new ExplicitEventHandler();
        var context = NewLegacyContext(handler);
        context.Track("goal", null);
        await context.PublishAsync();
        handler.Calls.Should().Be(1);
    }

    [Test]
    public void PublisherIsUsableAsLegacyEventHandler()
    {
        IContextEventHandler legacy = new ExplicitPublisher();
        legacy.Should().NotBeNull();

        var config = new ABsmartlyConfig { ContextEventHandler = new ExplicitEventHandler() };
        config.ContextPublisher.Should().NotBeNull();
        config.ContextEventHandler.Should().BeOfType<ExplicitEventHandler>();
    }

    [Test]
    public void ContextConstructorWithNullHandlerIsNotAmbiguous()
    {
        // Both the IContextPublisher and the obsolete IContextEventHandler overloads accept null;
        // the more specific IContextPublisher one wins.
        var context = new Context(new ContextConfig(), _data, Clock.Fixed(0), Mock.Of<IContextDataProvider>(), null,
            null, new DefaultVariableParser(), new AudienceMatcher(new DefaultAudienceDeserializer()),
            new LoggerFactory());
        context.IsReady().Should().BeTrue();
    }

    [Test]
    public void DisposeBoolIsPublic()
    {
        var context = NewLegacyContext(Mock.Of<IContextEventHandler>());
        context.Dispose(true);
        context.IsClosed().Should().BeTrue();
    }

    [Test]
    public void CustomFieldMethodsKeepThe130ParameterName()
    {
        var context = NewLegacyContext(Mock.Of<IContextEventHandler>());
        context.GetCustomFieldValue(environmentName: "exp_test_ab", key: "country").Should().Be("US,PT,ES,DE,FR");
        context.GetCustomFieldType(environmentName: "exp_test_ab", key: "country").Should().Be("string");
    }

    private sealed class LegacyClient : IABSdkHttpClient
    {
        public Task<HttpResponseMessage> GetAsync(string? requestUri) => Task.FromResult(new HttpResponseMessage());
        public Task<HttpResponseMessage> PutAsync(string? requestUri, HttpContent content) =>
            Task.FromResult(new HttpResponseMessage());
        public void AddHeader(string name, string? value) { }
        public void Dispose() { }
    }

    private sealed class LegacyFactory : IABSdkHttpClientFactory
    {
        public int Calls;
        public IABSdkHttpClient CreateClient() { Calls++; return new LegacyClient(); }
    }

    [Test]
    public async Task ABSmartlyServiceAcceptsLegacyFactory()
    {
        var factory = new LegacyFactory();
        var service = new ABSmartlyService(ServiceConfig(), factory, Mock.Of<IContextDataDeserializer>(),
            Mock.Of<IContextEventSerializer>(x => x.Serialize(It.IsAny<PublishEvent>()) == new byte[] { 1 }));
        await service.PublishAsync(new PublishEvent());
        factory.Calls.Should().Be(1);
    }

    [Test]
    public void ABSmartlyServiceOverloadsAreNotAmbiguous()
    {
        var deserializer = Mock.Of<IContextDataDeserializer>();
        var serializer = Mock.Of<IContextEventSerializer>();

        // ABSdkHttpClientFactory implements both factory interfaces.
        new ABSmartlyService(ServiceConfig(), new ABSdkHttpClientFactory(Mock.Of<IHttpClientFactory>()),
            deserializer, serializer).Should().NotBeNull();

        Func<ABSmartlyService> withNull = () => new ABSmartlyService(ServiceConfig(), null, deserializer, serializer);
        withNull.Should().Throw<ArgumentNullException>();
    }

    [Test]
    public void ABSdkAcceptsLegacyConfigAndIsNotAmbiguous()
    {
        var factory = new LegacyFactory();
        new ABSdk(factory, ServiceConfig()).Should().NotBeNull();
        new ABSdk(factory, ServiceConfig(), null).Should().NotBeNull();
        new ABSdk(factory, ServiceConfig(), new ABSdkConfig()).Should().NotBeNull();
        new ABSdk(factory, ServiceConfig(), new ABsmartlyConfig()).Should().NotBeNull();
        new ABSdk(factory, ServiceConfig(), loggerFactory: new LoggerFactory()).Should().NotBeNull();
        new ABSdk(new ABSdkHttpClientFactory(Mock.Of<IHttpClientFactory>()), ServiceConfig()).Should().NotBeNull();
        new ABSdk(factory, Options.Create(ServiceConfig()), Options.Create(new ABSdkConfig()), new LoggerFactory())
            .Should().NotBeNull();
        ABSdk.HttpClientName.Should().Be(ABsmartly.HttpClientName);
    }

    [Test]
    public void ABSdkHttpClientFactoryKeepsIts130Surface()
    {
        var httpClientFactory = Mock.Of<IHttpClientFactory>(x => x.CreateClient(It.IsAny<string>()) == new HttpClient());
        var factory = new ABSdkHttpClientFactory(httpClientFactory);

        IABSdkHttpClient legacyClient = factory.CreateClient();
        legacyClient.Should().BeOfType<ABSdkHttpClientFactory.HttpClientWrapper>();
        legacyClient.Dispose();

        using var client = ((IABsmartlyHttpClientFactory)factory).CreateClient();
        client.Should().BeAssignableTo<IABSdkHttpClient>();

        using var wrapper = new ABSdkHttpClientFactory.HttpClientWrapper(new HttpClient());
        wrapper.Should().BeAssignableTo<IABSdkHttpClient>();
    }

    // 1.3.0 contract for direct callers: a failed fetch returns null instead of throwing,
    // while context creation through the SDK still reports the real error as ReadyError.
    [Test]
    public async Task ServiceGetContextDataAsyncReturnsNullOnFailedFetchAsIn130()
    {
        var client = Mock.Of<IABsmartlyHttpClient>();
        Mock.Get(client).Setup(x => x.GetAsync(It.IsAny<string>())).ThrowsAsync(new HttpRequestException("offline"));
        var factory = Mock.Of<IABsmartlyHttpClientFactory>(x => x.CreateClient() == client);
        var config = new ABSmartlyServiceConfiguration
            { Endpoint = "https://example.absmartly.io/v1", ApiKey = "k", Application = "a", Environment = "e" };

        var service = new ABSmartlyService(config, factory, new DefaultContextDataDeserializer(),
            new DefaultContextEventSerializer());
        (await service.GetContextDataAsync()).Should().BeNull();

        var sdk = new ABsmartly(factory, config);
        var context = (IContextV2)await sdk.CreateContextAsync(
            new ContextConfig().SetUnit("session_id", "abc"));
        context.IsFailed().Should().BeTrue();
        context.ReadyError.Should().BeOfType<HttpRequestException>().Which.Message.Should().Be("offline");
    }

    // 1.3.0 contract for direct callers of the public provider: a failed fetch through the
    // SDK's own service returns null. Context loading and refresh still report the real
    // error.
    [Test]
    public async Task DefaultContextDataProviderReturnsNullOnFailedFetchAsIn130()
    {
        var client = Mock.Of<IABsmartlyHttpClient>();
        Mock.Get(client).Setup(x => x.GetAsync(It.IsAny<string>())).ThrowsAsync(new HttpRequestException("offline"));
        var factory = Mock.Of<IABsmartlyHttpClientFactory>(x => x.CreateClient() == client);
        var config = new ABSmartlyServiceConfiguration
            { Endpoint = "https://example.absmartly.io/v1", ApiKey = "k", Application = "a", Environment = "e" };
        var service = new ABSmartlyService(config, factory, new DefaultContextDataDeserializer(),
            new DefaultContextEventSerializer());

        var provider = new DefaultContextDataProvider(service);
        (await provider.GetContextDataAsync()).Should().BeNull();

        var sdk = new ABsmartly(factory, config, new ABsmartlyConfig { ContextDataProvider = provider });
        var context = (IContextV2)await sdk.CreateContextAsync(new ContextConfig().SetUnit("session_id", "abc"));
        context.IsFailed().Should().BeTrue();
        context.ReadyError.Should().BeOfType<HttpRequestException>().Which.Message.Should().Be("offline");
    }

    // Refresh through the default provider rethrows the real error of a failed fetch and
    // keeps the existing data.
    [Test]
    public async Task RefreshThroughDefaultProviderThrowsRealErrorOnFailedFetch()
    {
        var client = Mock.Of<IABsmartlyHttpClient>();
        Mock.Get(client).Setup(x => x.GetAsync(It.IsAny<string>())).ThrowsAsync(new HttpRequestException("offline"));
        var factory = Mock.Of<IABsmartlyHttpClientFactory>(x => x.CreateClient() == client);
        var config = new ABSmartlyServiceConfiguration
            { Endpoint = "https://example.absmartly.io/v1", ApiKey = "k", Application = "a", Environment = "e" };
        var sdk = new ABsmartly(factory, config);

        var context = (Context)sdk.CreateContextWith(new ContextConfig().SetUnit("session_id", "abc"), _data);
        Func<Task> refresh = () => context.RefreshAsync();
        await refresh.Should().ThrowAsync<HttpRequestException>().WithMessage("offline");
        context.IsReady().Should().BeTrue();
        context.IsFailed().Should().BeFalse();
    }

    // A consumer subclass of DefaultContextDataProvider that reimplements
    // IContextDataProvider.GetContextDataAsync (here, serving cached data) is called for
    // creation and refresh, as in 1.3.0, instead of the underlying service.
    private sealed class CachingProvider : DefaultContextDataProvider, IContextDataProvider
    {
        private readonly ContextData _cached;
        public int Calls;

        public CachingProvider(IABSmartlyServiceClient client, ContextData cached) : base(client) => _cached = cached;

        Task<ContextData> IContextDataProvider.GetContextDataAsync()
        {
            Interlocked.Increment(ref Calls);
            return Task.FromResult(_cached);
        }
    }

    [Test]
    public async Task DefaultContextDataProviderSubclassReimplementationIsUsedForCreationAndRefresh()
    {
        var client = Mock.Of<IABsmartlyHttpClient>();
        Mock.Get(client).Setup(x => x.GetAsync(It.IsAny<string>())).ThrowsAsync(new HttpRequestException("offline"));
        var factory = Mock.Of<IABsmartlyHttpClientFactory>(x => x.CreateClient() == client);
        var config = new ABSmartlyServiceConfiguration
            { Endpoint = "https://example.absmartly.io/v1", ApiKey = "k", Application = "a", Environment = "e" };
        var service = new ABSmartlyService(config, factory, new DefaultContextDataDeserializer(),
            new DefaultContextEventSerializer());
        var provider = new CachingProvider(service, _data);

        var sdk = new ABsmartly(factory, config, new ABsmartlyConfig { ContextDataProvider = provider });
        var context = (IContextV2)await sdk.CreateContextAsync(new ContextConfig().SetUnit("session_id", "abc"));
        context.IsFailed().Should().BeFalse();
        context.ReadyError.Should().BeNull();

        await ((Context)context).RefreshAsync();
        provider.Calls.Should().Be(2);
        Mock.Get(client).Verify(x => x.GetAsync(It.IsAny<string>()), Times.Never);
    }
}
