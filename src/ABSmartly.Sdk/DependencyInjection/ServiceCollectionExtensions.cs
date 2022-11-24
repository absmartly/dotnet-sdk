using System;
using System.Net.Http;
using System.Security.Authentication;
using ABSmartly.Services;
using ABSmartly.Services.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
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
        services.Configure(setupSdkOptions ?? (_ => { }));

        var builder = services.AddHttpClient(ABSdk.HttpClientName);

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

        services.AddTransient<IABSdkHttpClientFactory, ABSdkHttpClientFactory>();
        services.AddTransient<IJsonOptionsProvider, JsonOptionsProvider>();

        services.AddSingleton<ABSdk>();
    }

    private static Func<PolicyBuilder<HttpResponseMessage>, AsyncRetryPolicy<HttpResponseMessage>>
        ConfigureDefaultPolicy(HttpClientConfig config)
    {
        return policyBuilder =>
            policyBuilder.WaitAndRetryAsync(config.MaxRetries, _ => TimeSpan.FromMilliseconds(config.RetryInterval));
    }
}