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

    [Test]
    public void CustomLegacyFactoryRegistration_IsUsedByABSdk()
    {
        var custom = Moq.Mock.Of<IABSdkHttpClientFactory>();
        using var sp = BuildProvider(s => s.AddSingleton(custom), null);
        // AddABSmartly registers its own legacy factory too; a later registration wins.
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddABSmartly(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string>
        {
            ["Endpoint"] = "https://example.absmartly.io/v1", ["ApiKey"] = "k", ["Application"] = "a", ["Environment"] = "e"
        }).Build(), HttpClientConfig.CreateDefault());
        services.AddSingleton(custom);
        using var provider = services.BuildServiceProvider();

        var sdk = provider.GetRequiredService<ABSdk>();
        var field = typeof(ABsmartly).GetField("_httpClientFactory", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        field!.GetValue(sdk).Should().BeSameAs(custom);
    }

    [Test]
    public void LegacyConfigureAndSetupCallback_ComposeInRegistrationOrder()
    {
        var providerA = Moq.Mock.Of<IContextDataProvider>();
        var providerB = Moq.Mock.Of<IContextDataProvider>();
        bool callbackSawA = false;

        var services = new ServiceCollection();
        services.AddLogging();
        services.Configure<ABSdkConfig>(c => c.ContextDataProvider = providerA);
        services.AddABSmartly(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string>
        {
            ["Endpoint"] = "https://example.absmartly.io/v1", ["ApiKey"] = "k", ["Application"] = "a", ["Environment"] = "e"
        }).Build(), HttpClientConfig.CreateDefault(), (Action<ABSdkConfig>)(c =>
        {
            callbackSawA = ReferenceEquals(c.ContextDataProvider, providerA);
            c.ContextDataProvider ??= providerB;
        }));
        using var sp = services.BuildServiceProvider();

        sp.GetRequiredService<IOptions<ABSdkConfig>>().Value.ContextDataProvider.Should().BeSameAs(providerA);
        callbackSawA.Should().BeTrue();
        var sdk = sp.GetRequiredService<ABSdk>();
        var field = typeof(ABsmartly).GetField("_contextDataProvider", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        field!.GetValue(sdk).Should().BeSameAs(providerA);
    }
}
