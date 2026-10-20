#pragma warning disable CS0618
using System;
using System.Collections.Generic;
using System.Linq;
using ABSmartly.DependencyInjection;
using ABSmartly.Services;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
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

    private static ServiceProvider BuildProvider(Action<IServiceCollection> extra, Action<ABSdkConfig> typedSetup)
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
        extra?.Invoke(services);
        services.AddABSmartly(configuration, HttpClientConfig.CreateDefault(), typedSetup);
        return services.BuildServiceProvider();
    }

    [Test]
    public void TypedLegacySetupCallback_CompilesAndReachesSdk()
    {
        var provider = Moq.Mock.Of<IContextDataProvider>();
        Action<ABSdkConfig> setup = c => c.ContextDataProvider = provider;
        using var sp = BuildProvider(null, setup);

        sp.GetRequiredService<IOptions<ABsmartlyConfig>>().Value.ContextDataProvider.Should().BeSameAs(provider);
        sp.GetRequiredService<IOptions<ABSdkConfig>>().Value.ContextDataProvider.Should().BeSameAs(provider);
        sp.GetRequiredService<ABSdk>().Should().NotBeNull();
    }

    [Test]
    public void BaseTypedSetupCallback_StillCompiles()
    {
        var provider = Moq.Mock.Of<IContextDataProvider>();
        Action<ABsmartlyConfig> setup = c => c.ContextDataProvider = provider;
        using var sp = BuildProvider(null, setup);

        sp.GetRequiredService<IOptions<ABsmartlyConfig>>().Value.ContextDataProvider.Should().BeSameAs(provider);
    }

    [Test]
    public void LegacyConfigureOptions_ReachResolvedABSdk()
    {
        var provider = Moq.Mock.Of<IContextDataProvider>();
        using var sp = BuildProvider(s => s.Configure<ABSdkConfig>(c => c.ContextDataProvider = provider), null);

        sp.GetRequiredService<IOptions<ABSdkConfig>>().Value.ContextDataProvider.Should().BeSameAs(provider);
        var sdk = sp.GetRequiredService<ABSdk>();
        var field = typeof(ABsmartly).GetFields(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
            .FirstOrDefault(f => f.FieldType == typeof(IContextDataProvider));
        field.Should().NotBeNull();
        field!.GetValue(sdk).Should().BeSameAs(provider);
    }
}
