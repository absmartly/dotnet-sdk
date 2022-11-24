using ABSmartly.Models;
using Microsoft.Extensions.Logging;

namespace ABSmartly.Sdk.Tests;

[TestFixture]
public class ABSdkTests
{
    private IABSdkHttpClientFactory _httpClientFactory = null!;
    // private IContextDataDeserializer _deserializer = null!;
    // private IContextEventSerializer _serializer = null!;
    private ILoggerFactory _loggerFactory = null!;
    private IABSdkHttpClient _httpClient = null!;
    private ABSmartlyServiceConfiguration _serviceConfig = null!;

    [SetUp]
    public void SetUp()
    {
        _httpClient = Mock.Of<IABSdkHttpClient>();
        _httpClientFactory = Mock.Of<IABSdkHttpClientFactory>(x => x.CreateClient() == _httpClient);
        // _deserializer = Mock.Of<IContextDataDeserializer>();
        // _serializer = Mock.Of<IContextEventSerializer>();
        _loggerFactory = Mock.Of<ILoggerFactory>();
        
        _serviceConfig = H.ServiceConfig("website", "http://localhost/v1", "dev", "test-api-key");
    }
    
    [Test]
    public void TestCreateThrowsWithInvalidConfig()
    {
        Func<ABSdk> act;
        
        act = () => new ABSdk(null, _serviceConfig);
        act.Should().Throw<ArgumentNullException>()
            .WithMessage("Missing HTTP client factory configuration (Parameter 'httpClientFactory')");

        act = () => new ABSdk(_httpClientFactory, null);
        act.Should().Throw<ArgumentNullException>()
            .WithMessage($"{nameof(ABSmartlyService)} config is required (Parameter 'config')");
    }
    
    [Test]
    public void TestCreateContext_DefaultConfig()
    {
        var abSdk = new ABSdk(_httpClientFactory, _serviceConfig);
        var context = abSdk.CreateContext(new ContextConfig());

        context.Should().NotBeNull();
    }
    
    [Test]
    public async Task TestCreateContextAsync_DefaultConfig()
    {
        var abSdk = new ABSdk(_httpClientFactory, _serviceConfig);
        var context = await abSdk.CreateContextAsync(new ContextConfig());

        context.Should().NotBeNull();
    }
    
    [Test]
    public void TestCreateContextWith()
    {
        var expected = new ContextData {Experiments = Array.Empty<Experiment>()};
        var dataProvider = Mock.Of<IContextDataProvider>();
        Mock.Get(dataProvider).Setup(x => x.GetContextDataAsync()).Returns(GetDataFn);
        
        var abSdk = new ABSdk(_httpClientFactory, _serviceConfig, new ABSdkConfig{ ContextDataProvider = dataProvider});
        var context = abSdk.CreateContextWith(new ContextConfig(), expected);
        
        context.Should().NotBeNull();
        context.GetContextData().Should().BeSameAs(expected);

        Task<ContextData> GetDataFn() => Task.FromResult(expected);
    }
}