namespace ABSmartly.Sdk.Tests;

[TestFixture]
public class HttpClientConfigTests
{
    [Test]
    public void TestDefaults()
    {
        var config = HttpClientConfig.CreateDefault();

        config.ConnectTimeout.Should().Be(3000);
        config.ConnectionKeepAlive.Should().Be(30000);
        config.RetryInterval.Should().Be(333);
        config.MaxRetries.Should().Be(5);
    }
}