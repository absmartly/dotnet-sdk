using System;
using ABSmartly.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ABSmartly;

[Obsolete("ABSdk has been renamed to ABsmartly. Please use ABsmartly instead.")]
public class ABSdk : ABsmartly
{
    [ActivatorUtilitiesConstructor]
    public ABSdk(IABSdkHttpClientFactory httpClientFactory,
        IOptions<ABsmartlyServiceConfiguration> serviceConfiguration,
        IOptions<ABSdkConfig> configOptions,
        ILoggerFactory loggerFactory)
        : base(LegacyHttpClientFactoryAdapter.Wrap(httpClientFactory), serviceConfiguration,
            Options.Create<ABsmartlyConfig>(configOptions?.Value), loggerFactory)
    {
    }

    public ABSdk(IABSdkHttpClientFactory httpClientFactory,
        ABsmartlyServiceConfiguration serviceConfiguration,
        ABsmartlyConfig config = null,
        ILoggerFactory loggerFactory = null)
        : base(LegacyHttpClientFactoryAdapter.Wrap(httpClientFactory), serviceConfiguration, config, loggerFactory)
    {
    }

    public ABSdk(ABSdkHttpClientFactory httpClientFactory,
        ABsmartlyServiceConfiguration serviceConfiguration,
        ABsmartlyConfig config = null,
        ILoggerFactory loggerFactory = null)
        : base(httpClientFactory, serviceConfiguration, config, loggerFactory)
    {
    }

    public ABSdk(IABsmartlyHttpClientFactory httpClientFactory,
        ABsmartlyServiceConfiguration serviceConfiguration,
        ABsmartlyConfig config = null,
        ILoggerFactory loggerFactory = null)
        : base(httpClientFactory, serviceConfiguration, config, loggerFactory)
    {
    }
}

[Obsolete("Absmartly has been renamed to ABsmartly. Please use ABsmartly instead.")]
public class Absmartly : ABsmartly
{
    [ActivatorUtilitiesConstructor]
    public Absmartly(IABSdkHttpClientFactory httpClientFactory,
        IOptions<ABsmartlyServiceConfiguration> serviceConfiguration,
        IOptions<ABsmartlyConfig> configOptions,
        ILoggerFactory loggerFactory)
        : base(LegacyHttpClientFactoryAdapter.Wrap(httpClientFactory), serviceConfiguration, configOptions, loggerFactory)
    {
    }

    public Absmartly(IABSdkHttpClientFactory httpClientFactory,
        ABsmartlyServiceConfiguration serviceConfiguration,
        ABsmartlyConfig config = null,
        ILoggerFactory loggerFactory = null)
        : base(LegacyHttpClientFactoryAdapter.Wrap(httpClientFactory), serviceConfiguration, config, loggerFactory)
    {
    }

    public Absmartly(ABSdkHttpClientFactory httpClientFactory,
        ABsmartlyServiceConfiguration serviceConfiguration,
        ABsmartlyConfig config = null,
        ILoggerFactory loggerFactory = null)
        : base(httpClientFactory, serviceConfiguration, config, loggerFactory)
    {
    }

    public Absmartly(IABsmartlyHttpClientFactory httpClientFactory,
        ABsmartlyServiceConfiguration serviceConfiguration,
        ABsmartlyConfig config = null,
        ILoggerFactory loggerFactory = null)
        : base(httpClientFactory, serviceConfiguration, config, loggerFactory)
    {
    }
}
