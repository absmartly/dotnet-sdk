using System;
using ABSmartlySdk.DefaultServiceImplementations;
using Microsoft.Extensions.DependencyInjection;

namespace ABSmartlySdk.Utils.Extensions;

public static class ServiceCollectionExtensions
{
    public static void AddABSmartly(this IServiceCollection services, Action<ABSmartlyConfig> options = null, ServiceLifetime? lifeTime = null)
    {
        services.AddHttpClient<ABSmartly>(DefaultHttpClient.ABSmartyHttpClientName, o =>
        {
            // Todo: add a base address??
            //o.BaseAddress = new Uri("");
        });

        if (lifeTime is null)
            lifeTime = ServiceLifetime.Singleton;

        services.Configure(options);

        if (lifeTime is ServiceLifetime.Singleton)
            services.AddSingleton<ABSmartly>();

        else if (lifeTime is ServiceLifetime.Scoped)
            services.AddScoped<ABSmartly>();

        else if (lifeTime is ServiceLifetime.Transient)
            services.AddTransient<ABSmartly>();

        //var absmartly = ABSmartly.Create(null);
        //return absmartly;
    }
}