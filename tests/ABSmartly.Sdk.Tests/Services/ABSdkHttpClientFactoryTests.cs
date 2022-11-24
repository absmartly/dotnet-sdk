using ABSmartly.Services;

namespace ABSmartly.Sdk.Tests.Services;

[TestFixture]
public class ABSdkHttpClientFactoryTests
{
    [Test]
    public void CreatesClientWithSdkName()
    {
        var httpClientFactory = Mock.Of<IHttpClientFactory>();
        var sdkClient = new ABSdkHttpClientFactory(httpClientFactory);

        var _ = sdkClient.CreateClient();

        Mock.Get(httpClientFactory).Verify(x => x.CreateClient(ABSdk.HttpClientName), Times.Once);
    }
}