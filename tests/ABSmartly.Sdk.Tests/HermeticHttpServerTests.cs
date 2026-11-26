using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ABSmartly.Services;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json.Linq;

namespace ABSmartly.Sdk.Tests;

// Hermetic end-to-end transport test.
//
// Starts a REAL local HTTP server (System.Net.HttpListener) on a 127.0.0.1 ephemeral
// port, points the SDK's public client at it, and drives the PUBLIC SDK API so the
// SDK's REAL HttpClient performs:
//   1. GET  /context  (createContext -> wait ready)
//   2. PUT  /context  (treatment + track -> publish)
// then asserts on the requests the server actually received (path, query, method,
// headers, body) per the ABsmartly <-> collector wire contract.
[TestFixture]
public class HermeticHttpServerTests
{
    private const string Application = "website";
    private const string Environment = "dev";
    private const string ApiKey = "test-api-key";

    private HttpListener _listener = null!;
    private string _baseUrl = null!;
    private CancellationTokenSource _cts = null!;
    private Task _serverLoop = null!;

    private readonly ConcurrentQueue<RecordedRequest> _requests = new();
    private string _contextBody = "{\"experiments\":[]}";

    [SetUp]
    public void SetUp()
    {
        _requests.Clear();
        _contextBody = "{\"experiments\":[]}";
        var port = GetFreePort();
        _baseUrl = $"http://127.0.0.1:{port}";

        _listener = new HttpListener();
        _listener.Prefixes.Add(_baseUrl + "/");
        _listener.Start();

        _cts = new CancellationTokenSource();
        _serverLoop = Task.Run(() => ServeAsync(_cts.Token));
    }

    [TearDown]
    public void TearDown()
    {
        _cts.Cancel();
        try
        {
            _listener.Stop();
            _listener.Close();
        }
        catch
        {
        }
    }

    [Test]
    public async Task DrivesRealHttpClient_Get_And_Put_Context()
    {
        var sdk = BuildSdk();

        var config = new ContextConfig();
        config.SetUnit("session_id", "bleh@absmartly.com");

        var context = await sdk.CreateContextAsync(config);
        context.IsReady().Should().BeTrue("the context should be ready after the GET completes");

        var get = WaitForRequest(r => r.Method == "GET");
        get.Should().NotBeNull("the SDK should have performed a real GET /context");
        get!.Path.Should().Be("/context");
        get.Query.Should().ContainKey("application").WhoseValue.Should().Be(Application);
        get.Query.Should().ContainKey("environment").WhoseValue.Should().Be(Environment);

        context.GetTreatment("exp_test_ab");
        context.Track("payment", new Dictionary<string, object> { ["amount"] = 100 });

        await context.PublishAsync();

        var put = WaitForRequest(r => r.Method == "PUT");
        put.Should().NotBeNull("publish should have performed a real PUT /context");
        put!.Path.Should().Be("/context");
        put.Query.Should().BeEmpty("publish must not append query params");

        put.Headers.Should().ContainKey("X-API-Key").WhoseValue.Should().Be(ApiKey);
        put.Headers.Should().ContainKey("X-Application").WhoseValue.Should().Be(Application);
        put.Headers.Should().ContainKey("X-Environment").WhoseValue.Should().Be(Environment);
        put.Headers.Should().ContainKey("X-Application-Version").WhoseValue.Should().Be("0");
        put.Headers.Should().ContainKey("X-Agent");
        put.Headers["X-Agent"].Should().StartWith("absmartly-dotnet-sdk/");
        put.Headers.Should().ContainKey("Content-Type");
        put.Headers["Content-Type"].Should().StartWith("application/json");

        var body = JObject.Parse(put.Body);
        body.Should().ContainKey("hashed");
        body.Should().ContainKey("units");
        body.Should().ContainKey("publishedAt");

        body["units"].Should().BeOfType<JArray>();
        ((JArray)body["units"]!).Count.Should().BeGreaterThan(0, "the published units must be present");

        body.Should().ContainKey("goals");
        body["goals"].Should().BeOfType<JArray>();
        ((JArray)body["goals"]!).Count.Should().BeGreaterThan(0, "the tracked goal must be published");
    }

    // Cross-SDK scenario 320: the SDK's own context GET carries the same SDK headers as the
    // publish PUT, with X-Agent in the form absmartly-<lang>-sdk/<semver>.
    [Test]
    public async Task ContextFetch_SendsSdkHeaders()
    {
        var sdk = BuildSdk();

        var config = new ContextConfig();
        config.SetUnit("session_id", "svk-unit-1");

        var context = await sdk.CreateContextAsync(config);
        context.IsReady().Should().BeTrue();
        context.IsFailed().Should().BeFalse();

        var get = WaitForRequest(r => r.Method == "GET");
        get.Should().NotBeNull("the SDK should have performed a real GET /context");
        get!.Query.Should().ContainKey("application").WhoseValue.Should().Be(Application);
        get.Query.Should().ContainKey("environment").WhoseValue.Should().Be(Environment);

        get.Headers.Should().ContainKey("X-API-Key").WhoseValue.Should().Be(ApiKey);
        get.Headers.Should().ContainKey("X-Application").WhoseValue.Should().Be(Application);
        get.Headers.Should().ContainKey("X-Environment").WhoseValue.Should().Be(Environment);
        get.Headers.Should().ContainKey("X-Application-Version").WhoseValue.Should().Be("0");
        get.Headers.Should().ContainKey("X-Agent").WhoseValue.Should()
            .MatchRegex(@"^absmartly-[a-z0-9]+-sdk/[0-9]+\.[0-9]+\.[0-9]+([-+][0-9A-Za-z.+-]*)?$");
    }

    // The collector binds X-Application-Version as a Long: a dotted version would make it reject
    // both the context fetch and the publish with HTTP 400, so the SDK sends "0" instead.
    [TestCase("3.2.1", "0")]
    [TestCase("1004", "1004")]
    public async Task ApplicationVersionHeader_IsAWholeNumberOnGetAndPut(string configured, string expected)
    {
        var sdk = BuildSdk(configured);

        var config = new ContextConfig();
        config.SetUnit("session_id", "app-version-unit");

        var context = await sdk.CreateContextAsync(config);
        context.IsReady().Should().BeTrue();
        context.GetTreatment("exp_test_ab");
        await context.PublishAsync();

        var get = WaitForRequest(r => r.Method == "GET");
        var put = WaitForRequest(r => r.Method == "PUT");
        get!.Headers.Should().ContainKey("X-Application-Version").WhoseValue.Should().Be(expected);
        put!.Headers.Should().ContainKey("X-Application-Version").WhoseValue.Should().Be(expected);
    }

    // A switchback context over real HTTP: the PUT body carries a whole floating-point cluster value
    // as 3.0 (the Collector reads 3 as the integer key "3").
    [Test]
    public async Task SwitchbackPublishCarriesTheWireEncoding()
    {
        using (var stream = GetType().Assembly.GetManifestResourceStream("ABSmartly.Sdk.Tests.Resources.switchback_context.json")!)
        using (var reader = new StreamReader(stream))
            _contextBody = await reader.ReadToEndAsync();
        var sdk = BuildSdk("1004");

        var config = new ContextConfig();
        config.SetUnit("store_id", "s-1");
        config.SetUnit("device_id", "d-1");
        config.SetAttribute("store", new Dictionary<string, object> { ["key"] = "porto" });
        config.SetAttribute("geo", new Dictionary<string, object> { ["city"] = 3.0 });
        var context = await sdk.CreateContextAsync(config);
        context.IsReady().Should().BeTrue();
        context.GetTreatment("exp_test_switchback");
        await context.PublishAsync();

        var put = WaitForRequest(r => r.Method == "PUT");
        put.Should().NotBeNull();
        put!.Body.Should().Contain("\"value\":{\"city\":3.0}");
        var body = JObject.Parse(put.Body);
        ((JArray)body["exposures"]!).Should().ContainSingle();
    }

    // The caller changes its configuration after building the SDK, as 1.4.1 allows: the requests
    // carry the new application, environment and version, and a context created afterwards serves a
    // switchback clustered on application_version with the version its PUT carries, so the
    // Collector, which replays that cluster from the header, records the exposure it served.
    [Test]
    public async Task ConfigurationChangedAfterBuildReachesTheHeadersAndServing()
    {
        var data = JObject.Parse(await new StreamReader(GetType().Assembly
            .GetManifestResourceStream("ABSmartly.Sdk.Tests.Resources.switchback_context.json")!).ReadToEndAsync());
        var experiment = (JObject)data["experiments"]![0]!;
        experiment["switchbackClusters"] = new JObject { ["store_id"] = "application_version" };
        experiment["switchbackClusterValues"] = new JArray("1005");
        _contextBody = data.ToString();
        var sdk = BuildSdk("1004", out var serviceConfig);
        serviceConfig.ApplicationVersion = "1005";
        serviceConfig.Environment = "staging";
        serviceConfig.Application = "mobile";

        var config = new ContextConfig();
        config.SetUnit("store_id", "s-1");
        var context = await sdk.CreateContextAsync(config);
        context.IsReady().Should().BeTrue();
        context.GetTreatment("exp_test_switchback");
        await context.PublishAsync();

        foreach (var request in new[] { WaitForRequest(r => r.Method == "GET")!, WaitForRequest(r => r.Method == "PUT")! })
        {
            request.Headers["X-Application-Version"].Should().Be("1005");
            request.Headers["X-Environment"].Should().Be("staging");
            request.Headers["X-Application"].Should().Be("mobile");
        }

        var put = JObject.Parse(WaitForRequest(r => r.Method == "PUT")!.Body);
        ((JArray)put["exposures"]!).Should().ContainSingle().Which["eligible"]!.Value<bool>().Should().BeTrue();
    }

    // A switchback exposure served before a later linked unit was set is sent over real HTTP in its
    // own PUT carrying only the units present when it was served; the goal tracked after the new
    // unit goes in a second PUT with every unit. The Collector replays the cluster from each PUT's
    // units, so the late unit's disagreeing cluster value cannot drop the exposure.
    [Test]
    public async Task ALateLinkedUnitSplitsThePublishIntoServingTimeRequests()
    {
        using (var stream = GetType().Assembly.GetManifestResourceStream("ABSmartly.Sdk.Tests.Resources.switchback_context.json")!)
        using (var reader = new StreamReader(stream))
            _contextBody = await reader.ReadToEndAsync();
        var sdk = BuildSdk();
        var config = new ContextConfig();
        config.SetUnit("store_id", "s-1");
        config.SetAttribute("store", new Dictionary<string, object> { ["key"] = "porto" });
        config.SetAttribute("geo", new Dictionary<string, object> { ["city"] = "lisbon" });
        var context = await sdk.CreateContextAsync(config);
        context.IsReady().Should().BeTrue();

        context.GetTreatment("exp_test_switchback");
        context.SetUnit("device_id", "d-1");
        context.Track("purchase", null!);
        await context.PublishAsync();

        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (_requests.Count(r => r.Method == "PUT") < 2 && DateTime.UtcNow < deadline) await Task.Delay(25);
        var puts = _requests.Where(r => r.Method == "PUT").Select(r => JObject.Parse(r.Body)).ToList();
        puts.Should().HaveCount(2);

        string[] Units(JObject body) => ((JArray)body["units"]!).Select(u => (string)u["type"]!).ToArray();
        var exposurePut = puts.Single(b => b["exposures"] is JArray { Count: > 0 });
        var goalPut = puts.Single(b => b["goals"] is JArray { Count: > 0 });
        Units(exposurePut).Should().Equal("store_id");
        ((JArray)exposurePut["exposures"]!).Should().ContainSingle().Which["name"]!.Value<string>()
            .Should().Be("exp_test_switchback");
        Units(goalPut).Should().Equal("store_id", "device_id");
    }

    // A switchback clustered on environment, in a context created after the configuration changed
    // from dev to staging: its environment-scoped rule sees the same staging its cluster and its PUT
    // see, so a staging-only rule wins (ruleOverride) instead of being skipped for the old dev.
    [Test]
    public async Task SwitchbackRulesUseTheEnvironmentTheContextWasCreatedWith()
    {
        var data = JObject.Parse(await new StreamReader(GetType().Assembly
            .GetManifestResourceStream("ABSmartly.Sdk.Tests.Resources.switchback_context.json")!).ReadToEndAsync());
        var experiment = (JObject)data["experiments"]![0]!;
        experiment["switchbackClusters"] = new JObject { ["store_id"] = "environment" };
        experiment["switchbackClusterValues"] = new JArray("staging");
        experiment["fullOnVariant"] = 1;
        experiment["assignmentRules"] =
            "{\"rules\":[{\"type\":\"assign\",\"environments\":[\"staging\"],\"variant\":0}]}";
        _contextBody = data.ToString();
        var sdk = BuildSdk(null, out var serviceConfig);
        serviceConfig.Environment = "staging";

        var config = new ContextConfig();
        config.SetUnit("store_id", "s-1");
        var context = await sdk.CreateContextAsync(config);
        context.IsReady().Should().BeTrue();
        context.GetTreatment("exp_test_switchback").Should().Be(0, "the staging-only rule wins");
        await context.PublishAsync();

        var put = WaitForRequest(r => r.Method == "PUT")!;
        put.Headers["X-Environment"].Should().Be("staging");
        var exposure = ((JArray)JObject.Parse(put.Body)["exposures"]!).Should().ContainSingle().Subject;
        exposure["ruleOverride"]!.Value<bool>().Should().BeTrue();
        exposure["variant"]!.Value<int>().Should().Be(0);
    }

    // The same change after requests were already sent: the service reads the caller's configuration
    // on every request, not once at its first, so the refresh, a second context's fetch and both
    // PUTs carry the new values, and the second context serves the version its PUT carries.
    [Test]
    public async Task ConfigurationChangedAfterRequestsReachesLaterRequestsAndContexts()
    {
        var data = JObject.Parse(await new StreamReader(GetType().Assembly
            .GetManifestResourceStream("ABSmartly.Sdk.Tests.Resources.switchback_context.json")!).ReadToEndAsync());
        var experiment = (JObject)data["experiments"]![0]!;
        experiment["switchbackClusters"] = new JObject { ["store_id"] = "application_version" };
        experiment["switchbackClusterValues"] = new JArray("1005");
        _contextBody = data.ToString();
        var sdk = BuildSdk("1004", out var serviceConfig);
        var config = new ContextConfig();
        config.SetUnit("store_id", "s-1");
        var first = await sdk.CreateContextAsync(config);
        first.GetTreatment("exp_test_switchback");
        first.Track("before", null!);
        await first.PublishAsync();
        WaitForRequest(r => r.Method == "PUT")!.Headers["X-Application-Version"].Should().Be("1004");
        _requests.Clear();

        serviceConfig.ApplicationVersion = "1005";
        serviceConfig.Environment = "staging";
        serviceConfig.Application = "mobile";
        await first.RefreshAsync();
        first.Track("after", null!);
        await first.PublishAsync();
        var second = await sdk.CreateContextAsync(config);
        second.GetTreatment("exp_test_switchback");
        await second.PublishAsync();

        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (_requests.Count < 4 && DateTime.UtcNow < deadline) await Task.Delay(25);
        var requests = _requests.ToArray();
        requests.Count(r => r.Method == "GET").Should().Be(2);
        requests.Count(r => r.Method == "PUT").Should().Be(2);
        foreach (var request in requests)
        {
            request.Headers["X-Application-Version"].Should().Be("1005");
            request.Headers["X-Environment"].Should().Be("staging");
            request.Headers["X-Application"].Should().Be("mobile");
        }

        var exposures = requests.Where(r => r.Method == "PUT").Select(r => JObject.Parse(r.Body))
            .SelectMany(b => (JArray?)b["exposures"] ?? new JArray()).ToList();
        exposures.Should().ContainSingle().Which["eligible"]!.Value<bool>().Should().BeTrue();
    }

    [Test]
    public async Task CallerCredentialAndRotationsReachGetAndPut()
    {
        var sdk = BuildSdk(null, out var serviceConfig, "customer-secret-239");
        var config = new ContextConfig();
        config.SetUnit("session_id", "credential-test");
        var context = await sdk.CreateContextAsync(config);
        WaitForRequest(r => r.Method == "GET")!.Headers["X-API-Key"].Should().Be("customer-secret-239");
        context.Track("first", null!);
        await context.PublishAsync();
        WaitForRequest(r => r.Method == "PUT")!.Headers["X-API-Key"].Should().Be("customer-secret-239");
        _requests.Clear();
        serviceConfig.ApiKey = "rotated-customer-secret-781";
        await context.RefreshAsync();
        WaitForRequest(r => r.Method == "GET")!.Headers["X-API-Key"].Should().Be("rotated-customer-secret-781");
        context.Track("second", null!);
        await context.PublishAsync();
        WaitForRequest(r => r.Method == "PUT")!.Headers["X-API-Key"].Should().Be("rotated-customer-secret-781");
    }

    // Builds the SDK using ONLY the public configuration surface, wiring the SDK's
    // REAL ABsmartlyHttpClientFactory over a REAL IHttpClientFactory so a genuine
    // System.Net.Http.HttpClient performs the requests.
    private ABsmartly.Sdk BuildSdk(string? applicationVersion = null) => BuildSdk(applicationVersion, out _);

    private ABsmartly.Sdk BuildSdk(string? applicationVersion, out ABSmartlyServiceConfiguration serviceConfig, string apiKey = ApiKey)
    {
        var services = new ServiceCollection();
        services.AddHttpClient(ABsmartly.Sdk.HttpClientName);
        services.AddTransient<IABsmartlyHttpClientFactory, ABsmartlyHttpClientFactory>();
        var provider = services.BuildServiceProvider();

        var httpClientFactory = provider.GetRequiredService<IABsmartlyHttpClientFactory>();

        serviceConfig = new ABSmartlyServiceConfiguration
        {
            Endpoint = _baseUrl,
            ApiKey = apiKey,
            Application = Application,
            Environment = Environment,
            ApplicationVersion = applicationVersion!
        };

        return new ABsmartly.Sdk(httpClientFactory, serviceConfig);
    }

    private async Task ServeAsync(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            HttpListenerContext ctx;
            try
            {
                ctx = await _listener.GetContextAsync();
            }
            catch
            {
                return; // listener stopped
            }

            var req = ctx.Request;

            string body;
            using (var reader = new StreamReader(req.InputStream, req.ContentEncoding ?? Encoding.UTF8))
            {
                body = await reader.ReadToEndAsync();
            }

            var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (string name in req.Headers)
                headers[name] = req.Headers[name]!;

            var query = new Dictionary<string, string>();
            foreach (string key in req.QueryString)
            {
                if (key != null)
                    query[key] = req.QueryString[key]!;
            }

            _requests.Enqueue(new RecordedRequest
            {
                Method = req.HttpMethod,
                Path = req.Url!.AbsolutePath,
                Query = query,
                Headers = headers,
                Body = body
            });

            byte[] payload;
            if (req.HttpMethod == "GET")
                payload = Encoding.UTF8.GetBytes(_contextBody);
            else
                payload = Encoding.UTF8.GetBytes("{}");

            ctx.Response.StatusCode = 200;
            ctx.Response.ContentType = "application/json";
            ctx.Response.ContentLength64 = payload.Length;
            await ctx.Response.OutputStream.WriteAsync(payload, 0, payload.Length);
            ctx.Response.Close();
        }
    }

    private RecordedRequest? WaitForRequest(Func<RecordedRequest, bool> predicate)
    {
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (DateTime.UtcNow < deadline)
        {
            foreach (var r in _requests)
            {
                if (predicate(r))
                    return r;
            }

            Thread.Sleep(25);
        }

        return null;
    }

    private static int GetFreePort()
    {
        var l = new System.Net.Sockets.TcpListener(IPAddress.Loopback, 0);
        l.Start();
        var port = ((IPEndPoint)l.LocalEndpoint).Port;
        l.Stop();
        return port;
    }

    private sealed class RecordedRequest
    {
        public string Method { get; init; } = "";
        public string Path { get; init; } = "";
        public Dictionary<string, string> Query { get; init; } = new();
        public Dictionary<string, string> Headers { get; init; } = new();
        public string Body { get; init; } = "";
    }
}
