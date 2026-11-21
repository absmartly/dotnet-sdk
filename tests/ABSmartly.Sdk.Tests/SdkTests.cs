using ABSmartly.Models;
using ABSmartly.Services;

namespace ABSmartly.Sdk.Tests;

[TestFixture]
public class SdkTests
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
        Func<ABsmartly.Sdk> act;

        act = () => new ABsmartly.Sdk(null, _serviceConfig);
        act.Should().Throw<ArgumentNullException>()
            .WithMessage("Missing HTTP client factory configuration (Parameter 'httpClientFactory')");

        act = () => new ABsmartly.Sdk(_httpClientFactory, null);
        act.Should().Throw<ArgumentNullException>()
            .WithMessage($"{nameof(ABSmartlyService)} config is required (Parameter 'config')");
    }

    [Test]
    public void TestCreateContext_DefaultConfig()
    {
        var absmartly = new ABsmartly.Sdk(_httpClientFactory, _serviceConfig);
        var context = absmartly.CreateContext(new ContextConfig());

        context.Should().NotBeNull();
    }

    [Test]
    public async Task TestCreateContextAsync_DefaultConfig()
    {
        var absmartly = new ABsmartly.Sdk(_httpClientFactory, _serviceConfig);
        var context = await absmartly.CreateContextAsync(new ContextConfig());

        context.Should().NotBeNull();
    }

    [Test]
    public void TestCreateContextWith()
    {
        var expected = new ContextData { Experiments = Array.Empty<Experiment>() };
        var dataProvider = Mock.Of<IContextDataProvider>();
        Mock.Get(dataProvider).Setup(x => x.GetContextDataAsync()).Returns(GetDataFn);

        var absmartly = new ABsmartly.Sdk(_httpClientFactory, _serviceConfig,
            new ABsmartlyConfig { ContextDataProvider = dataProvider });
        var context = absmartly.CreateContextWith(new ContextConfig(), expected);

        context.Should().NotBeNull();
        context.GetContextData().Should().BeSameAs(expected);

        Task<ContextData> GetDataFn()
        {
            return Task.FromResult(expected);
        }
    }

    [Test]
    public async Task ContextsReadTheConfiguredCollectorAttributesInClusterPaths()
    {
        var experiment = new Experiment
        {
            Id = 1, Name = "exp_sb", UnitType = "store_id", Iteration = 1, SeedHi = 0, SeedLo = 42,
            Split = new[] { 0.5, 0.5 }, TrafficSeedHi = 1, TrafficSeedLo = 2, TrafficSplit = new[] { 0.0, 1.0 },
            FullOnVariant = 0, Applications = new[] { new ExperimentApplication { Name = "website" } },
            Variants = new[] { new ExperimentVariant { Name = "A" }, new ExperimentVariant { Name = "B" } },
            RandomizationType = "switchback",
            SwitchbackClusters = new[] { new KeyValuePair<string, string>("store_id", "environment") },
            SwitchbackStartAt = 0, SwitchbackPeriodMs = 400_000L * 3_600_000
        };
        var data = new ContextData { Experiments = new[] { experiment } };
        var dataProvider = Mock.Of<IContextDataProvider>(x => x.GetContextDataAsync() == Task.FromResult(data));
        var absmartly = new ABsmartly.Sdk(_httpClientFactory, _serviceConfig,
            new ABsmartlyConfig { ContextDataProvider = dataProvider });
        var config = new ContextConfig();
        config.SetUnit("store_id", "s-1");
        // The contexts read the system clock; the 400,000-hour period keeps the index stable.
        var period = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() / experiment.SwitchbackPeriodMs!.Value;
        var expected = ABSmartly.Internal.SwitchbackAssigner.Assign("dev", period, 42, 2);

        var with = (Context)absmartly.CreateContextWith(config, data);
        with.GetTreatment("exp_sb").Should().Be(expected);
        with.PendingCount.Should().Be(1, "an eligible cluster is exposed");

        var pending = (Context)await absmartly.CreateContextAsync(config);
        pending.GetTreatment("exp_sb").Should().Be(expected);
        pending.PendingCount.Should().Be(1);
    }

    // The Sdk copies its service configuration when built: changing the caller's object later
    // changes neither the values cluster lookups read nor the headers sent.
    [Test]
    public void LaterChangesToTheServiceConfigurationAreNotSeen()
    {
        var experiment = new Experiment
        {
            Id = 1, Name = "exp_sb", UnitType = "store_id", Iteration = 1, SeedHi = 0, SeedLo = 42,
            Split = new[] { 0.5, 0.5 }, TrafficSeedHi = 1, TrafficSeedLo = 2, TrafficSplit = new[] { 0.0, 1.0 },
            FullOnVariant = 0, Applications = new[] { new ExperimentApplication { Name = "website" } },
            Variants = new[] { new ExperimentVariant { Name = "A" }, new ExperimentVariant { Name = "B" } },
            RandomizationType = "switchback",
            SwitchbackClusters = new[] { new KeyValuePair<string, string>("store_id", "environment") },
            SwitchbackClusterValues = new[] { "dev" },
            SwitchbackStartAt = 0, SwitchbackPeriodMs = 400_000L * 3_600_000
        };
        var data = new ContextData { Experiments = new[] { experiment } };
        var absmartly = new ABsmartly.Sdk(_httpClientFactory, _serviceConfig);
        _serviceConfig.Environment = "production";
        var config = new ContextConfig();
        config.SetUnit("store_id", "s-1");
        var context = (Context)absmartly.CreateContextWith(config, data);
        context.GetTreatment("exp_sb");
        context.PendingCount.Should().Be(1, "the cluster is still the configured dev");
    }
}
