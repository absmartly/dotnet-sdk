# A/B Smartly DotNet SDK

Latest stable: [![NuGet stable](https://img.shields.io/nuget/v/ABSmartly.Sdk?style=flat-square)](https://www.nuget.org/packages/ABSmartly.Sdk)

Current prerelease: [![NuGet pre](https://img.shields.io/nuget/vpre/ABSmartly.Sdk?style=flat-square)](https://www.nuget.org/packages/ABSmartly.Sdk)

## Installation

Install the A/B Smartly DotNet SDK from Nuget
```shell
dotnet add package ABSmartly.Sdk --version 1.0.0
```

SDK targets .NET Standard 2.0 and .NET 5.

## Getting Started

#### Initialization
Following examples assume an Api Key, an Application, and an Environment have been created in the A/B Smartly web console.

Given that project uses .NET dependency injection, the default setup for the SDK can be used:

Startup code:
```csharp
using ABSmartly;
using ABSmartly.DependencyInjection;

var builder = WebApplication.CreateBuilder(args);

...

builder.Services.AddABSmartly(builder.Configuration.GetSection("ABSmartly"), HttpClientConfig.CreateDefault());

...
```

appsettings.json:
```json
{
  "ABSmartly": {
    "Environment": "development",
    "Application": "website",
    "Endpoint": "https://your-company.absmartly.io/v1",
    "ApiKey": "YOUR-API-KEY"
  }
}
```

ABSdk instance is added as a singleton then, and can be injected at usage places:
```csharp
using ABSmartly;
using Microsoft.AspNetCore.Mvc;

[ApiController]
[Route("[controller]")]
public class Test : ControllerBase
{
    private readonly ABSdk _abSdk;

    public Test(ABSdk abSdk)
    {
        _abSdk = abSdk;
    }
}
```

AddABSmartly extension method allows to configure SDK settings, HTTP connection settings, inject custom 
implementation if context-specific services, and also configure additional HTTP requests policies using Polly.  



Alternatively, SDK instance can be created manually like in the following example. 
```csharp
using ABSmartly;
using ABSmartly.Services;
using Microsoft.Extensions.DependencyInjection;

var serviceProvider = new ServiceCollection().AddHttpClient().BuildServiceProvider();
var httpClientFactory = serviceProvider.GetService<IHttpClientFactory>();

var abSdk = new ABSdk(new ABSdkHttpClientFactory(httpClientFactory), new ABSmartlyServiceConfiguration
{
    Environment = "development",
    Application = "website",
    Endpoint = "https://your-company.absmartly.io/v1",
    ApiKey = "YOUR-API-KEY"
});

...
```

SDK uses IHttpClientFactory abstraction to effectively manage HTTP connections pool. This factory is 
injected using IABSdkHttpClientFactory as seen in the example above.
In case custom behavior or implementation is required, inject own implementation of either 
IHttpClientFactory or IABSdkHttpClientFactory.  
In case of injecting IABSdkHttpClientFactory implementation make sure it creates named IHttpClient 
instances with name `ABSmartlySDK.HttpClient` (you can use static field in ABSdk class, `ABSdk.HttpClientName`).


#### Creating a new Context synchronously
```csharp
    // define a new context request
    var config = new ContextConfig()
        .SetUnit("session_id", "5ebf06d8cb5d8137290c4abb64155584fbdb64d8");
    var context = await _abSdk.CreateContextAsync(config);
```

#### Creating a new Context asynchronously
```csharp
    // define a new context request
    var config = new ContextConfig()
        .SetUnit("session_id", "5ebf06d8cb5d8137290c4abb64155584fbdb64d8");
    var context = _abSdk.CreateContext(config);
```

#### Creating a new Context with pre-fetched data
Creating a context involves a round-trip to the A/B Smartly event collector.
We can avoid repeating the round-trip on the client-side by re-using data previously retrieved.

```csharp
    var config = new ContextConfig()
        .SetUnit("session_id", "5ebf06d8cb5d8137290c4abb64155584fbdb64d8");
    var context = await _abSdk.CreateContextAsync(config);
    
    var anotherContextConfig = new ContextConfig()
        .SetUnit("session_id", "5ebf06d8cb5d8137290c4abb64155584fbdb64d9"); // a unique id identifying the other user
    
    var anotherContext = _abSdk.CreateContextWith(anotherContextConfig, context.GetContextData());
```

#### Setting extra units for a context
You can add additional units to a context by calling the `SetUnit()` or the `SetUnits()` method.
This method may be used for example, when a user logs in to your application, and you want to use the new unit type to the context.
Please note that **you cannot override an already set unit type** as that would be a change of identity, and will throw an exception. In this case, you must create a new context instead.
The `SetUnit()` and `SetUnits()` methods can be called before the context is ready.

```csharp
    context.SetUnit("db_user_id", "1000013");
    
    context.SetUnits(new Dictionary<string, string>() {
        { "db_user_id", "1000013" }
    });
```

#### Setting context attributes
The `SetAttribute()` and `SetAttributes()` methods can be called before the context is ready.
```csharp
    context.SetAttribute('user_agent', Request.Headers["User-Agent"]);
    
    context.SetAttributes(new Dictionary<string, object>() {
        { "customer_age", "new_customer" }
    });
```

#### Selecting a treatment
```csharp
    if (context.GetTreament("exp_test_experiment") == 0) {
        // user is in control group (variant 0)
    } else {
        // user is in treatment group
    }
```

#### Selecting a treatment variable
```csharp
    var variable = context.GetVariableValue("my_variable");
```

#### Tracking a goal achievement
Goals are created in the A/B Smartly web console.
```csharp
    context.Track("payment", new Dictionary<string, object>() {
        { "item_count", 1 },
        { "total_amount", 1999.99}
    });
```

#### Publishing pending data
Sometimes it is necessary to ensure all events have been published to the A/B Smartly collector, before proceeding.
You can explicitly call the `Publish()` or `PublishAsync()` methods.
```csharp
    context.Publish();
```

#### Disposing
Context implements IDisposable and IAsyncDisposable interfaces to ensure all events have been published to the A/B 
Smartly collector, like `Publish()`, and will also "seal" the context, throwing an error if any method that could 
generate an event is called.

Instead calling `Publish()` directly, `using` pattern can be used. 
```csharp
    using var context = _abSdk.CreateContext(config);
```

#### Refreshing the context with fresh experiment data
Sometimes for long-running contexts, the context is usually created once when the application is first started.
However, any experiments being tracked in your production code, but started after the context was created, will 
not be triggered.
To mitigate this, we can use the `RefreshInterval` property on the context config.

```csharp
    var config = new ContextConfig()
        .SetUnit("session_id", "5ebf06d8cb5d8137290c4abb64155584fbdb64d8");
    config.RefreshInterval = TimeSpan.FromHours(4); // every 4 hours
```

Alternatively, the `Refresh()` method can be called manually.
The `Refresh()` method pulls updated experiment data from the A/B Smartly collector and will trigger recently 
started experiments when `GetTreatment()` is called again.
```csharp
    context.Refresh();
    // or
    await context.RefreshAsync();
```

#### Using a custom Event Logger
The A/B Smartly SDK can be instantiated with an event logger used for all contexts.
In addition, an event logger can be specified when creating a particular context, in the `ContextConfig`.
```csharp
    // example implementation
    public class CustomEventLogger : IContextEventLogger
    {
        public void HandleEvent(Context context, EventType eventType, object data)
        {
            switch (eventType)
            {
                case EventType.Exposure when data is Exposure exposure:
                    Console.WriteLine($"exposed to experiment: {exposure.Name}");
                    break;
                case EventType.Goal when data is GoalAchievement goal:
                    Console.WriteLine($"goal tracked: {goal.Name}");
                    break;
                case EventType.Error:
                    Console.WriteLine($"error: {data}");
                    break;
                case EventType.Close:
                case EventType.Publish:
                case EventType.Ready:
                case EventType.Refresh:
                    break;
            }
        }
    }
```

```csharp
    // for all contexts, during SDK initialization
    // when using dependency injection
    
    builder.Services.AddABSmartly(
        builder.Configuration.GetSection("ABSmartly"), 
        HttpClientConfig.CreateDefault(), 
        config => config.ContextEventLogger = new CustomEventLogger());
    
    // or when creating SDK instance manually
    var abSdk = new ABSdk(
        new ABSdkHttpClientFactory(...), 
        new ABSmartlyServiceConfiguration {... }, 
        new ABSdkConfig{ContextEventLogger = new CustomEventLogger()});
    
    
    // OR, alternatively, during a particular context initialization
    var contextConfig = new ContextConfig() {
        ContextEventLogger = new CustomEventLogger()
    };
```

The data parameter depends on the type of event.
Currently, the SDK logs the following events:

| event | when                                                            | data                                                   |
|:---: |-----------------------------------------------------------------|--------------------------------------------------------|
| `Error` | `Context` receives an error                                     | `Exception` object                                     |
| `Ready` | `Context` turns ready                                           | `ContextData` used to initialize the context           |
| `Refresh` | `Context.Refresh()` method succeeds                             | `ContextData` used to refresh the context              |
| `Publish` | `Context.Publish()` pr `Context.PublishAsync()` method succeeds | `PublishEvent` sent to the A/B Smartly event collector |
| `Exposure` | `Context.GetTreatment()` method succeeds on first exposure      | `Exposure` enqueued for publishing                     |
| `Goal` | `Context.Track()` method succeeds                               | `GoalAchievement` enqueued for publishing              |
| `Close` | `Context` disposal succeeds               | `null`                                                 |


#### Peek at treatment variants
Although generally not recommended, it is sometimes necessary to peek at a treatment or variable without triggering 
an exposure.
The A/B Smartly SDK provides a `PeekTreatment()` method for that.

```csharp
    if (context.PeekTreatment("exp_test_experiment") == 0) {
        // user is in control group (variant 0)
    } else {
        // user is in treatment group
    }
```

##### Peeking at variables
```csharp
    var variable = context.PeekVariableValue("my_variable");
```

#### Overriding treatment variants
During development, for example, it is useful to force a treatment for an experiment. This can be achieved with 
the `SetOverride()` and/or `SetOverrides()` methods.
The `SetOverride()` and `SetOverrides()` methods can be called before the context is ready.
```csharp
    context.SetOverride("exp_test_experiment", 1); // force variant 1 of treatment
    context.SetOverrides(new Dictionary<string, int>() {
        { "exp_test_experiment", 1 },
        { "exp_another_experiment", 0 }
    });
```

## About A/B Smartly
**A/B Smartly** is the leading provider of state-of-the-art, on-premises, full-stack experimentation platforms for engineering and product teams that want to confidently deploy features as fast as they can develop them.
A/B Smartly's real-time analytics helps engineering and product teams ensure that new features will improve the customer experience without breaking or degrading performance and/or business metrics.

### Have a look at our growing list of clients and SDKs:
- [Java SDK](https://www.github.com/absmartly/java-sdk)
- [JavaScript SDK](https://www.github.com/absmartly/javascript-sdk)
- [PHP SDK](https://www.github.com/absmartly/php-sdk)
- [Swift SDK](https://www.github.com/absmartly/swift-sdk)
- [Vue2 SDK](https://www.github.com/absmartly/vue2-sdk)
