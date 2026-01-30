# A/B Smartly .NET SDK

Latest
stable: [![NuGet stable](https://img.shields.io/nuget/v/ABSmartly.Sdk?style=flat-square)](https://www.nuget.org/packages/ABSmartly.Sdk)

Current
prerelease: [![NuGet pre](https://img.shields.io/nuget/vpre/ABSmartly.Sdk?style=flat-square)](https://www.nuget.org/packages/ABSmartly.Sdk)

A/B Smartly - .NET SDK

## Compatibility

The A/B Smartly .NET SDK targets .NET Standard 2.0 and .NET 5 and later.

It is compatible with:
- .NET 5.0+
- .NET Core 2.0+
- .NET Framework 4.6.1+

**Note on Naming:** The main SDK class has been renamed from `ABSdk` to `ABsmartly` (with uppercase AB and lowercase smartly) to standardize naming across all SDKs. The old class names (`ABSdk`, `ABSmartly`, `Absmartly`, `ABSdkConfig`, etc.) are still available as obsolete aliases for backwards compatibility, but new code should use the updated naming: `ABsmartly`.

## Installation

Install the A/B Smartly .NET SDK from NuGet:

```shell
dotnet add package ABSmartly.Sdk --version 1.0.0
```

## Getting Started

Please follow the [installation](#installation) instructions before trying the following code:

### Initialization

This example assumes an API Key, an Application, and an Environment have been created in the A/B Smartly web console.

#### Recommended: Dependency Injection Setup

If your project uses .NET dependency injection, use the default setup for the SDK:

**Startup code:**

```csharp
using ABSmartly;
using ABSmartly.DependencyInjection;

var builder = WebApplication.CreateBuilder(args);

...

builder.Services.AddABSmartly(
    builder.Configuration.GetSection("ABSmartly"),
    HttpClientConfig.CreateDefault());

...
```

**appsettings.json:**

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

The `ABsmartly` instance is added as a singleton and can be injected where needed:

```csharp
using ABSmartly;
using Microsoft.AspNetCore.Mvc;

[ApiController]
[Route("[controller]")]
public class Test : ControllerBase
{
    private readonly ABsmartly _absmartly;

    public Test(ABsmartly absmartly)
    {
        _absmartly = absmartly;
    }
}
```

The `AddABSmartly` extension method allows you to configure SDK settings, HTTP connection settings, inject custom implementations of context-specific services, and configure additional HTTP request policies using Polly.

#### Advanced: Manual SDK Configuration

Alternatively, the SDK instance can be created manually:

```csharp
using ABSmartly;
using ABSmartly.Services;
using Microsoft.Extensions.DependencyInjection;

var serviceProvider = new ServiceCollection().AddHttpClient().BuildServiceProvider();
var httpClientFactory = serviceProvider.GetService<IHttpClientFactory>();

var absmartly = new ABsmartly(new ABsmartlyHttpClientFactory(httpClientFactory), new ABSmartlyServiceConfiguration
{
    Environment = "development",
    Application = "website",
    Endpoint = "https://your-company.absmartly.io/v1",
    ApiKey = "YOUR-API-KEY"
});

...
```

**SDK Options**

| Config              | Type                         | Required? | Default | Description                                                                                                                                                                   |
|:--------------------|:-----------------------------|:---------:|:-------:|:------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|
| Endpoint            | `string`                     | &#9989;   | `null`  | The URL to your API endpoint. Most commonly `"https://your-company.absmartly.io/v1"`                                                                                         |
| ApiKey              | `string`                     | &#9989;   | `null`  | Your API key which can be found on the Web Console.                                                                                                                           |
| Environment         | `string`                     | &#9989;   | `null`  | The environment of the platform where the SDK is installed. Environments are created on the Web Console and should match the available environments in your infrastructure.   |
| Application         | `string`                     | &#9989;   | `null`  | The name of the application where the SDK is installed. Applications are created on the Web Console and should match the applications where your experiments will be running. |
| ContextEventLogger  | `IContextEventLogger`        | &#10060;  | `null`  | Callback to handle SDK events (ready, exposure, goal, etc.)                                                                                                                   |
| ContextDataProvider | `IContextDataProvider`       | &#10060;  | auto    | Custom provider for context data (advanced usage)                                                                                                                             |
| ContextEventHandler | `IContextEventHandler`       | &#10060;  | auto    | Custom handler for publishing events (advanced usage)                                                                                                                         |

**Note:** The SDK uses `IHttpClientFactory` abstraction to effectively manage HTTP connection pools. This factory is injected using `IABsmartlyHttpClientFactory` as seen in the example above. If custom behavior or implementation is required, inject your own implementation of either `IHttpClientFactory` or `IABsmartlyHttpClientFactory`.

When injecting `IABsmartlyHttpClientFactory`, ensure it creates instances of the `IABsmartlyHttpClient` interface, which is a wrapper on top of `HttpClient`. The SDK provides `ABSmartly.Services.ABsmartlyHttpClientFactory.HttpClientWrapper` for this purpose.

When injecting `IHttpClientFactory`, ensure it creates named `IHttpClient` instances with the name `ABSmartlySDK.HttpClient` (available as `ABsmartly.HttpClientName`).

### Creating a New Context

#### Asynchronously (Recommended)

```csharp
// define a new context request
var config = new ContextConfig()
    .SetUnit("session_id", "5ebf06d8cb5d8137290c4abb64155584fbdb64d8");

var context = await _absmartly.CreateContextAsync(config);
```

#### Synchronously

```csharp
// define a new context request
var config = new ContextConfig()
    .SetUnit("session_id", "5ebf06d8cb5d8137290c4abb64155584fbdb64d8");

var context = _absmartly.CreateContext(config);
```

#### With Pre-fetched Data

When doing full-stack experimentation with A/B Smartly, we recommend creating a context only once on the server-side. Creating a context involves a round-trip to the A/B Smartly event collector. We can avoid repeating the round-trip on the client-side by re-using data previously retrieved.

```csharp
var config = new ContextConfig()
    .SetUnit("session_id", "5ebf06d8cb5d8137290c4abb64155584fbdb64d8");
var context = await _absmartly.CreateContextAsync(config);

var anotherContextConfig = new ContextConfig()
    .SetUnit("session_id", "5ebf06d8cb5d8137290c4abb64155584fbdb64d9");

var anotherContext = _absmartly.CreateContextWith(anotherContextConfig, context.GetContextData());
```

### Setting Extra Units

You can add additional units to a context by calling the `SetUnit()` or `SetUnits()` method. This method may be used, for example, when a user logs in to your application and you want to add the new unit type to the context.

Please note that **you cannot override an already set unit type** as that would be a change of identity and will throw an exception. In this case, you must create a new context instead.

The `SetUnit()` and `SetUnits()` methods can be called before the context is ready.

```csharp
context.SetUnit("db_user_id", "1000013");

context.SetUnits(new Dictionary<string, string> {
    { "db_user_id", "1000013" }
});
```

## Basic Usage

### Context Attributes

The `SetAttribute()` and `SetAttributes()` methods can be called before the context is ready.

```csharp
context.SetAttribute("user_agent", Request.Headers["User-Agent"]);

context.SetAttributes(new Dictionary<string, object> {
    { "customer_age", "new_customer" }
});
```

### Selecting a Treatment

```csharp
if (context.GetTreatment("exp_test_experiment") == 0)
{
    // user is in control group (variant 0)
}
else
{
    // user is in treatment group
}
```

### Treatment Variables

```csharp
var defaultButtonColor = "red";
var buttonColor = context.GetVariableValue("button.color", defaultButtonColor);
```

### Tracking Goals

Goals are created in the A/B Smartly web console.

```csharp
context.Track("payment", new Dictionary<string, object> {
    { "item_count", 1 },
    { "total_amount", 1999.99 }
});
```

### Publishing Pending Data

Sometimes it is necessary to ensure all events have been published to the A/B Smartly collector before proceeding. You can explicitly call the `Publish()` or `PublishAsync()` methods.

```csharp
await context.PublishAsync();

// or synchronously
context.Publish();
```

### Finalizing (Disposing)

The `Context` implements `IDisposable` and `IAsyncDisposable` interfaces to ensure all events have been published to the A/B Smartly collector, like `Publish()`, and will also "seal" the context, throwing an error if any method that could generate an event is called.

Instead of calling `Publish()` directly, the `using` pattern can be used:

```csharp
using var context = _absmartly.CreateContext(config);

// or asynchronously
await using var context = await _absmartly.CreateContextAsync(config);
```

## Platform-Specific Examples

### Using with ASP.NET Core Minimal APIs

```csharp
using ABSmartly;
using ABSmartly.DependencyInjection;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddABSmartly(
    builder.Configuration.GetSection("ABSmartly"),
    HttpClientConfig.CreateDefault());

var app = builder.Build();

app.MapGet("/", async (ABsmartly absmartly, HttpContext httpContext) =>
{
    var sessionId = httpContext.Session.GetString("SessionId")
        ?? Guid.NewGuid().ToString();
    httpContext.Session.SetString("SessionId", sessionId);

    var config = new ContextConfig()
        .SetUnit("session_id", sessionId);

    var context = await absmartly.CreateContextAsync(config);

    var treatment = context.GetTreatment("exp_test_experiment");

    await context.DisposeAsync();

    return treatment == 0
        ? Results.Text("<h1>Control Group</h1>", "text/html")
        : Results.Text("<h1>Treatment Group</h1>", "text/html");
});

app.Run();
```

### Using with ASP.NET Core MVC

```csharp
// Startup.cs or Program.cs
using ABSmartly;
using ABSmartly.DependencyInjection;

public class Startup
{
    public void ConfigureServices(IServiceCollection services)
    {
        services.AddControllersWithViews();

        services.AddABSmartly(
            Configuration.GetSection("ABSmartly"),
            HttpClientConfig.CreateDefault());
    }
}

// Controllers/HomeController.cs
using Microsoft.AspNetCore.Mvc;
using ABSmartly;

public class HomeController : Controller
{
    private readonly ABsmartly _absmartly;

    public HomeController(ABsmartly absmartly)
    {
        _absmartly = absmartly;
    }

    public async Task<IActionResult> Index()
    {
        var sessionId = HttpContext.Session.GetString("SessionId")
            ?? Guid.NewGuid().ToString();
        HttpContext.Session.SetString("SessionId", sessionId);

        var config = new ContextConfig()
            .SetUnit("session_id", sessionId);

        await using var context = await _absmartly.CreateContextAsync(config);

        var treatment = context.GetTreatment("exp_test_experiment");

        ViewBag.Treatment = treatment;

        return treatment == 0 ? View("Control") : View("Treatment");
    }
}
```

### Using with ASP.NET Core Razor Pages

```csharp
// Pages/Index.cshtml.cs
using Microsoft.AspNetCore.Mvc.RazorPages;
using ABSmartly;

public class IndexModel : PageModel
{
    private readonly ABsmartly _absmartly;

    public int Treatment { get; set; }

    public IndexModel(ABsmartly absmartly)
    {
        _absmartly = absmartly;
    }

    public async Task OnGetAsync()
    {
        var sessionId = HttpContext.Session.GetString("SessionId")
            ?? Guid.NewGuid().ToString();
        HttpContext.Session.SetString("SessionId", sessionId);

        var config = new ContextConfig()
            .SetUnit("session_id", sessionId);

        await using var context = await _absmartly.CreateContextAsync(config);

        Treatment = context.GetTreatment("exp_test_experiment");
    }
}
```

### Using with Blazor Server

```csharp
// Program.cs
using ABSmartly;
using ABSmartly.DependencyInjection;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorPages();
builder.Services.AddServerSideBlazor();

builder.Services.AddABSmartly(
    builder.Configuration.GetSection("ABSmartly"),
    HttpClientConfig.CreateDefault());

// Pages/Index.razor
@page "/"
@inject ABsmartly ABsmartly
@inject NavigationManager NavigationManager

<h1>@(treatment == 0 ? "Control Group" : "Treatment Group")</h1>

@code {
    private int treatment = 0;

    protected override async Task OnInitializedAsync()
    {
        var config = new ContextConfig()
            .SetUnit("session_id", Guid.NewGuid().ToString());

        await using var context = await ABsmartly.CreateContextAsync(config);

        treatment = context.GetTreatment("exp_test_experiment");
    }
}
```

## Advanced Request Configuration

### HTTP Client Timeout Configuration

Configure global timeout settings via HttpClientConfig:

```csharp
using ABSmartly;
using ABSmartly.DependencyInjection;

var builder = WebApplication.CreateBuilder(args);

var httpConfig = HttpClientConfig.CreateDefault();
httpConfig.Timeout = TimeSpan.FromMilliseconds(1500);

builder.Services.AddABSmartly(
    builder.Configuration.GetSection("ABSmartly"),
    httpConfig);
```

### Task-Based Cancellation Pattern

For scenarios requiring cancellation (e.g., user navigation), use Task.WhenAny with timeout:

```csharp
using ABSmartly;
using System.Threading.Tasks;

var config = new ContextConfig()
    .SetUnit("session_id", "abc123");

var contextTask = _absmartly.CreateContextAsync(config);
var timeoutTask = Task.Delay(TimeSpan.FromMilliseconds(1500));

var completedTask = await Task.WhenAny(contextTask, timeoutTask);

if (completedTask == contextTask)
{
    var context = await contextTask;
    Console.WriteLine("Context ready!");
    await context.DisposeAsync();
}
else
{
    Console.WriteLine("Context creation timed out");
}
```

### Component Lifecycle Cancellation Pattern

For Blazor or other component-based scenarios with navigation:

```csharp
// In a Blazor component or controller
private Task<IContext> _contextTask;
private bool _isNavigating = false;

protected override async Task OnInitializedAsync()
{
    var config = new ContextConfig()
        .SetUnit("session_id", Guid.NewGuid().ToString());

    _contextTask = ABsmartly.CreateContextAsync(config);

    try
    {
        var context = await _contextTask;

        if (!_isNavigating)
        {
            treatment = context.GetTreatment("exp_test_experiment");
            await context.DisposeAsync();
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Error creating context: {ex.Message}");
    }
}

public void Dispose()
{
    _isNavigating = true;
}
```

## Advanced

### Refreshing the Context with Fresh Experiment Data

For long-running contexts, the context is usually created once when the application is first started. However, any experiments being tracked in your production code but started after the context was created will not be triggered.

To mitigate this, we can use the `RefreshInterval` property on the context config:

```csharp
var config = new ContextConfig()
    .SetUnit("session_id", "5ebf06d8cb5d8137290c4abb64155584fbdb64d8");
config.RefreshInterval = TimeSpan.FromHours(4); // every 4 hours
```

Alternatively, the `Refresh()` method can be called manually. The `Refresh()` method pulls updated experiment data from the A/B Smartly collector and will trigger recently started experiments when `GetTreatment()` is called again.

```csharp
await context.RefreshAsync();

// or synchronously
context.Refresh();
```

### Custom Event Logger

The A/B Smartly SDK can be instantiated with an event logger used for all contexts. In addition, an event logger can be specified when creating a particular context in the `ContextConfig`.

```csharp
// Example implementation
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

**Usage:**

```csharp
// For all contexts, during SDK initialization
// When using dependency injection
builder.Services.AddABSmartly(
    builder.Configuration.GetSection("ABSmartly"),
    HttpClientConfig.CreateDefault(),
    config => config.ContextEventLogger = new CustomEventLogger());

// Or when creating SDK instance manually
var absmartly = new ABsmartly(
    new ABsmartlyHttpClientFactory(...),
    new ABSmartlyServiceConfiguration { ... },
    new ABsmartlyConfig { ContextEventLogger = new CustomEventLogger() });

// OR, alternatively, during a particular context initialization
var contextConfig = new ContextConfig
{
    ContextEventLogger = new CustomEventLogger()
};
```

**Event Types**

The data parameter depends on the type of event. Currently, the SDK logs the following events:

| Event      | When                                                        | Data                                                   |
|:-----------|:------------------------------------------------------------|:-------------------------------------------------------|
| `Error`    | Context receives an error                                   | `Exception` object                                     |
| `Ready`    | Context turns ready                                         | `ContextData` used to initialize the context           |
| `Refresh`  | `Refresh()` method succeeds                                 | `ContextData` used to refresh the context              |
| `Publish`  | `Publish()` or `PublishAsync()` method succeeds             | `PublishEvent` sent to the A/B Smartly event collector |
| `Exposure` | `GetTreatment()` method succeeds on first exposure          | `Exposure` enqueued for publishing                     |
| `Goal`     | `Track()` method succeeds                                   | `GoalAchievement` enqueued for publishing              |
| `Close`    | Context disposal succeeds                                   | `null`                                                 |

### Peek at Treatment Variants

Although generally not recommended, it is sometimes necessary to peek at a treatment or variable without triggering an exposure. The A/B Smartly SDK provides a `PeekTreatment()` method for that.

```csharp
if (context.PeekTreatment("exp_test_experiment") == 0)
{
    // user is in control group (variant 0)
}
else
{
    // user is in treatment group
}
```

**Peeking at variables:**

```csharp
var variable = context.PeekVariableValue("my_variable", defaultValue);
```

### Overriding Treatment Variants

During development, for example, it is useful to force a treatment for an experiment. This can be achieved with the `SetOverride()` and/or `SetOverrides()` methods.

The `SetOverride()` and `SetOverrides()` methods can be called before the context is ready.

```csharp
context.SetOverride("exp_test_experiment", 1); // force variant 1 of treatment

context.SetOverrides(new Dictionary<string, int> {
    { "exp_test_experiment", 1 },
    { "exp_another_experiment", 0 }
});
```

## About A/B Smartly

**A/B Smartly** is the leading provider of state-of-the-art, on-premises, full-stack experimentation platforms for engineering and product teams that want to confidently deploy features as fast as they can develop them.
A/B Smartly's real-time analytics helps engineering and product teams ensure that new features will improve the customer experience without breaking or degrading performance and/or business metrics.

### Have a look at our growing list of clients and SDKs:
- [JavaScript SDK](https://www.github.com/absmartly/javascript-sdk)
- [Java SDK](https://www.github.com/absmartly/java-sdk)
- [PHP SDK](https://www.github.com/absmartly/php-sdk)
- [Swift SDK](https://www.github.com/absmartly/swift-sdk)
- [Vue2 SDK](https://www.github.com/absmartly/vue2-sdk)
- [Vue3 SDK](https://www.github.com/absmartly/vue3-sdk)
- [React SDK](https://www.github.com/absmartly/react-sdk)
- [Python3 SDK](https://www.github.com/absmartly/python3-sdk)
- [Go SDK](https://www.github.com/absmartly/go-sdk)
- [Ruby SDK](https://www.github.com/absmartly/ruby-sdk)
- [.NET SDK](https://www.github.com/absmartly/dotnet-sdk) (this package)
- [Dart SDK](https://www.github.com/absmartly/dart-sdk)
- [Flutter SDK](https://www.github.com/absmartly/flutter-sdk)

## Documentation

- [Full Documentation](https://docs.absmartly.com/)

## License

See [LICENSE](LICENSE) for details.
