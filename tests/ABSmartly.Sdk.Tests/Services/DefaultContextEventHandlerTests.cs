using ABSmartly.Models;
using ABSmartly.Services;

namespace ABSmartly.Sdk.Tests.Services;

[TestFixture]
public class DefaultContextEventHandlerTests
{
    private PublishEvent _event = null!;
    private IABSmartlyServiceClient _client = null!;
    private DefaultContextEventHandler _handler = null!;
    private IContext _context = null!;

    [SetUp]
    public void SetUp()
    {
        _event = Mock.Of<PublishEvent>();
        _client = Mock.Of<IABSmartlyServiceClient>();
        _handler = new DefaultContextEventHandler(_client);
        _context = Mock.Of<IContext>();
    }
    
    [Test]
    public async Task TestCallsServiceClient()
    {
        await _handler.PublishAsync(_context, _event);

        Mock.Get(_client).Verify(x => x.PublishAsync(_event), Times.Once);
    }
    
    [Test]
    public async Task TestThrowsIfServiceClientThrows()
    {
        var exception = new Exception();
        Mock.Get(_client).Setup(x => x.PublishAsync(It.IsAny<PublishEvent>())).Throws(exception);
        var act = async () => await _handler.PublishAsync(_context, _event);

        await act.Should().ThrowExactlyAsync<Exception>();
        
        Mock.Get(_client).Verify(x => x.PublishAsync(_event), Times.Once);
    }
}