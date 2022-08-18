using ABSmartly.DefaultServiceImplementations;
using ABSmartly.Json;
using Moq;

namespace ABSmartly.DotNet.Standard.UnitTests.DefaultServiceImplementationsTests;

[TestFixture]
public class DefaultContextDataProviderTests
{
    [Test]
    public void Test()
    {
        var client = new Mock<IClient>();
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