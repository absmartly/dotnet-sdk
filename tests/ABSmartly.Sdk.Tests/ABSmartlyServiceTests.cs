using System.Net;
using System.Text;
using ABSmartly.Models;
using Microsoft.Extensions.Logging;

namespace ABSmartly.Sdk.Tests;

[TestFixture]
public class ABSmartlyServiceTests
{
    private IABSdkHttpClientFactory _httpClientFactory = null!;
    private IContextDataDeserializer _deserializer = null!;
    private IContextEventSerializer _serializer = null!;
    private ILoggerFactory _loggerFactory = null!;
    private IABSdkHttpClient _httpClient = null!;

    [SetUp]
    public void SetUp()
    {
        _httpClient = Mock.Of<IABSdkHttpClient>();
        _httpClientFactory = Mock.Of<IABSdkHttpClientFactory>(x => x.CreateClient() == _httpClient);
        _deserializer = Mock.Of<IContextDataDeserializer>();
        _serializer = Mock.Of<IContextEventSerializer>();
        _loggerFactory = Mock.Of<ILoggerFactory>();
    }
    
    [Test]
    public void TestCreateWithInvalidConfiguration()
    {
        Func<ABSmartlyService> act;

        act = () => new ABSmartlyService(null, _httpClientFactory, _deserializer, _serializer, _loggerFactory);
        act.Should().Throw<ArgumentNullException>()
            .WithMessage($"{nameof(ABSmartlyService)} config is required (Parameter 'config')");
        
        act = () => new ABSmartlyService(H.ServiceConfig("1", "1", "1", "1"), null, _deserializer, _serializer, _loggerFactory);
        act.Should().Throw<ArgumentNullException>().WithMessage("HTTP client factory is required (Parameter 'httpClientFactory')");
        
        act = () => new ABSmartlyService(H.ServiceConfig("1", "1", "1", "1"), _httpClientFactory, null, _serializer, _loggerFactory);
        act.Should().Throw<ArgumentNullException>().WithMessage("Data deserializer is required (Parameter 'dataDeserializer')");
        
        act = () => new ABSmartlyService(H.ServiceConfig("1", "1", "1", "1"), _httpClientFactory, _deserializer, null, _loggerFactory);
        act.Should().Throw<ArgumentNullException>().WithMessage("Event serializer is required (Parameter 'eventSerializer')");

        act = () => new ABSmartlyService(H.ServiceConfig(null!, "1", "1", "1"), _httpClientFactory, _deserializer, _serializer, _loggerFactory);
        act.Should().Throw<ArgumentNullException>().WithMessage("Missing Application configuration (Parameter 'Application')");
        
        act = () => new ABSmartlyService(H.ServiceConfig("1", null!, "1", "1"), _httpClientFactory, _deserializer, _serializer, _loggerFactory);
        act.Should().Throw<ArgumentNullException>().WithMessage("Missing Endpoint configuration (Parameter 'Endpoint')");
        
        act = () => new ABSmartlyService(H.ServiceConfig("1", "1", null!, "1"), _httpClientFactory, _deserializer, _serializer, _loggerFactory);
        act.Should().Throw<ArgumentNullException>().WithMessage("Missing Environment configuration (Parameter 'Environment')");
        
        act = () => new ABSmartlyService(H.ServiceConfig("1", "1", "1", null!), _httpClientFactory, _deserializer, _serializer, _loggerFactory);
        act.Should().Throw<ArgumentNullException>().WithMessage("Missing APIKey configuration (Parameter 'ApiKey')");
    }

    [Test]
    public void TestCreateWithValidConfiguration()
    {
        Func<ABSmartlyService> act;

        var config = H.ServiceConfig("1", "1", "1", "1");

        act = () => new ABSmartlyService(config, _httpClientFactory, _deserializer, _serializer);
        act.Should().NotThrow();

        act = () => new ABSmartlyService(config, _httpClientFactory, _deserializer, _serializer, loggerFactory: null);
        act.Should().NotThrow();
        
        act = () => new ABSmartlyService(config, _httpClientFactory, _deserializer, _serializer, _loggerFactory);
        act.Should().NotThrow();
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
    }

    [Test]
    public async Task TestGetContextData_ConnectionError()
    {
        var endpoint = "https://localhost/v1";
        var uri = $"{endpoint}/context?application=website&environment=dev";
        
        var config = H.ServiceConfig("website", endpoint, "dev", "test-api-key");
        var service = new ABSmartlyService(config, _httpClientFactory, _deserializer, _serializer);

        var bytes = Encoding.UTF8.GetBytes("{}");

        Mock.Get(_httpClient).Setup(x => x.GetAsync(uri))
            .Throws<Exception>();

        var actual = await service.GetContextDataAsync();

        actual.Should().BeNull();

        Mock.Get(_httpClientFactory).Verify(x => x.CreateClient(), Times.Once);
        Mock.Get(_httpClient).Verify(x => x.GetAsync(uri), Times.Once);
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

        var actual = await service.GetContextDataAsync();

        actual.Should().BeNull();

        Mock.Get(_httpClientFactory).Verify(x => x.CreateClient(), Times.Once);
        Mock.Get(_httpClient).Verify(x => x.GetAsync(uri), Times.Once);
        Mock.Get(_deserializer).Verify(x => x.Deserialize(It.IsAny<Stream>()), Times.Never);
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
    
    #region Helpers

    private void VerifyDefaultHeaders(Mock<IABSdkHttpClient> httpClientMock)
    {
        httpClientMock.Verify(x => x.AddHeader("X-API-Key", "test-api-key"));
        httpClientMock.Verify(x => x.AddHeader("X-Application", "website"));
        httpClientMock.Verify(x => x.AddHeader("X-Environment", "dev"));
        httpClientMock.Verify(x => x.AddHeader("X-Application-Version", "0"));
        httpClientMock.Verify(x => x.AddHeader("X-Agent", "absmartly-dotnet-sdk"));
    }
    
    private Task<HttpResponseMessage> GetResponse(HttpStatusCode statusCode, byte[] bytes) =>
        Task.FromResult<HttpResponseMessage>(new(statusCode)
        {
            Content = new ByteArrayContent(bytes)
        });
    
    #endregion
}