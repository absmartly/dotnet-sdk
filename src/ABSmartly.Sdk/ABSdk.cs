using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ABSmartly;

[Obsolete("ABSdk has been renamed to ABsmartly. Please use ABsmartly instead.")]
public class ABSdk : ABsmartly
{
    [ActivatorUtilitiesConstructor]
    public ABSdk(IABsmartlyHttpClientFactory httpClientFactory,
        IOptions<ABSmartlyServiceConfiguration> serviceConfiguration,
        IOptions<ABsmartlyConfig> configOptions,
        ILoggerFactory loggerFactory)
        : base(httpClientFactory, serviceConfiguration, configOptions, loggerFactory)
    {
    }

    public ABSdk(IABsmartlyHttpClientFactory httpClientFactory,
        ABSmartlyServiceConfiguration serviceConfiguration,
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
    public Absmartly(IABsmartlyHttpClientFactory httpClientFactory,
        IOptions<ABSmartlyServiceConfiguration> serviceConfiguration,
        IOptions<ABsmartlyConfig> configOptions,
        ILoggerFactory loggerFactory)
        : base(httpClientFactory, serviceConfiguration, configOptions, loggerFactory)
    {
    }

    public Absmartly(IABsmartlyHttpClientFactory httpClientFactory,
        ABSmartlyServiceConfiguration serviceConfiguration,
        ABsmartlyConfig config = null,
        ILoggerFactory loggerFactory = null)
        : base(httpClientFactory, serviceConfiguration, config, loggerFactory)
    {
    }
}
