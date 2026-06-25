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

    [SetUp]
    public void SetUp()
    {
        // Bind to a free ephemeral port on the loopback interface.
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
            // ignored - listener already torn down
        }
    }

    [Test]
    public async Task DrivesRealHttpClient_Get_And_Put_Context()
    {
        var sdk = BuildSdk();

        // 1. createContext -> SDK fires a REAL GET /context and deserializes the body.
        var config = new ContextConfig();
        config.SetUnit("session_id", "bleh@absmartly.com");

        // CreateContextAsync awaits the data fetch, so the GET completes here and the
        // context is ready once it returns.
        var context = await sdk.CreateContextAsync(config);
        context.IsReady().Should().BeTrue("the context should be ready after the GET completes");

        // Assert the GET landed with the right path + query params.
        var get = WaitForRequest(r => r.Method == "GET");
        get.Should().NotBeNull("the SDK should have performed a real GET /context");
        get!.Path.Should().Be("/context");
        get.Query.Should().ContainKey("application").WhoseValue.Should().Be(Application);
        get.Query.Should().ContainKey("environment").WhoseValue.Should().Be(Environment);

        // 2. Drive a treatment (queues an exposure) + track (queues a goal), then publish
        //    -> SDK fires a REAL PUT /context with the serialized event body.
        context.GetTreatment("exp_test_ab");
        context.Track("payment", new Dictionary<string, object> { ["amount"] = 100 });

        await context.PublishAsync();

        var put = WaitForRequest(r => r.Method == "PUT");
        put.Should().NotBeNull("publish should have performed a real PUT /context");
        put!.Path.Should().Be("/context");
        put.Query.Should().BeEmpty("publish must not append query params");

        // Headers (exact names per wire contract).
        put.Headers.Should().ContainKey("X-API-Key").WhoseValue.Should().Be(ApiKey);
        put.Headers.Should().ContainKey("X-Application").WhoseValue.Should().Be(Application);
        put.Headers.Should().ContainKey("X-Environment").WhoseValue.Should().Be(Environment);
        put.Headers.Should().ContainKey("X-Application-Version").WhoseValue.Should().Be("0");
        put.Headers.Should().ContainKey("X-Agent");
        put.Headers["X-Agent"].Should().NotBeNullOrEmpty();
        put.Headers.Should().ContainKey("Content-Type");
        put.Headers["Content-Type"].Should().StartWith("application/json");

        // Body JSON fields.
        var body = JObject.Parse(put.Body);
        body.Should().ContainKey("hashed");
        body.Should().ContainKey("units");
        body.Should().ContainKey("publishedAt");

        body["units"].Should().BeOfType<JArray>();
        ((JArray)body["units"]!).Count.Should().BeGreaterThan(0, "the published units must be present");

        // Goal we tracked must be present in the goals array.
        body.Should().ContainKey("goals");
        body["goals"].Should().BeOfType<JArray>();
        ((JArray)body["goals"]!).Count.Should().BeGreaterThan(0, "the tracked goal must be published");
    }

    // Builds the SDK using ONLY the public configuration surface, wiring the SDK's
    // REAL ABsmartlyHttpClientFactory over a REAL IHttpClientFactory so a genuine
    // System.Net.Http.HttpClient performs the requests.
    private ABsmartly BuildSdk()
    {
        var services = new ServiceCollection();
        services.AddHttpClient(ABsmartly.HttpClientName);
        services.AddTransient<IABsmartlyHttpClientFactory, ABsmartlyHttpClientFactory>();
        var provider = services.BuildServiceProvider();

        var httpClientFactory = provider.GetRequiredService<IABsmartlyHttpClientFactory>();

        var serviceConfig = new ABSmartlyServiceConfiguration
        {
            Endpoint = _baseUrl,
            ApiKey = ApiKey,
            Application = Application,
            Environment = Environment
        };

        return new ABsmartly(httpClientFactory, serviceConfig);
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
                payload = Encoding.UTF8.GetBytes("{\"experiments\":[]}");
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
