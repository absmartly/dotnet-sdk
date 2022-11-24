using ABSmartly.Services;

namespace ABSmartly.Sdk.Tests.Services;

[TestFixture]
public class DefaultContextDataProviderTests
{
    [Test]
    public async Task TestCallsServiceClient()
    {
        var client = Mock.Of<IABSmartlyServiceClient>();
        var provider = new DefaultContextDataProvider(client);

        await provider.GetContextDataAsync();

        Mock.Get(client).Verify(x => x.GetContextDataAsync(), Times.Once);
    }
    
    [Test]
    public async Task TestThrowsIfServiceClientThrows()
    {
        var exception = new Exception();
        var client = Mock.Of<IABSmartlyServiceClient>();
        Mock.Get(client).Setup(x => x.GetContextDataAsync()).Throws(exception);
        var provider = new DefaultContextDataProvider(client);

        var act = async () => await provider.GetContextDataAsync();

        await act.Should().ThrowExactlyAsync<Exception>();
        
        Mock.Get(client).Verify(x => x.GetContextDataAsync(), Times.Once);
    }
}