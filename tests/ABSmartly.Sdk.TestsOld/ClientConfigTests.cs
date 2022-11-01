﻿using ABSmartlySdk;
using ABSmartlySdk.DefaultServiceImplementations;

namespace ABSmartly.DotNet.Standard.UnitTests;

public class ClientConfigTests
{
    [Test]
    public void EmptyConstructor_Initializes_DefaultValuesAndTypes()
    {
        var config = new ClientConfig(new ABSmartlyServiceConfiguration());

        Assert.That(config.Endpoint, Is.EqualTo(string.Empty));
        Assert.That(config.ApiKey, Is.EqualTo(string.Empty));
        Assert.That(config.Environment, Is.EqualTo(string.Empty));
        Assert.That(config.Application, Is.EqualTo(string.Empty));

        Assert.That(config.DataDeserializer.GetType(), Is.EqualTo(typeof(DefaultContextDataDeserializer)));
        Assert.That(config.EventSerializer.GetType(), Is.EqualTo(typeof(DefaultContextEventSerializer)));
        Assert.That(config.Executor.GetType(), Is.EqualTo(typeof(DefaultExecutor)));
    }

    [TestCase("", "https://test.endpoint.com", "testApiKey", "testEnvironment", "testApplication")]
    [TestCase("prefix_", "https://test.endpoint.com", "testApiKey", "testEnvironment", "testApplication")]
    [TestCase("prefix_", "", "", "", "")]
    public void Constructor_Initializes_SetValues(string prefix, string endpoint, string apiKey, string environment, string application)
    {
        var clientConfiguration = new ABSmartlyServiceConfiguration
        {
            Prefix = prefix,
            Environment = environment,
            Application = application,
            Endpoint = endpoint,
            ApiKey = apiKey
        };

        var config = new ClientConfig(
            clientConfiguration
            );

        var endpointPrefix = string.IsNullOrWhiteSpace(endpoint) ? "" : prefix;
        var apiKeyPrefix = string.IsNullOrWhiteSpace(apiKey) ? "" : prefix;
        var environmentPrefix = string.IsNullOrWhiteSpace(environment) ? "" : prefix;
        var applicationPrefix = string.IsNullOrWhiteSpace(application) ? "" : prefix;

        Assert.That(config.Endpoint, Is.EqualTo(endpointPrefix + endpoint));
        Assert.That(config.ApiKey, Is.EqualTo(apiKeyPrefix + apiKey));
        Assert.That(config.Environment, Is.EqualTo(environmentPrefix + environment));
        Assert.That(config.Application, Is.EqualTo(applicationPrefix + application));
    }
}