using ABSmartlySdk;
using ABSmartlySdk.DefaultServiceImplementations;
using Moq;

namespace ABSmartly.DotNet.Standard.UnitTests.DefaultServiceImplementationsTests;

[TestFixture]
public class DefaultContextEventHandlerTests
{
    [Test]
    public void Publish_Verify()
    {
        var client = new Mock<IABSmartlyServiceClient>();

        var contextEventHandler = new DefaultContextEventHandler(client.Object);

        contextEventHandler.PublishAsync(null, null);

        client.Verify(a => a.PublishAsync(null));
    }
}