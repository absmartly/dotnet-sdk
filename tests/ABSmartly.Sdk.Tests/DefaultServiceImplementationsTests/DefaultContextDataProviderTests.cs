using ABSmartlySdk;
using ABSmartlySdk.DefaultServiceImplementations;
using ABSmartlySdk.Json;
using Moq;

namespace ABSmartly.DotNet.Standard.UnitTests.DefaultServiceImplementationsTests;

[TestFixture]
public class DefaultContextDataProviderTests
{
    [Test]
    public void GetContextDataAsync_Returns_ExpectedResult()
    {
        var client = new Mock<IABSmartlyServiceClient>();
        client.Setup(q => q.GetContextDataAsync()).ReturnsAsync(new ContextData(new Experiment[]
        {
            null,
            null,
            null
        }));

        var contextProvider = new DefaultContextDataProvider(client.Object);

        var result = contextProvider.GetContextDataAsync();

        Assert.That(result.Result.Experiments.Length, Is.EqualTo(3));
    }
}