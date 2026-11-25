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

    // As released, the service reads the caller's configuration on every request, and each context
    // reads the collector attributes when it is created: a context created after a change serves with
    // the values its requests carry, and one created before keeps serving with its own.
    [Test]
    public void ContextsReadTheServiceConfigurationWhenCreated()
    {
        var experiment = new Experiment
        {
            Id = 1, Name = "exp_sb", UnitType = "store_id", Iteration = 1, SeedHi = 0, SeedLo = 42,
            Split = new[] { 0.5, 0.5 }, TrafficSeedHi = 1, TrafficSeedLo = 2, TrafficSplit = new[] { 0.0, 1.0 },
            FullOnVariant = 0, Applications = new[] { new ExperimentApplication { Name = "website" } },
            Variants = new[] { new ExperimentVariant { Name = "A" }, new ExperimentVariant { Name = "B" } },
            RandomizationType = "switchback",
            SwitchbackClusters = new[] { new KeyValuePair<string, string>("store_id", "environment") },
            SwitchbackClusterValues = new[] { "production" },
            SwitchbackStartAt = 0, SwitchbackPeriodMs = 400_000L * 3_600_000
        };
        var data = new ContextData { Experiments = new[] { experiment } };
        var absmartly = new ABsmartly.Sdk(_httpClientFactory, _serviceConfig);
        var config = new ContextConfig();
        config.SetUnit("store_id", "s-1");
        var before = (Context)absmartly.CreateContextWith(config, data);

        _serviceConfig.Environment = "production";
        var after = (Context)absmartly.CreateContextWith(config, data);

        after.GetTreatment("exp_sb");
        after.PendingCount.Should().Be(1, "created after the change, it reads production");
        before.GetTreatment("exp_sb");
        before.PendingCount.Should().Be(0, "created before it, it keeps dev, outside the allow-list");
    }
}
