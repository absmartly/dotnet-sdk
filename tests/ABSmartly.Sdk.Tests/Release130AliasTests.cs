#pragma warning disable CS0618
using System.Net.Http;
using System.Reflection;
using ABSmartly;
using ABSmartly.DependencyInjection;
using ABSmartly.Models;
using ABSmartly.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

// Deliberately outside the ABSmartly namespace: this file is written the way a 1.3.0 consumer
// wrote it (only `using ABSmartly...` imports), so it proves the 1.3.0 names still compile from
// application code and forward to the new ABsmartly.Sdk entry point.
namespace Release130Consumer;

[TestFixture]
public class Release130AliasTests
{
    private static ABSmartlyServiceConfiguration ServiceConfig() => new()
    {
        Environment = "prod",
        Application = "www",
        Endpoint = "https://acme.absmartly.io/v1",
        ApiKey = "YOUR_API_KEY"
    };

    private static IContextDataProvider DataProvider()
    {
        var provider = new Mock<IContextDataProvider>();
        provider.Setup(x => x.GetContextDataAsync())
            .ReturnsAsync(new ContextData { Experiments = Array.Empty<Experiment>() });
        return provider.Object;
    }

    [Test]
    public async Task ABSdkConstructedAsIn130CreatesContexts()
    {
        var abSdk = new ABSdk(new ABSdkHttpClientFactory(Mock.Of<IHttpClientFactory>()), ServiceConfig(),
            new ABSdkConfig { ContextDataProvider = DataProvider() });

        abSdk.Should().BeAssignableTo<ABsmartly.Sdk>();

        var context = await abSdk.CreateContextAsync(new ContextConfig().SetUnit("user_id", "123"));
        context.IsReady().Should().BeTrue();
        context.IsFailed().Should().BeFalse();
        context.GetTreatment("unknown").Should().Be(0);
    }

    [Test]
    public void ABSdkHttpClientNameMatchesSdk()
    {
        ABSdk.HttpClientName.Should().Be("ABSmartlySDK.HttpClient");
        ABsmartly.Sdk.HttpClientName.Should().Be(ABSdk.HttpClientName);
    }

    [Test]
    public void AddABSmartlyRegistersBothEntryPoints()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string>
        {
            ["Endpoint"] = "https://acme.absmartly.io/v1", ["ApiKey"] = "k", ["Application"] = "a", ["Environment"] = "e"
        }).Build();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddABSmartly(configuration, HttpClientConfig.CreateDefault(),
            config => config.ContextDataProvider = DataProvider());
        using var provider = services.BuildServiceProvider();

        provider.GetRequiredService<ABSdk>().Should().BeAssignableTo<ABsmartly.Sdk>();
        provider.GetRequiredService<ABsmartly.Sdk>().Should().NotBeNull();
        provider.GetRequiredService<IABSdkHttpClientFactory>().Should().BeOfType<ABSdkHttpClientFactory>();
    }

    [TestCase(typeof(ABSdk), "ABsmartly.Sdk")]
    [TestCase(typeof(ABSdkConfig), "ABsmartlyConfig")]
    [TestCase(typeof(ABSdkHttpClientFactory), "ABsmartlyHttpClientFactory")]
    [TestCase(typeof(IABSdkHttpClientFactory), "IABsmartlyHttpClientFactory")]
    [TestCase(typeof(IABSdkHttpClient), "IABsmartlyHttpClient")]
    public void Release130TypesAreObsoleteAndNameTheirReplacement(Type alias, string replacement)
    {
        alias.Namespace.Should().StartWith("ABSmartly");
        var obsolete = alias.GetCustomAttribute<ObsoleteAttribute>();
        obsolete.Should().NotBeNull();
        obsolete!.IsError.Should().BeFalse();
        obsolete.Message.Should().Contain(replacement);
    }

    [Test]
    public void AddABSmartlyIsObsoleteAndNamesAddABsmartly()
    {
        var method = typeof(ABSmartly.DependencyInjection.ServiceCollectionExtensions)
            .GetMethod(nameof(ABSmartly.DependencyInjection.ServiceCollectionExtensions.AddABSmartly))!;
        method.GetCustomAttribute<ObsoleteAttribute>()!.Message.Should().Contain("AddABsmartly");
    }

    // ABSmartly.ABsmartly only existed during 1.4.0 development (never released), so it is
    // replaced outright by ABsmartly.Sdk with no alias.
    [Test]
    public void EntryPointIsABsmartlySdkWithNoUnreleasedAlias()
    {
        typeof(ABsmartly.Sdk).FullName.Should().Be("ABsmartly.Sdk");
        typeof(ABsmartly.Sdk).GetCustomAttribute<ObsoleteAttribute>().Should().BeNull();
        typeof(ABsmartly.Sdk).Assembly.GetType("ABSmartly.ABsmartly").Should().BeNull();
    }
}
