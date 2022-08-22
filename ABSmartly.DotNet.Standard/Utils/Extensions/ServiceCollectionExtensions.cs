using System;
using ABSmartlySdk.DefaultServiceImplementations;
using Microsoft.Extensions.DependencyInjection;

namespace ABSmartlySdk.Utils.Extensions;

public static class ServiceCollectionExtensions
{
    public static void AddABSmartly(this IServiceCollection services, Action<ABSmartlyConfig> options = null, ServiceLifetime? lifeTime = null)
    {
        Add(services, options, lifeTime);
    }

    private static void Add(IServiceCollection services, Action<ABSmartlyConfig> options = null, ServiceLifetime? lifeTime = null)
    {
        // Step 1: Http Client Registration
        services.AddHttpClient<ABSmartly>(DefaultHttpClient.ABSmartyHttpClientName, o =>
        {
            // Todo: add a base address??
            //o.BaseAddress = new Uri("");
        });

        // Step 2: Configure Options
        if (options is not null)
            services.Configure(options);

        // Step 3: Lifetime
        if (lifeTime is null)
            lifeTime = ServiceLifetime.Transient;

        if (lifeTime is ServiceLifetime.Singleton)
            services.AddSingleton<ABSmartly>();

        else if (lifeTime is ServiceLifetime.Scoped)
            services.AddScoped<ABSmartly>();

        else if (lifeTime is ServiceLifetime.Transient)
            services.AddTransient<ABSmartly>();
    }
}