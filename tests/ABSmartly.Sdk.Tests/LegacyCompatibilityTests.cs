#pragma warning disable CS0618
using System.Collections.Generic;
using ABSmartly.DependencyInjection;
using ABSmartly.Services;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;

namespace ABSmartly.Sdk.Tests;

public class LegacyCompatibilityTests
{
    [Test]
    public void AddABSmartly_ResolvesLegacyTypes()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string>
        {
            ["Endpoint"] = "https://example.absmartly.io/v1",
            ["ApiKey"] = "key",
            ["Application"] = "app",
            ["Environment"] = "env"
        }).Build();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddABSmartly(configuration, HttpClientConfig.CreateDefault());
        using var provider = services.BuildServiceProvider();

        provider.GetRequiredService<ABsmartly>().Should().NotBeNull();
        provider.GetRequiredService<ABSdk>().Should().NotBeNull();
        provider.GetRequiredService<Absmartly>().Should().NotBeNull();
        provider.GetRequiredService<IABSdkHttpClientFactory>().Should().BeOfType<ABSdkHttpClientFactory>();
    }

    [Test]
    public void LegacyFactory_AssignableToLegacyInterface()
    {
        IABSdkHttpClientFactory factory = new ABSdkHttpClientFactory(null!);
        factory.Should().NotBeNull();
    }
}
