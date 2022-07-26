﻿// See https://aka.ms/new-console-template for more information

using ABSmartly;
using ABSmartly.Services;
using Microsoft.Extensions.DependencyInjection;

var serviceProvider = new ServiceCollection().AddHttpClient().BuildServiceProvider();
var httpClientFactory = serviceProvider.GetService<IHttpClientFactory>();

var abSdk = new ABSdk(new ABSdkHttpClientFactory(httpClientFactory), new ABSmartlyServiceConfiguration
{
    Environment = "prod",
    Application = "www",
    Endpoint = "https://acme.absmartly.io/v1",
    ApiKey = "YOUR_API_KEY"
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