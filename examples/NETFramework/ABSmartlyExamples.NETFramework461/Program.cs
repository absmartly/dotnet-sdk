using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using ABSmartly;
using ABSmartly.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ABSmartlyExamples.NETFramework461
{
    internal class Program
    {
        public static async Task Main(string[] args)
        {
            var serviceProvider = new ServiceCollection().AddHttpClient().BuildServiceProvider();
            var httpClientFactory = serviceProvider.GetService<IHttpClientFactory>();

            var abSdk = new ABSdk(new ABSdkHttpClientFactory(httpClientFactory), new LoggerFactory(), new ABSdkConfig(),
                new ABSmartlyServiceConfiguration()
                {
                    Environment = "prod",
                    Application = "www",
                    Endpoint = "https://acme.absmartly.io/v1",
                    ApiKey = ""
                });

            var context = await abSdk.CreateContextAsync();
            context.SetUnit("user_id", "test_classic_dotnet_2");

            var treatment = await context.GetTreatmentAsync("net_seasons");
            
            await context.TrackAsync("booking", new Dictionary<string, object>
            {
                { "bookingTime", DateTime.Now },
                { "selectedTreatment", treatment },
            });

            await context.PublishAsync();
        }
    }
}