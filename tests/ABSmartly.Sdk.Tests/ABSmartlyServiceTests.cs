using System.Net;
using System.Text;
using ABSmartly.Models;
using Microsoft.Extensions.Logging;

namespace ABSmartly.Sdk.Tests;

[TestFixture]
public class ABSmartlyServiceTests
{
    [SetUp]
    public void SetUp()
    {
        _httpClient = Mock.Of<IABsmartlyHttpClient>();
        _httpClientFactory = Mock.Of<IABsmartlyHttpClientFactory>(x => x.CreateClient() == _httpClient);
        _deserializer = Mock.Of<IContextDataDeserializer>();
        _serializer = Mock.Of<IContextEventSerializer>();
        _loggerFactory = Mock.Of<ILoggerFactory>();
    }

    private IABsmartlyHttpClientFactory _httpClientFactory = null!;
    private IContextDataDeserializer _deserializer = null!;
    private IContextEventSerializer _serializer = null!;
    private ILoggerFactory _loggerFactory = null!;
    private IABsmartlyHttpClient _httpClient = null!;

    [Test]
    public void TestCreateWithInvalidConfiguration()
    {
        Func<ABSmartlyService> act;

        act = () => new ABSmartlyService(null, _httpClientFactory, _deserializer, _serializer, _loggerFactory);
        act.Should().Throw<ArgumentNullException>()
            .WithMessage($"{nameof(ABSmartlyService)} config is required (Parameter 'config')");

        act = () => new ABSmartlyService(H.ServiceConfig("1", "1", "1", "1"), null, _deserializer, _serializer,
            _loggerFactory);
        act.Should().Throw<ArgumentNullException>()
            .WithMessage("HTTP client factory is required (Parameter 'httpClientFactory')");

        act = () => new ABSmartlyService(H.ServiceConfig("1", "1", "1", "1"), _httpClientFactory, null, _serializer,
            _loggerFactory);
        act.Should().Throw<ArgumentNullException>()
            .WithMessage("Data deserializer is required (Parameter 'dataDeserializer')");

        act = () => new ABSmartlyService(H.ServiceConfig("1", "1", "1", "1"), _httpClientFactory, _deserializer, null,
            _loggerFactory);
        act.Should().Throw<ArgumentNullException>()
            .WithMessage("Event serializer is required (Parameter 'eventSerializer')");

        act = () => new ABSmartlyService(H.ServiceConfig(null!, "1", "1", "1"), _httpClientFactory, _deserializer,
            _serializer, _loggerFactory);
        act.Should().Throw<ArgumentNullException>()
            .WithMessage("Missing Application configuration (Parameter 'Application')");

        act = () => new ABSmartlyService(H.ServiceConfig("1", null!, "1", "1"), _httpClientFactory, _deserializer,
            _serializer, _loggerFactory);
        act.Should().Throw<ArgumentNullException>()
            .WithMessage("Missing Endpoint configuration (Parameter 'Endpoint')");

        act = () => new ABSmartlyService(H.ServiceConfig("1", "1", null!, "1"), _httpClientFactory, _deserializer,
            _serializer, _loggerFactory);
        act.Should().Throw<ArgumentNullException>()
            .WithMessage("Missing Environment configuration (Parameter 'Environment')");

        act = () => new ABSmartlyService(H.ServiceConfig("1", "1", "1", null!), _httpClientFactory, _deserializer,
            _serializer, _loggerFactory);
        act.Should().Throw<ArgumentNullException>().WithMessage("Missing APIKey configuration (Parameter 'ApiKey')");
    }

    [Test]
    public void TestCreateWithValidConfiguration()
    {
        Func<ABSmartlyService> act;

        var httpsConfig = H.ServiceConfig("1", "https://test.example.com", "1", "1");

        act = () => new ABSmartlyService(httpsConfig, _httpClientFactory, _deserializer, _serializer);
        act.Should().NotThrow();

        act = () => new ABSmartlyService(httpsConfig, _httpClientFactory, _deserializer, _serializer, null);
        act.Should().NotThrow();

        act = () => new ABSmartlyService(httpsConfig, _httpClientFactory, _deserializer, _serializer, _loggerFactory);
        act.Should().NotThrow();

        var localhostConfig = H.ServiceConfig("1", "http://localhost:8080", "1", "1");
        act = () => new ABSmartlyService(localhostConfig, _httpClientFactory, _deserializer, _serializer);
        act.Should().NotThrow();

        var loopbackConfig = H.ServiceConfig("1", "http://127.0.0.1:8080", "1", "1");
        act = () => new ABSmartlyService(loopbackConfig, _httpClientFactory, _deserializer, _serializer);
        act.Should().NotThrow();

        var ipv6Config = H.ServiceConfig("1", "http://[::1]:3000", "1", "1");
        act = () => new ABSmartlyService(ipv6Config, _httpClientFactory, _deserializer, _serializer);
        act.Should().NotThrow();

        var remoteHttpConfig = H.ServiceConfig("1", "http://test.example.com", "1", "1");
        act = () => new ABSmartlyService(remoteHttpConfig, _httpClientFactory, _deserializer, _serializer);
        act.Should().NotThrow();

        var invalidSchemeConfig = H.ServiceConfig("1", "ftp://test.example.com", "1", "1");
        act = () => new ABSmartlyService(invalidSchemeConfig, _httpClientFactory, _deserializer, _serializer);
        act.Should().Throw<ArgumentException>();
    }

    [Test]
    public async Task TestGetContextData()
    {
        var endpoint = "https://localhost/v1";
        var uri = $"{endpoint}/context?application=website&environment=dev";

        var config = H.ServiceConfig("website", endpoint, "dev", "test-api-key");
        var service = new ABSmartlyService(config, _httpClientFactory, _deserializer, _serializer);

        var bytes = Encoding.UTF8.GetBytes("{}");

        Mock.Get(_httpClient).Setup(x => x.GetAsync(uri))
            .Returns(() => GetResponse(HttpStatusCode.OK, bytes));

        var expected = new ContextData();
        Mock.Get(_deserializer).Setup(x => x.Deserialize(It.IsAny<Stream>()))
            .Returns(() => expected);

        var actual = await service.GetContextDataAsync();

        actual.Should().BeSameAs(expected);
        actual.Should().BeEquivalentTo(expected);

        Mock.Get(_httpClientFactory).Verify(x => x.CreateClient(), Times.Once);
        Mock.Get(_httpClient).Verify(x => x.GetAsync(uri), Times.Once);
        Mock.Get(_deserializer).Verify(x => x.Deserialize(It.IsAny<Stream>()), Times.Once);
        VerifyDefaultHeaders(Mock.Get(_httpClient));
    }

    [Test]
    public async Task TestGetContextData_ConnectionError()
    {
        var endpoint = "https://localhost/v1";
        var uri = $"{endpoint}/context?application=website&environment=dev";

        var config = H.ServiceConfig("website", endpoint, "dev", "test-api-key");
        var service = new ABSmartlyService(config, _httpClientFactory, _deserializer, _serializer);

        Mock.Get(_httpClient).Setup(x => x.GetAsync(uri))
            .Throws(new HttpRequestException("connection refused"));

        // As 1.3.0, the public method returns null on a failed fetch.
        (await service.GetContextDataAsync()).Should().BeNull();

        // The SDK's own loading path surfaces the real cause (it becomes ReadyError).
        var act = async () => await service.FetchContextDataAsync();
        await act.Should().ThrowAsync<HttpRequestException>().WithMessage("connection refused");

        Mock.Get(_httpClientFactory).Verify(x => x.CreateClient(), Times.Exactly(2));
        Mock.Get(_httpClient).Verify(x => x.GetAsync(uri), Times.Exactly(2));
        Mock.Get(_deserializer).Verify(x => x.Deserialize(It.IsAny<Stream>()), Times.Never);
    }

    [Test]
    public async Task TestGetContextData_HttpError()
    {
        var endpoint = "https://localhost/v1";
        var uri = $"{endpoint}/context?application=website&environment=dev";

        var config = H.ServiceConfig("website", endpoint, "dev", "test-api-key");
        var service = new ABSmartlyService(config, _httpClientFactory, _deserializer, _serializer);

        Mock.Get(_httpClient).Setup(x => x.GetAsync(uri))
            .Returns(() => GetResponse(HttpStatusCode.BadRequest, Array.Empty<byte>()));

        (await service.GetContextDataAsync()).Should().BeNull("as 1.3.0, a failed fetch returns null");

        var act = async () => await service.FetchContextDataAsync();
        await act.Should().ThrowAsync<HttpRequestException>();

        Mock.Get(_httpClientFactory).Verify(x => x.CreateClient(), Times.Exactly(2));
        Mock.Get(_httpClient).Verify(x => x.GetAsync(uri), Times.Exactly(2));
        Mock.Get(_deserializer).Verify(x => x.Deserialize(It.IsAny<Stream>()), Times.Never);
    }

    [Test]
    public async Task TestGetContextData_DeserializerReturnsNull()
    {
        var endpoint = "https://localhost/v1";
        var uri = $"{endpoint}/context?application=website&environment=dev";

        var config = H.ServiceConfig("website", endpoint, "dev", "test-api-key");
        var service = new ABSmartlyService(config, _httpClientFactory, _deserializer, _serializer);

        Mock.Get(_httpClient).Setup(x => x.GetAsync(uri))
            .Returns(() => GetResponse(HttpStatusCode.OK, Encoding.UTF8.GetBytes("not json")));
        Mock.Get(_deserializer).Setup(x => x.Deserialize(It.IsAny<Stream>()))
            .Returns((ContextData)null!);

        (await service.GetContextDataAsync()).Should().BeNull();

        var act = async () => await service.FetchContextDataAsync();
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*deserializer returned null*");
    }

    [Test]
    public async Task TestGetContextData_MalformedResponseThrowsWithTheDefaultDeserializer()
    {
        // The default deserializer returns null on malformed JSON (as 1.3.0 and Java);
        // the service still fails the fetch, so ReadyError and Refresh see an error.
        var endpoint = "https://localhost/v1";
        var uri = $"{endpoint}/context?application=website&environment=dev";

        var config = H.ServiceConfig("website", endpoint, "dev", "test-api-key");
        var service = new ABSmartlyService(config, _httpClientFactory, new ABSmartly.Services.DefaultContextDataDeserializer(), _serializer);

        Mock.Get(_httpClient).Setup(x => x.GetAsync(uri))
            .Returns(() => GetResponse(HttpStatusCode.OK, Encoding.UTF8.GetBytes("{broken")));

        (await service.GetContextDataAsync()).Should().BeNull();

        var act = async () => await service.FetchContextDataAsync();
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*deserializer returned null*");
    }

    [Test]
    public async Task TestPublish()
    {
        var config = H.ServiceConfig("website", "https://localhost/v1", "dev", "test-api-key");
        var service = new ABSmartlyService(config, _httpClientFactory, _deserializer, _serializer);

        var publishEvent = new PublishEvent();
        var eventBytes = Array.Empty<byte>();

        Mock.Get(_serializer).Setup(x => x.Serialize(publishEvent)).Returns(eventBytes);
        Mock.Get(_httpClient).Setup(x => x.PutAsync("https://localhost/v1/context", It.IsAny<HttpContent>()))
            .Returns(() => GetResponse(HttpStatusCode.OK, Array.Empty<byte>()));

        var actual = await service.PublishAsync(publishEvent);

        actual.Should().BeTrue();

        Mock.Get(_httpClientFactory).Verify(x => x.CreateClient(), Times.Once);
        Mock.Get(_httpClient).Verify(x =>
            x.PutAsync("https://localhost/v1/context", It.IsAny<HttpContent>()), Times.Once);
        VerifyDefaultHeaders(Mock.Get(_httpClient));
    }

    [Test]
    public async Task TestPublish_ConnectionError()
    {
        var config = H.ServiceConfig("website", "https://localhost/v1", "dev", "test-api-key");
        var service = new ABSmartlyService(config, _httpClientFactory, _deserializer, _serializer);

        var publishEvent = new PublishEvent();
        var eventBytes = Array.Empty<byte>();

        Mock.Get(_serializer).Setup(x => x.Serialize(publishEvent)).Returns(eventBytes);
        Mock.Get(_httpClient).Setup(x => x.PutAsync("https://localhost/v1/context", It.IsAny<HttpContent>()))
            .Throws<Exception>();

        var actual = await service.PublishAsync(publishEvent);

        actual.Should().BeFalse();

        Mock.Get(_httpClientFactory).Verify(x => x.CreateClient(), Times.Once);
        Mock.Get(_httpClient).Verify(x =>
            x.PutAsync("https://localhost/v1/context", It.IsAny<HttpContent>()), Times.Once);
        VerifyDefaultHeaders(Mock.Get(_httpClient));
    }

    [Test]
    public async Task TestPublish_HttpError()
    {
        var config = H.ServiceConfig("website", "https://localhost/v1", "dev", "test-api-key");
        var service = new ABSmartlyService(config, _httpClientFactory, _deserializer, _serializer);

        var publishEvent = new PublishEvent();
        var eventBytes = Array.Empty<byte>();

        Mock.Get(_serializer).Setup(x => x.Serialize(publishEvent)).Returns(eventBytes);
        Mock.Get(_httpClient).Setup(x => x.PutAsync("https://localhost/v1/context", It.IsAny<HttpContent>()))
            .Returns(() => GetResponse(HttpStatusCode.BadRequest, Array.Empty<byte>()));

        var actual = await service.PublishAsync(publishEvent);

        actual.Should().BeFalse();

        Mock.Get(_httpClientFactory).Verify(x => x.CreateClient(), Times.Once);
        Mock.Get(_httpClient).Verify(x =>
            x.PutAsync("https://localhost/v1/context", It.IsAny<HttpContent>()), Times.Once);
        VerifyDefaultHeaders(Mock.Get(_httpClient));
    }

    // A failed publish is marked retryable only when a retry can succeed: HTTP 408, 429 or 5xx, or
    // a transport failure. Another 4xx, or an event the serializer could not write, would fail again.
    [TestCase(500, true)]
    [TestCase(503, true)]
    [TestCase(408, true)]
    [TestCase(429, true)]
    [TestCase(400, false)]
    [TestCase(401, false)]
    [TestCase(404, false)]
    public async Task FailedPublishIsRetryableOnlyWhenARetryCanSucceed(int status, bool retryable)
    {
        var service = new ABSmartlyService(H.ServiceConfig("website", "https://localhost/v1", "dev", "test-api-key"),
            _httpClientFactory, _deserializer, _serializer);
        var publishEvent = new PublishEvent();
        Mock.Get(_serializer).Setup(x => x.Serialize(publishEvent)).Returns(Array.Empty<byte>());
        Mock.Get(_httpClient).Setup(x => x.PutAsync("https://localhost/v1/context", It.IsAny<HttpContent>()))
            .Returns(() => GetResponse((HttpStatusCode)status, Array.Empty<byte>()));

        (await service.PublishAsync(publishEvent)).Should().BeFalse();
        publishEvent.RetryableFailure.Should().Be(retryable);
    }

    [Test]
    public async Task TransportFailureIsRetryableButASerializerFailureIsNot()
    {
        var service = new ABSmartlyService(H.ServiceConfig("website", "https://localhost/v1", "dev", "test-api-key"),
            _httpClientFactory, _deserializer, _serializer);
        var sent = new PublishEvent();
        Mock.Get(_serializer).Setup(x => x.Serialize(sent)).Returns(Array.Empty<byte>());
        Mock.Get(_httpClient).Setup(x => x.PutAsync("https://localhost/v1/context", It.IsAny<HttpContent>()))
            .ThrowsAsync(new HttpRequestException("connection refused"));
        (await service.PublishAsync(sent)).Should().BeFalse();
        sent.RetryableFailure.Should().BeTrue();

        var unwritable = new PublishEvent();
        Mock.Get(_serializer).Setup(x => x.Serialize(unwritable)).Throws(new InvalidOperationException("cycle"));
        (await service.PublishAsync(unwritable)).Should().BeFalse();
        unwritable.RetryableFailure.Should().BeFalse();

        var nullBytes = new PublishEvent();
        Mock.Get(_serializer).Setup(x => x.Serialize(nullBytes)).Returns((byte[])null!);
        (await service.PublishAsync(nullBytes)).Should().BeFalse();
        nullBytes.RetryableFailure.Should().BeFalse();
    }

    private void VerifyDefaultHeaders(Mock<IABsmartlyHttpClient> httpClientMock)
    {
        httpClientMock.Verify(x => x.AddHeader("X-API-Key", "test-api-key"));
        httpClientMock.Verify(x => x.AddHeader("X-Application", "website"));
        httpClientMock.Verify(x => x.AddHeader("X-Environment", "dev"));
        httpClientMock.Verify(x => x.AddHeader("X-Application-Version", "0"));
        httpClientMock.Verify(x => x.AddHeader("X-Agent", "absmartly-dotnet-sdk/" + PackageVersion()));
    }

    // The <PackageVersion> in the SDK project, which X-Agent must carry.
    private static string PackageVersion()
    {
        var csproj = File.ReadAllText(Path.Combine(TestContext.CurrentContext.TestDirectory,
            "..", "..", "..", "..", "..", "src", "ABSmartly.Sdk", "ABSmartly.Sdk.csproj"));
        var match = System.Text.RegularExpressions.Regex.Match(csproj, "<PackageVersion>([^<]+)</PackageVersion>");
        match.Success.Should().BeTrue();
        return match.Groups[1].Value;
    }

    // The collector binds X-Application-Version as a Long and rejects anything else with HTTP 400,
    // so only a whole number of 1-18 ASCII digits (after trimming) is sent; anything else is "0",
    // as the Java SDK's Client.applicationVersionHeader().
    [TestCase(null, "0")]
    [TestCase("", "0")]
    [TestCase("   ", "0")]
    [TestCase("1004", "1004")]
    [TestCase(" 1004 ", "1004")]
    [TestCase("3.2.1", "0")]
    [TestCase("-5", "0")]
    [TestCase("+5", "0")]
    [TestCase("1e3", "0")]
    [TestCase("\u0661\u0662", "0")]
    [TestCase("123456789012345678", "123456789012345678")]
    [TestCase("1234567890123456789", "0")]
    public async Task ApplicationVersionHeaderIsAWholeNumberOnGetAndPut(string? configured, string expected)
    {
        var endpoint = "https://localhost/v1";
        var config = H.ServiceConfig("website", endpoint, "dev", "test-api-key");
        config.ApplicationVersion = configured!;
        var service = new ABSmartlyService(config, _httpClientFactory, _deserializer, _serializer);

        var getClient = Mock.Of<IABsmartlyHttpClient>();
        var putClient = Mock.Of<IABsmartlyHttpClient>();
        Mock.Get(_httpClientFactory).SetupSequence(x => x.CreateClient()).Returns(getClient).Returns(putClient);

        Mock.Get(getClient).Setup(x => x.GetAsync($"{endpoint}/context?application=website&environment=dev"))
            .Returns(() => GetResponse(HttpStatusCode.OK, Encoding.UTF8.GetBytes("{}")));
        Mock.Get(_deserializer).Setup(x => x.Deserialize(It.IsAny<Stream>())).Returns(new ContextData());
        (await service.GetContextDataAsync()).Should().NotBeNull();

        var publishEvent = new PublishEvent();
        Mock.Get(_serializer).Setup(x => x.Serialize(publishEvent)).Returns(Array.Empty<byte>());
        Mock.Get(putClient).Setup(x => x.PutAsync($"{endpoint}/context", It.IsAny<HttpContent>()))
            .Returns(() => GetResponse(HttpStatusCode.OK, Array.Empty<byte>()));
        (await service.PublishAsync(publishEvent)).Should().BeTrue();

        foreach (var client in new[] { getClient, putClient })
        {
            Mock.Get(client).Verify(x => x.AddHeader("X-Application-Version", expected), Times.Once);
            Mock.Get(client).Verify(x => x.AddHeader("X-Application-Version", It.Is<string>(v => v != expected)),
                Times.Never);
        }
    }

    [Test]
    public void AgentIsSdkNameSlashVersion()
    {
        ABSmartly.Internal.SdkInfo.Agent.Should().MatchRegex(@"^absmartly-dotnet-sdk/\d+\.\d+\.\d+");
        ABSmartly.Internal.SdkInfo.Version.Should().Be(PackageVersion());
    }

    private Task<HttpResponseMessage> GetResponse(HttpStatusCode statusCode, byte[] bytes)
    {
        return Task.FromResult(new HttpResponseMessage(statusCode)
        {
            Content = new ByteArrayContent(bytes)
        });
    }
}