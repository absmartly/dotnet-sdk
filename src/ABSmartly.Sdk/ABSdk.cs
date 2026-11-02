using System;
using ABSmartly.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ABSmartly;

/// <summary>
/// Obsolete: Use <see cref="ABsmartly"/> instead.
/// </summary>
/// <remarks>
/// Keeps the exact 1.3.0 constructor set so existing source and compiled callers keep working.
/// It deliberately has no <see cref="IABsmartlyHttpClientFactory"/> overloads: those would make
/// calls passing an <see cref="ABSdkHttpClientFactory"/> (which implements both factory interfaces)
/// ambiguous. New code should construct <see cref="ABsmartly"/>.
/// </remarks>
[Obsolete("ABSdk has been renamed to ABsmartly. Please use ABsmartly instead.")]
public class ABSdk : ABsmartly
{
    // Redeclared so the 1.3.0 field ABSdk.HttpClientName still exists on this type.
    public new const string HttpClientName = ABsmartly.HttpClientName;

    [ActivatorUtilitiesConstructor]
    public ABSdk(IABSdkHttpClientFactory httpClientFactory,
        IOptions<ABSmartlyServiceConfiguration> serviceConfiguration,
        IOptions<ABSdkConfig> configOptions,
        ILoggerFactory loggerFactory)
        : base(LegacyHttpClientFactoryAdapter.Wrap(httpClientFactory), serviceConfiguration,
            Options.Create<ABsmartlyConfig>(configOptions?.Value), loggerFactory)
    {
    }

    public ABSdk(IABSdkHttpClientFactory httpClientFactory,
        ABSmartlyServiceConfiguration serviceConfiguration,
        ABSdkConfig config = null,
        ILoggerFactory loggerFactory = null)
        : base(LegacyHttpClientFactoryAdapter.Wrap(httpClientFactory), serviceConfiguration, config, loggerFactory)
    {
    }

    // config has no default here, so new ABSdk(factory, serviceConfig) keeps binding to the
    // 1.3.0 overload above instead of becoming ambiguous.
    public ABSdk(IABSdkHttpClientFactory httpClientFactory,
        ABSmartlyServiceConfiguration serviceConfiguration,
        ABsmartlyConfig config,
        ILoggerFactory loggerFactory = null)
        : base(LegacyHttpClientFactoryAdapter.Wrap(httpClientFactory), serviceConfiguration, config, loggerFactory)
    {
    }
}
