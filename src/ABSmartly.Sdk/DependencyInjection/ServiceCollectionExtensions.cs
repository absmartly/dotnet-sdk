using System;
using System.Net.Http;
using System.Security.Authentication;
using ABSmartly.Services;
using ABSmartly.Services.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Polly;
using Polly.Retry;
#if NETCOREAPP2_1_OR_GREATER || NETCOREAPP || NET
using System.Net.Security;
#endif

namespace ABSmartly.DependencyInjection;

// ReSharper disable once UnusedType.Global
public static class ServiceCollectionExtensions
{
    public static void AddABSmartly(
        this IServiceCollection services,
        IConfiguration abSmartlyServiceConfiguration,
        HttpClientConfig httpClientConfig,
        Action<ABSdkConfig> setupSdkOptions = null,
        Func<PolicyBuilder<HttpResponseMessage>, IAsyncPolicy<HttpResponseMessage>> setupRetryPolicy = null
    )
    {
        if (abSmartlyServiceConfiguration == null)
            throw new ArgumentNullException(nameof(abSmartlyServiceConfiguration));
        if (httpClientConfig == null) throw new ArgumentNullException(nameof(httpClientConfig));

        services.Configure<ABSmartlyServiceConfiguration>(abSmartlyServiceConfiguration);
#pragma warning disable CS0618
        // The setup callback is a plain Configure<ABSdkConfig>, so it composes with existing
        // Configure<ABSdkConfig> registrations in registration order. The resulting legacy values
        // are then propagated to ABsmartlyConfig so the current SDK type sees them as well.
        if (setupSdkOptions != null)
        {
            services.Configure(setupSdkOptions);
        }

        services.AddOptions<ABsmartlyConfig>()
            .PostConfigure<IOptions<ABSdkConfig>>((config, legacy) => config.CopyNonNullFrom(legacy.Value));
#pragma warning restore CS0618

        var builder = services.AddHttpClient(ABsmartly.HttpClientName);

#if NETCOREAPP2_1_OR_GREATER || NETCOREAPP || NET
        builder = builder
            .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
            {
                UseCookies = false,
                SslOptions = new SslClientAuthenticationOptions
                {
                    EnabledSslProtocols = SslProtocols.Tls12 | SslProtocols.Tls13
                },
                MaxConnectionsPerServer = 20,
                PooledConnectionIdleTimeout = TimeSpan.FromMinutes(2),
                PooledConnectionLifetime = TimeSpan.FromMinutes(5),

                ConnectTimeout = TimeSpan.FromMilliseconds(httpClientConfig.ConnectTimeout),
                KeepAlivePingTimeout = TimeSpan.FromMilliseconds(httpClientConfig.ConnectionKeepAlive)
            });

#elif NETSTANDARD2_0_OR_GREATER
        builder = builder
            .SetHandlerLifetime(TimeSpan.FromMinutes(2))
            .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
            {
                UseCookies = false,
                SslProtocols = SslProtocols.Tls12,
                MaxConnectionsPerServer = 20
            });

#endif

        builder.AddTransientHttpErrorPolicy(setupRetryPolicy ?? ConfigureDefaultPolicy(httpClientConfig));

        services.AddTransient<IABsmartlyHttpClientFactory, ABsmartlyHttpClientFactory>();
        services.AddTransient<IJsonOptionsProvider, JsonOptionsProvider>();

        services.AddSingleton<ABsmartly>();

        // Legacy registrations so existing consumers of the obsolete aliases keep resolving.
#pragma warning disable CS0618
        services.AddTransient<IABSdkHttpClientFactory, ABSdkHttpClientFactory>();
        services.AddSingleton<ABSdk>();
        services.AddSingleton<Absmartly>();
#pragma warning restore CS0618
    }

    private static Func<PolicyBuilder<HttpResponseMessage>, AsyncRetryPolicy<HttpResponseMessage>>
        ConfigureDefaultPolicy(HttpClientConfig config)
    {
        return policyBuilder =>
            policyBuilder.WaitAndRetryAsync(config.MaxRetries, _ => TimeSpan.FromMilliseconds(config.RetryInterval));
    }
}