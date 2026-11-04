# A/B Smartly .NET SDK

A/B Smartly - .NET SDK

Latest
stable: [![NuGet stable](https://img.shields.io/nuget/v/ABSmartly.Sdk?style=flat-square)](https://www.nuget.org/packages/ABSmartly.Sdk)

Current
prerelease: [![NuGet pre](https://img.shields.io/nuget/vpre/ABSmartly.Sdk?style=flat-square)](https://www.nuget.org/packages/ABSmartly.Sdk)

## Compatibility

The A/B Smartly .NET SDK targets `netstandard2.0`, `net8.0` and `net9.0`.

Through .NET Standard 2.0 it is compatible with:
- .NET 5.0+
- .NET Core 2.0+
- .NET Framework 4.6.1+

**Note on Naming:** The main SDK class has been renamed from `ABSdk` to `ABsmartly` (with uppercase AB and lowercase smartly) to standardize naming across all SDKs. The old class names (`ABSdk`, `ABSdkConfig`, `ABSdkHttpClientFactory`, etc.) are still available as obsolete aliases for backwards compatibility, but new code should use the updated naming: `ABsmartly`.

## Installation

Install the A/B Smartly .NET SDK from NuGet:

```shell
dotnet add package ABSmartly.Sdk
```

## Getting Started

Please follow the [installation](#installation) instructions before trying the following code.

### Initialization

This example assumes an API Key, an Application, and an Environment have been created in the A/B Smartly web console.

#### Recommended: Dependency Injection Setup

If your project uses .NET dependency injection, use the default setup for the SDK:

**Startup code:**

```csharp
using ABSmartly;
using ABSmartly.DependencyInjection;

var builder = WebApplication.CreateBuilder(args);

// AddABsmartly registers its own named HttpClient (ABsmartly.HttpClientName),
// so no separate AddHttpClient() call is needed.
builder.Services.AddABsmartly(
    builder.Configuration.GetSection("ABSmartly"),
    HttpClientConfig.CreateDefault());

var app = builder.Build();
app.Run();
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
public class TestController : ControllerBase
{
    private readonly ABsmartly _absmartly;

    public TestController(ABsmartly absmartly)
    {
        _absmartly = absmartly;
    }
}
```

The `AddABsmartly` extension method allows you to configure SDK settings, HTTP connection settings, inject custom implementations of context-specific services, and configure additional HTTP request policies using Polly.

#### Alternative: Manual SDK Configuration

The SDK instance can also be created manually:

```csharp
using System.Net.Http;
using ABSmartly;
using ABSmartly.Services;
using Microsoft.Extensions.DependencyInjection;

var serviceProvider = new ServiceCollection().AddHttpClient().BuildServiceProvider();
var httpClientFactory = serviceProvider.GetRequiredService<IHttpClientFactory>();

var absmartly = new ABsmartly(new ABsmartlyHttpClientFactory(httpClientFactory), new ABSmartlyServiceConfiguration
{
    Environment = "development",
    Application = "website",
    Endpoint = "https://your-company.absmartly.io/v1",
    ApiKey = "YOUR-API-KEY"
});
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
| ContextPublisher    | `IContextPublisher`          | &#10060;  | auto    | Custom publisher for events (advanced usage). `ContextEventHandler` (`IContextEventHandler`) still works but is obsolete.                                                     |

**Note:** The SDK uses `IHttpClientFactory` abstraction to effectively manage HTTP connection pools. This factory is injected using `IABsmartlyHttpClientFactory` as seen in the example above. If custom behavior or implementation is required, inject your own implementation of either `IHttpClientFactory` or `IABsmartlyHttpClientFactory`.

#### Security considerations

These are deliberate decisions by the SDK owners. They keep the behaviour of the 1.3.0 release and the Java SDK.

- **Use an `https://` endpoint.** The API key travels in the `X-API-Key` header, and context data and events go in the request and response bodies. Over `http://`, all of them are sent in plaintext.
  - The SDK still accepts `http://` endpoints, because in-cluster collectors, sidecars and local proxies often use it. It logs a warning through the configured `ILoggerFactory` (the warning only appears if you configure logging).
  - Any scheme other than `http://` or `https://` throws an `ArgumentException`.
  - Protecting an `http://` endpoint is the deployment's responsibility, for example a private network or service mesh. Don't send traffic over `http://` across networks you don't trust.
- **Audience `match` patterns have no time limit.** Regular expressions in experiment audiences are evaluated with `System.Text.RegularExpressions` and no match timeout.
  - The patterns come from experiment definitions on the ABsmartly Web Console, so they're trusted configuration. Only people who can edit experiments can change them.
  - The text being matched comes from the attributes your application sets on the context.
  - A pattern prone to catastrophic backtracking, run against a large attribute value you don't control, can use a lot of CPU. Keep audience patterns simple, and limit the size of attribute values that come from end users.
  - An invalid pattern makes the `match` evaluate to `null`, so it doesn't match.


When injecting `IABsmartlyHttpClientFactory`, ensure it creates instances of the `IABsmartlyHttpClient` interface, which is a wrapper on top of `HttpClient`. The SDK provides `ABSmartly.Services.ABsmartlyHttpClientFactory.HttpClientWrapper` for this purpose.

When injecting `IHttpClientFactory`, ensure it creates named `HttpClient` instances with the name `ABSmartlySDK.HttpClient` (available as `ABsmartly.HttpClientName`).

## Creating a New Context

### Asynchronously (Recommended)

```csharp
var config = new ContextConfig()
    .SetUnit("session_id", "5ebf06d8cb5d8137290c4abb64155584fbdb64d8");

var context = await _absmartly.CreateContextAsync(config);
```

### Synchronously

```csharp
var config = new ContextConfig()
    .SetUnit("session_id", "5ebf06d8cb5d8137290c4abb64155584fbdb64d8");

var context = _absmartly.CreateContext(config);
```

`CreateContext()` and `CreateContextAsync()` return once the context data has loaded. If the data cannot be fetched, they do not throw: the context is returned with `IsFailed()` set to `true`, `ReadyError` holding the error that caused the failure, and an `Error` event logged. A failed context returns the control variant (`0`) and the default variable values.

These methods return `IContext`, whose members are unchanged since 1.3.0. Members added in 1.4.0 (`ReadyError`, `WaitUntilReady()`, `FinalizeAsync()`, `Experiments`, `Units`, `Attributes`, `GetAttribute()`, `VariableKeys`, ...) are on `IContextV2`, which the returned context implements: `var context = (IContextV2)_absmartly.CreateContext(config);`.

### Without Waiting for the Data

`CreatePendingContext()` returns at once with an `IContextV2` context that is not ready yet, and loads the data in the background. Until the context is ready, the read methods (`GetTreatment()`, `PeekTreatment()`, `GetVariableValue()`, `PeekVariableValue()`, `VariableKeys`, `Experiments` and the custom field methods) throw an `InvalidOperationException` with the message `ABSmartly Context is not yet ready` and queue no exposure. Call `WaitUntilReady()` or `WaitUntilReadyAsync()` first. `Track()`, `SetUnit()`, `SetAttribute()` and `SetOverride()` work as usual. Tracked goals are queued: the automatic publish timer starts only once the context is ready, but an explicit `Publish()`/`PublishAsync()` sends whatever is queued at once, even before the context is ready.

```csharp
IContextV2 context = _absmartly.CreatePendingContext(config);
context.Track("page_view", null); // queued before the context is ready

await context.WaitUntilReadyAsync(); // or context.WaitUntilReady()
if (context.IsFailed())
    Console.WriteLine($"context failed to load: {context.ReadyError?.Message}");
```

### With Pre-fetched Data

When doing full-stack experimentation with A/B Smartly, we recommend creating a context only once on the server-side. Creating a context involves a round-trip to the A/B Smartly event collector. We can avoid repeating the round-trip on the client-side by re-using data previously retrieved.

```csharp
var config = new ContextConfig()
    .SetUnit("session_id", "5ebf06d8cb5d8137290c4abb64155584fbdb64d8");
var context = await _absmartly.CreateContextAsync(config);

var anotherContextConfig = new ContextConfig()
    .SetUnit("session_id", "5ebf06d8cb5d8137290c4abb64155584fbdb64d9");

var anotherContext = _absmartly.CreateContextWith(anotherContextConfig, context.GetContextData());
```

### Refreshing the Context with Fresh Experiment Data

For long-running contexts, the context is usually created once when the application is first started. However, any experiments being tracked in your production code but started after the context was created will not be triggered.

To mitigate this, we can use the `RefreshInterval` property on the context config:

```csharp
var config = new ContextConfig()
    .SetUnit("session_id", "5ebf06d8cb5d8137290c4abb64155584fbdb64d8");
config.RefreshInterval = TimeSpan.FromHours(4); // every 4 hours
```

Alternatively, the `Refresh()` method can be called manually. The `Refresh()` method pulls updated experiment data from the A/B Smartly collector and will trigger recently started experiments when `GetTreatment()` is called again.

If a refresh fails, `Refresh()`/`RefreshAsync()` throws and the context keeps the data it already had. Refreshing doesn't recover a context whose initial load failed (`IsFailed()`): on such a context, `Refresh()` does nothing and `RefreshInterval` has no effect. To retry the load, create a new context.

```csharp
await context.RefreshAsync();

// or synchronously
context.Refresh();
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

#### Peeking at Variables

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

## Advanced

### Context Attributes

The `SetAttribute()` and `SetAttributes()` methods can be called before the context is ready.

```csharp
context.SetAttribute("user_agent", Request.Headers["User-Agent"]);

context.SetAttributes(new Dictionary<string, object> {
    { "customer_age", "new_customer" }
});
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

If the context's publisher reports a failure by throwing, or by returning a faulted task, the events stay queued (`PendingCount`) and a later `Publish()`, `Close()` or `Dispose()` retries them. The default publisher doesn't report an HTTP failure that way: as in 1.3.0, an unsuccessful response from the collector is not raised, so those events are not retried. To retry them, supply an `IContextPublisher` that throws on failure.

```csharp
await context.PublishAsync();

// or synchronously
context.Publish();
```

### Finalizing (Disposing)

The `Context` class implements `IDisposable` and `IAsyncDisposable` to ensure all events have been published to the A/B Smartly collector, like `Publish()`, and will also "seal" the context, throwing an error if any method that could generate an event is called.

The `IContext` and `IContextV2` interfaces are not disposable themselves (so that they stay compatible with 1.3.0), but the object returned by `CreateContext*()` and `CreatePendingContext()` is a `Context`. Cast it to use `using` / `await using`:

```csharp
using var context = (Context)_absmartly.CreateContext(config);

// or asynchronously
await using var asyncContext = (Context)await _absmartly.CreateContextAsync(config);
```

`IContextV2.FinalizeAsync()` publishes pending events and seals the context without disposing it, so units, attributes and experiments stay readable afterwards. `IContextV2.Close()` does the same synchronously.

`FinalizeAsync()`, `DisposeAsync()` and, on threads without a `SynchronizationContext`, `Close()` and `Dispose()` wait until the close has finished: publications already in flight have settled, and their failed events have been retried. A synchronous `Close()`/`Dispose()` on a UI or classic ASP.NET thread, or a publisher closing its own context, does not wait for publications already in flight, because waiting there could deadlock. The call returns at once, and the close finishes in the background once those publications settle. Until then the context is closing, not yet sealed, and `Dispose` releases its resources when the close completes. On such threads, use `await context.FinalizeAsync()` or `await using` to wait for the whole close.

### Custom Event Logger

The A/B Smartly SDK can be instantiated with an event logger used for all contexts. In addition, an event logger can be specified when creating a particular context in the `ContextConfig`.

```csharp
using ABSmartly;
using ABSmartly.Models;

public class CustomEventLogger : IContextEventLogger
{
    public void HandleEvent(IContext context, EventType eventType, object data)
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
builder.Services.AddABsmartly(
    builder.Configuration.GetSection("ABSmartly"),
    HttpClientConfig.CreateDefault(),
    config => config.ContextEventLogger = new CustomEventLogger());

// Or when creating SDK instance manually
var absmartly = new ABsmartly(
    new ABsmartlyHttpClientFactory(httpClientFactory),
    new ABSmartlyServiceConfiguration { /* Endpoint, ApiKey, Environment, Application */ },
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
| `Close`    | Context disposal, `Close()` or `FinalizeAsync()` succeeds   | `null`                                                 |

## Platform-Specific Examples

All ASP.NET Core examples read the SDK settings from the `ABSmartly` configuration section described in [Dependency Injection Setup](#recommended-dependency-injection-setup) (`Environment`, `Application`, `Endpoint` and `ApiKey` in `appsettings.json`), and register the SDK with `AddABsmartly()`, which also registers the `HttpClient` the SDK uses. The `session_id` examples use ASP.NET Core session state, which needs `AddDistributedMemoryCache()` (or another distributed cache), `AddSession()` and `UseSession()`.

`CreateContextAsync()` returns an `IContext`. The object it returns is a `Context`, which is the type that implements `IAsyncDisposable`, so the examples cast to `Context` to use `await using`.

### Using with ASP.NET Core Minimal APIs

```csharp
using ABSmartly;
using ABSmartly.DependencyInjection;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddABsmartly(
    builder.Configuration.GetSection("ABSmartly"),
    HttpClientConfig.CreateDefault());

// Sessions need a backing cache and middleware
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession();

var app = builder.Build();

app.UseSession();

app.MapGet("/", async (ABsmartly absmartly, HttpContext httpContext) =>
{
    var sessionId = httpContext.Session.GetString("SessionId")
        ?? Guid.NewGuid().ToString();
    httpContext.Session.SetString("SessionId", sessionId);

    var config = new ContextConfig()
        .SetUnit("session_id", sessionId);

    await using var context = (Context)await absmartly.CreateContextAsync(config);

    var treatment = context.GetTreatment("exp_test_experiment");

    return treatment == 0
        ? Results.Text("<h1>Control Group</h1>", "text/html")
        : Results.Text("<h1>Treatment Group</h1>", "text/html");
});

app.Run();
```

### Using with ASP.NET Core MVC

```csharp
// Program.cs
using ABSmartly;
using ABSmartly.DependencyInjection;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews();
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession();

builder.Services.AddABsmartly(
    builder.Configuration.GetSection("ABSmartly"),
    HttpClientConfig.CreateDefault());

var app = builder.Build();

app.UseRouting();
app.UseSession();
app.MapDefaultControllerRoute();

app.Run();
```

```csharp
// Controllers/HomeController.cs
using ABSmartly;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

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

        await using var context = (Context)await _absmartly.CreateContextAsync(config);

        var treatment = context.GetTreatment("exp_test_experiment");

        ViewBag.Treatment = treatment;

        return treatment == 0 ? View("Control") : View("Treatment");
    }
}
```

If you use a `Startup` class instead of top-level statements, call the same methods from it:

```csharp
// Startup.cs
using ABSmartly;
using ABSmartly.DependencyInjection;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

public class Startup
{
    public Startup(IConfiguration configuration)
    {
        Configuration = configuration;
    }

    public IConfiguration Configuration { get; }

    public void ConfigureServices(IServiceCollection services)
    {
        services.AddControllersWithViews();
        services.AddDistributedMemoryCache();
        services.AddSession();

        services.AddABsmartly(
            Configuration.GetSection("ABSmartly"),
            HttpClientConfig.CreateDefault());
    }

    public void Configure(IApplicationBuilder app)
    {
        app.UseRouting();
        app.UseSession();
        app.UseEndpoints(endpoints => endpoints.MapDefaultControllerRoute());
    }
}
```

### Using with ASP.NET Core Razor Pages

Register `AddRazorPages()`, `AddDistributedMemoryCache()`, `AddSession()`, `AddABsmartly(...)`, `app.UseSession()` and `app.MapRazorPages()` as above.

```csharp
// Pages/Index.cshtml.cs
using ABSmartly;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.RazorPages;

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

        await using var context = (Context)await _absmartly.CreateContextAsync(config);

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

builder.Services.AddABsmartly(
    builder.Configuration.GetSection("ABSmartly"),
    HttpClientConfig.CreateDefault());

var app = builder.Build();

app.UseStaticFiles();
app.UseRouting();
app.MapBlazorHub();
app.MapFallbackToPage("/_Host");

app.Run();
```

```razor
@* Pages/Index.razor *@
@page "/"
@using ABSmartly
@inject ABsmartly Sdk

<h1>@(treatment == 0 ? "Control Group" : "Treatment Group")</h1>

@code {
    private int treatment = 0;

    protected override async Task OnInitializedAsync()
    {
        var config = new ContextConfig()
            .SetUnit("session_id", Guid.NewGuid().ToString());

        await using var context = (Context)await Sdk.CreateContextAsync(config);

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
httpConfig.ConnectTimeout = 1500;

builder.Services.AddABsmartly(
    builder.Configuration.GetSection("ABSmartly"),
    httpConfig);
```

### Task-Based Navigation-Guard Pattern

`Task.WhenAny` with a timeout limits how long navigation waits; it does not cancel `CreateContextAsync`. This overload has no cancellation token. Always observe the eventual result and dispose the created context, including after a timeout:

```csharp
using ABSmartly;

var config = new ContextConfig()
    .SetUnit("session_id", "abc123");

var contextTask = absmartly.CreateContextAsync(config);
var timeoutTask = Task.Delay(TimeSpan.FromMilliseconds(1500));

var completedTask = await Task.WhenAny(contextTask, timeoutTask);
if (completedTask == contextTask)
    await ObserveAndDisposeAsync(contextTask, reportReady: true);
else
{
    Console.WriteLine("Context creation timed out; creation continues in the background");
    _ = ObserveAndDisposeAsync(contextTask, reportReady: false);
}

static async Task ObserveAndDisposeAsync(Task<IContext> creation, bool reportReady)
{
    IContext context = null;
    try
    {
        context = await creation;
        if (reportReady) Console.WriteLine("Context ready!");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Context creation failed: {ex.Message}");
    }
    finally
    {
        if (context is IAsyncDisposable disposable)
        {
            try { await disposable.DisposeAsync(); }
            catch (Exception ex) { Console.WriteLine($"Context disposal failed: {ex.Message}"); }
        }
    }
}
```

### Component Lifecycle Navigation-Guard Pattern

For Blazor or other component-based scenarios with navigation. `CreateContextAsync` cannot be cancelled, so the context is always disposed once it is created, even if the component has already navigated away:

```razor
@page "/guarded"
@using ABSmartly
@implements IDisposable
@inject ABsmartly Sdk

<h1>@(treatment == 0 ? "Control Group" : "Treatment Group")</h1>

@code {
    private int treatment = 0;
    private bool _isNavigating = false;

    protected override async Task OnInitializedAsync()
    {
        var config = new ContextConfig()
            .SetUnit("session_id", Guid.NewGuid().ToString());

        IContext context = null;
        try
        {
            context = await Sdk.CreateContextAsync(config);

            if (!_isNavigating)
            {
                treatment = context.GetTreatment("exp_test_experiment");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error creating context: {ex.Message}");
        }
        finally
        {
            if (context is IAsyncDisposable disposable)
            {
                await disposable.DisposeAsync();
            }
        }
    }

    public void Dispose()
    {
        _isNavigating = true;
    }
}
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
