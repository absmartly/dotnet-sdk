using ABSmartlySdk.DefaultServiceImplementations;
using ABSmartlySdk.Interfaces;
using Moq;

namespace ABSmartly.DotNet.Standard.UnitTests.DefaultServiceImplementationsTests;

[TestFixture]
public class DefaultContextEventHandlerTests
{
    [Test]
    public void Publish_Verify()
    {
        var client = new Mock<IClient>();

        var contextEventHandler = new DefaultContextEventHandler(client.Object);

        contextEventHandler.PublishAsync(null, null);

        client.Verify(a => a.PublishAsync(null));
    }
}