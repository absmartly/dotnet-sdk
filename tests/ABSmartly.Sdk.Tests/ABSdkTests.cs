using ABSmartly.Models;

namespace ABSmartly.Sdk.Tests;

[TestFixture]
public class ABSdkTests
{
    [SetUp]
    public void SetUp()
    {
        _httpClient = Mock.Of<IABsmartlyHttpClient>();
        _httpClientFactory = Mock.Of<IABsmartlyHttpClientFactory>(x => x.CreateClient() == _httpClient);

        _serviceConfig = H.ServiceConfig("website", "http://localhost/v1", "dev", "test-api-key");
    }

    private IABsmartlyHttpClientFactory _httpClientFactory = null!;

    private IABsmartlyHttpClient _httpClient = null!;
    private ABSmartlyServiceConfiguration _serviceConfig = null!;

    [Test]
    public void TestCreateThrowsWithInvalidConfig()
    {
        Func<ABsmartly> act;

        act = () => new ABsmartly(null, _serviceConfig);
        act.Should().Throw<ArgumentNullException>()
            .WithMessage("Missing HTTP client factory configuration (Parameter 'httpClientFactory')");

        act = () => new ABsmartly(_httpClientFactory, null);
        act.Should().Throw<ArgumentNullException>()
            .WithMessage($"{nameof(ABSmartlyService)} config is required (Parameter 'config')");
    }

    [Test]
    public void TestCreateContext_DefaultConfig()
    {
        var absmartly = new ABsmartly(_httpClientFactory, _serviceConfig);
        var context = absmartly.CreateContext(new ContextConfig());

        context.Should().NotBeNull();
    }

    [Test]
    public async Task TestCreateContextAsync_DefaultConfig()
    {
        var absmartly = new ABsmartly(_httpClientFactory, _serviceConfig);
        var context = await absmartly.CreateContextAsync(new ContextConfig());

        context.Should().NotBeNull();
    }

    [Test]
    public void TestCreateContextWith()
    {
        var expected = new ContextData { Experiments = Array.Empty<Experiment>() };
        var dataProvider = Mock.Of<IContextDataProvider>();
        Mock.Get(dataProvider).Setup(x => x.GetContextDataAsync()).Returns(GetDataFn);

        var absmartly = new ABsmartly(_httpClientFactory, _serviceConfig,
            new ABsmartlyConfig { ContextDataProvider = dataProvider });
        var context = absmartly.CreateContextWith(new ContextConfig(), expected);

        context.Should().NotBeNull();
        context.GetContextData().Should().BeSameAs(expected);

        Task<ContextData> GetDataFn()
        {
            return Task.FromResult(expected);
        }
    }
}