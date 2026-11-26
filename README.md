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

**Note on Naming:** The SDK entry point is now `ABsmartly.Sdk` (class `Sdk` in the `ABsmartly` namespace), replacing `ABSmartly.ABSdk`. The other types stay in the `ABSmartly` namespace. Only the names published in 1.3.0 are kept as obsolete aliases (`ABSdk`, `ABSdkConfig`, `ABSdkHttpClientFactory`, `IABSdkHttpClientFactory`, `IABSdkHttpClient`, `AddABSmartly()`, `IContextEventHandler`, `DefaultContextEventHandler`); they still compile and work, but new code should use `ABsmartly.Sdk`, `ABsmartlyConfig`, `ABsmartlyHttpClientFactory`, `IABsmartlyHttpClientFactory`, `AddABsmartly()` and `IContextPublisher`.

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

// AddABsmartly registers its own named HttpClient (ABsmartly.Sdk.HttpClientName),
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

The `ABsmartly.Sdk` instance is added as a singleton and can be injected where needed:

```csharp
using ABSmartly;
using Microsoft.AspNetCore.Mvc;

[ApiController]
[Route("[controller]")]
public class TestController : ControllerBase
{
    private readonly ABsmartly.Sdk _absmartly;

    public TestController(ABsmartly.Sdk absmartly)
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

var absmartly = new ABsmartly.Sdk(new ABsmartlyHttpClientFactory(httpClientFactory), new ABSmartlyServiceConfiguration
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
| ApplicationVersion  | `string`                     | &#10060;  | `"0"`   | The version of your application as a whole number (for example `"1004"`), sent as the `X-Application-Version` header. Any other value, such as `"2.4.1"`, is sent as `"0"`.                                                                                                 |
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
  - Experiment editors control the audience expression in the ABsmartly Web Console. A literal pattern is part of that configuration; if the pattern operand references an attribute, the application supplies the actual pattern value through that attribute.
  - The text being matched also comes from the attributes your application sets on the context.
  - A pattern prone to catastrophic backtracking, run against a large attribute value you don't control, can use a lot of CPU. Keep audience patterns simple, limit the size of attribute values that come from end users, and don't pass untrusted input as a pattern-valued attribute.
  - An invalid pattern makes the `match` evaluate to `null`, so it doesn't match.


When injecting `IABsmartlyHttpClientFactory`, ensure it creates instances of the `IABsmartlyHttpClient` interface, which is a wrapper on top of `HttpClient`. The SDK provides `ABSmartly.Services.ABsmartlyHttpClientFactory.HttpClientWrapper` for this purpose.

When injecting `IHttpClientFactory`, ensure it creates named `HttpClient` instances with the name `ABSmartlySDK.HttpClient` (available as `ABsmartly.Sdk.HttpClientName`).

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

`CreateContext()` and `CreateContextAsync()` return once the context data has loaded. If the data cannot be fetched, they do not throw: the context is returned with `IsFailed()` set to `true`, `ReadyError` holding the error that caused the failure, and an `Error` event logged. A failed context returns the control variant (`0`) and the default variable values. The one exception, as in 1.3.0: if your event logger throws on that `Ready` or `Error` event, `CreateContext()`/`CreateContextAsync()` throw that exception.

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

Audience filters and assignment-rule conditions can also use `application` and `environment`, which the SDK takes from `ABSmartlyServiceConfiguration`. An attribute you set with the same name overrides them. They are not added to the published attributes; the collector already records both. Assignment-rule `environments` scoping always uses the configured `Environment`, not an `environment` attribute.

Attributes are re-read after every `SetAttribute()`: the next `GetTreatment()`, `PeekTreatment()` or `GetVariableValue()` re-evaluates the experiment's audience and assignment rules, so the variant can change when an attribute changes whether the audience matches or which rule matches, even after it was exposed (the new variant is then exposed too). Set the attributes an experiment depends on before its first exposure.

Audience filters and assignment-rule conditions support semantic-version comparisons with `semver_eq`, `semver_gt`, `semver_gte`, `semver_lt` and `semver_lte`, for example `{"semver_gte": [{"var": "app_version"}, {"value": "2.4.0"}]}`. A leading `v` and build metadata are ignored, missing parts count as `0` and a pre-release ranks below its release (`2.4.0-beta.1` < `2.4.0`).

### Assignment Rules

Experiments can define assignment rules in the Web Console. The SDK evaluates them in order, and the first rule whose conditions match the context attributes and whose environments include the configured `Environment` decides the variant:

- An `assign` rule gives a fixed variant.
- A `split` rule spreads matching units over the variants by its own percentages, for example `20/30/50`. The unit is hashed with the experiment's seed. While the experiment's unit isn't set, the unit gets the variant it would get without a unit (the full-on variant, otherwise `0`); once the unit is set, an assignment that has not been exposed yet is re-randomized.

A rule wins over the audience (including strict audiences), traffic allocation, full-on and `SetCustomAssignment()`. Only `SetOverride()` wins over a rule. Exposures from a rule are flagged `ruleOverride`, so they are excluded from the experiment's analysis.

If the first matching rule's variant doesn't exist, or its split doesn't have one percentage per variant, the unit gets the normal assignment and later rules aren't tried. A malformed rule (unknown type, a variant that isn't an integer, percentages that aren't numbers or don't add up to 100) is skipped in favour of the next rule.

### Holdouts

A holdout keeps a share of units out of a group of experiments, so their combined effect can be measured against units that saw none of them. Holdouts are configured in the Web Console; the context data lists them, and each covered experiment names its holdouts. Nothing changes in your code: `GetTreatment()`, `PeekTreatment()`, `GetVariableValue()` and `PeekVariableValue()` apply them.

- The unit is placed in a holdout arm with the covered experiment's unit type. A unit in arm 0 is held out of every experiment the holdout covers. A holdout with three arms also holds arm 1 out of the covered experiments that are not full-on; full-on experiments assign it normally. Any other arm assigns the experiments normally.
- An experiment covered by several holdouts is held out when any one of them holds the unit out.
- A held-out experiment returns variant `0` and its control variables, and publishes no exposure of its own. Its audience, full-on variant, traffic allocation and `SetCustomAssignment()` are not applied. When several experiments share a variable key, a held-out one gives its control value only if no other experiment assigns the unit.
- Each holdout publishes its own exposure (the holdout's id and name, the arm as the variant) once per unit type, the first time an exposure-tracking read evaluates an experiment it covers: `GetTreatment()`, or `GetVariableValue()` for every experiment defining the key that it visits, including a held-out one or one that loses the key to another experiment. `PeekTreatment()` and `PeekVariableValue()` publish nothing.
- `SetOverride()` and a matching assignment rule win over a holdout. The holdout's exposure is still published.
- The arm is kept until the holdout's iteration changes, even if a refresh changes its seed or split.
- A holdout that cannot be read (for example an `id`, `iteration`, `seedHi` or `seedLo` that is null, fractional (`11.5`) or outside the 32-bit range) is ignored, so the experiments it covers are assigned as if it did not exist. An integral decimal such as `11.0` is read as that integer.

### Switchback Experiments

A switchback experiment is randomized over *clusters* and time rather than over units: every unit in the same cluster (for example a store's region) sees the same variant within a period, and the variant changes on a fixed time grid. It follows the cluster-defined contract the Collector serves, as in the Java SDK (`SWITCHBACK.md` in absmartly/java-sdk). Nothing changes in your code: `GetTreatment()`, `PeekTreatment()`, `GetVariableValue()` and `PeekVariableValue()` serve switchbacks, and you set the cluster value as an ordinary attribute.

```csharp
context.SetUnit("store_id", "store-1234");
context.SetAttribute("store", new Dictionary<string, object> { ["region"] = "lisbon" });

// The variant of the "lisbon" cluster for the current period.
var treatment = context.GetTreatment("exp_delivery_fee");
```

- The experiment's `switchbackClusters` names, in order, the unit types it is linked to and the attribute path of each one's cluster value (`store/region` above; `/` nests, a quoted segment is a literal key, an integer segment indexes a list). The first linked unit, in that order, whose path gives a value serves the experiment; its value is the cluster key, used as is (never hashed).
- A cluster value is read as it is published: a string with an unpaired surrogate is keyed with U+FFFD in its place, as the JSON encoder writes it, whether it is set at the root of a cluster path or nested. The Java SDK keys a root string unchanged.
- A cluster value is a non-empty string, or a whole number of an integer type (`int`, `long`, ...), used as its base-10 text. Every floating-point number (including `3.0` and `Half`), `decimal` with a fraction or scale, boolean, object, array, empty string and `null` gives no value. An enum is read as its name, as the Java SDK reads it. Set integer cluster values with an integer type.
- With no linked unit set, the experiment does not apply. With no value, disagreeing values, or a value outside `switchbackClusterValues`, the unit gets an ineligible control (variant 0) and nothing is published. Only `SetOverride()` still applies there.
- Otherwise the usual precedence applies, in this order: override, assignment rule, strict audience, full-on, traffic (on the serving unit), custom assignment, then the schedule draw. Before the grid's start (`switchbackStartAt`), or with a schedule the Collector cannot serve, the draw gives a silent control.
- Holdouts never apply to a switchback.
- Each period is exposed once, stamped with the time the period's cluster value was read, so the Collector, which re-resolves the cluster from the published attributes, replays the cluster and period that served. Once switchback data has been installed, `DefaultContextEventSerializer` publishes attribute values as the cluster lookup reads them: a whole floating-point number as a float (`3.0`), a NaN or infinite number as `null` and an enum as its name; the `PublishEvent` values themselves are untouched. The cluster value is read once per period: a `SetAttribute()` later in the same period takes effect in the next one. (A cluster attribute changed in the same millisecond as the read that served it is the one limit: the Collector, which breaks setAt ties by the last value set, replays the new value.)
- A switchback's assignment rules see the application and environment its context was created with, the same values its cluster lookup and its requests use, so a rule scoped to an environment and a cluster on `environment` never disagree.
- A cluster path can be rooted at `application`, `application_version` or `environment`. Unless you set an attribute of that name to a non-null value, it reads the value from `ABSmartlyServiceConfiguration` (`Application`, `ApplicationVersion` when it is a whole number, `Environment`), as the Collector does when it replays the cluster. (The Java SDK does not read these values, so there such a path finds no value and the cluster is ineligible.)
- A context reads those configured values when it is created. The request headers are read from `ABSmartlyServiceConfiguration` on every request, as before, so a context created after you change its application, environment or version serves with the values its requests carry. Changing them while a context is in use is not supported for cluster paths rooted at them: that context keeps serving with the values it was created with, while its later requests carry the new ones.
- Once switchback data is installed, a structured attribute value (a dictionary, list or object) is published as it was when you set it, or when a cluster lookup first read it. Mutating that object afterwards changes neither its cluster nor what is published; call `SetAttribute()` again for a new value. `PublishEvent.Attributes` still holds your object. A custom `IContextPublisher` that redacts a structured attribute must assign `Attribute.Value` (a new object, or the same one after changing it); changing it in place without assigning is not published.
- Set each cluster attribute under one name. Names that differ only by leading or trailing spaces or control characters (`city` and `" city "`) are the same attribute to the cluster lookup, but the Collector may replay either value, so such aliases are not supported.
- Changing a cluster attribute in the middle of a period is not supported, whether before the period's exposure is published or while it waits for a retry. Set the cluster attribute before the first switchback lookup of each period. The arm cached at the period's first read that can expose stays for the rest of the period (after a silent first read, such as before the start or with no usable value, a rebuilt lookup reads the cluster again), and the exposure is published (or retried) with it. The new value takes effect from the next period. This matches the Java SDK and the Collector, and no change to them is planned.
- The published exposures and payload have no new fields; attributes are published with their timestamps, as before. The Collector replays each switchback exposure's cluster from the published units and attributes, so the SDK keeps that replay faithful. A switchback exposure served before another linked unit was set (a login) is published in its own request carrying only the units present when it was served. A cluster attribute set again after a switchback read it for an exposure, but with an earlier clock reading (the clock went back), is dated just after that read; other attributes, including a cluster attribute only a silent lookup (no exposure) has read, keep the clock reading, and a later lookup that can expose reads the cluster again. With a custom `IContextPublisher`, such a split means more than one `PublishAsync` call for one `Publish()`.

Contexts read the period from their clock, `DateTimeOffset.UtcNow` unless you pass your own `Clock`.

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

`FinalizeAsync()`, `DisposeAsync()` and, on threads without a `SynchronizationContext` (and running on the default `TaskScheduler`), `Close()` and `Dispose()` wait until the close has finished: publications already in flight have settled, and their failed events have been retried. A synchronous `Close()`/`Dispose()` on a UI or classic ASP.NET thread (any thread with a `SynchronizationContext`, or code running on a custom `TaskScheduler`), or a publisher closing its own context, does not wait for publications already in flight, because waiting there could deadlock. When it starts the close, it publishes the events queued at that point before it returns, and the close finishes (seals the context) in the background once those publications settle. When it joins a close already in progress, it returns at once and leaves the queued events to that close. Until then the context is closing, not yet sealed, and `Dispose` releases its resources when the close completes. On such threads, use `await context.FinalizeAsync()` or `await using` to wait for the whole close. Once a close has started, a new `Publish()`/`PublishAsync()` throws `InvalidOperationException` ("ABSmartly Context is closing"). A publish that was already under way when the close started, or the automatic publish timer, leaves the queued events to that close and returns without publishing them; the close reports any failure to publish them. A custom `IContextPublisher` should not resume on the UI thread (use `ConfigureAwait(false)`): a close that waits for its publication while the UI thread is blocked on that close, as in `Task.Run(() => context.Dispose()).Wait()`, would deadlock. The default publisher does not.

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
var absmartly = new ABsmartly.Sdk(
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
| `Publish`  | The publisher completes a request (from `Publish()`, `PublishAsync()`, a timed flush or a close) | `PublishEvent` handed to the `IContextPublisher` |
| `Exposure` | `GetTreatment()` method succeeds on first exposure          | `Exposure` enqueued for publishing                     |
| `Goal`     | `Track()` method succeeds                                   | `GoalAchievement` enqueued for publishing              |
| `Close`    | Context disposal, `Close()` or `FinalizeAsync()` succeeds   | `null`                                                 |

A `Publish` event means the `IContextPublisher` task completed. The default publisher's task completes even when it logs a failure instead (the event could not be serialized, or the Collector returned an error status), so a `Publish` event is not an acknowledgement that the Collector accepted every event. In a context with switchback experiments one flush or close can send more than one request (see [Switchback Experiments](#switchback-experiments)): each completed request logs its own `Publish` event, in order, and when a later request fails the earlier ones have already been logged before its `Error`.

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

app.MapGet("/", async (ABsmartly.Sdk absmartly, HttpContext httpContext) =>
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
    private readonly ABsmartly.Sdk _absmartly;

    public HomeController(ABsmartly.Sdk absmartly)
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
    private readonly ABsmartly.Sdk _absmartly;

    public int Treatment { get; set; }

    public IndexModel(ABsmartly.Sdk absmartly)
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
@inject ABsmartly.Sdk Sdk

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
@inject ABsmartly.Sdk Sdk

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

## Publishing to NuGet

[Publish NuGet](.github/workflows/publish.yml) builds, tests and packs the SDK and uploads the package artifact. Manual runs default to `dry_run: true`, which skips both NuGet login and push. Existing `v*` tag pushes (with a tag matching `PackageVersion`) and manual runs with `dry_run: false` publish to nuget.org.

Publishing uses [NuGet Trusted Publishing](https://learn.microsoft.com/en-us/nuget/nuget-org/trusted-publishing) with the NuGet profile username `jonasalves` (not an email). The existing NuGet policy trusts repository `absmartly/dotnet-sdk`, workflow `publish.yml`, with **no environment**, and allows new packages and versions. `NuGet/login` exchanges GitHub OIDC for a short-lived API key immediately before push; no repository secret or long-lived API key setup is required.

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
