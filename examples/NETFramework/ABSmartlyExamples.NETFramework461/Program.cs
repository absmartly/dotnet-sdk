using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using ABSmartly;
using ABSmartly.Services;
using Microsoft.Extensions.DependencyInjection;

namespace ABSmartlyExamples.NETFramework461
{
    internal class Program
    {
        public static async Task Main(string[] args)
        {
            var serviceProvider = new ServiceCollection().AddHttpClient().BuildServiceProvider();
            var httpClientFactory = serviceProvider.GetService<IHttpClientFactory>();

            var abSdk = new ABSdk(new ABSdkHttpClientFactory(httpClientFactory), new ABSmartlyServiceConfiguration
            {
                Environment = "prod",
                Application = "www",
                Endpoint = "https://demo.absmartly.io/v1",
                ApiKey = "x3ZyxmeKmb6n3VilTGs5I6-tBdaS9gYyr3i4YQXmUZcpPhH8nd8ev44NoEL_3yvA"
            });

            var config = new ContextConfig().SetUnit("user_id", "test_classic_dotnet_2");

            var context = await abSdk.CreateContextAsync(config);

            var treatment = context.GetTreatment("net_seasons");

            context.Track("booking", new Dictionary<string, object>
            {
                { "bookingTime", DateTime.Now },
                { "selectedTreatment", treatment }
            });

            await context.PublishAsync();
        }
    }
}