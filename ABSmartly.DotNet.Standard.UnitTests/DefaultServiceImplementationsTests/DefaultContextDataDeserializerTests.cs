using System.Text;
using ABSmartly.DefaultServiceImplementations;

namespace ABSmartly.DotNet.Standard.UnitTests.DefaultServiceImplementationsTests;

[TestFixture]
public class DefaultContextDataDeserializerTests
{
    [Test]
    public void Deserialize_Returns_ExpectedResult()
    {
        // Arrange
        var deserializer = new DefaultContextDataDeserializer(null);
        var contextBytes = Encoding.UTF8.GetBytes(TestData.Json.Context);

        // Act
        var resultContextData = deserializer.Deserialize(contextBytes, 0, contextBytes.Length);

        // Assert
        var experiment1 = resultContextData.Experiments[0];
        var experiment2 = resultContextData.Experiments[1];
        var experiment3 = resultContextData.Experiments[2];
        var experiment4 = resultContextData.Experiments[3];

        // Do 'lazy assertation' - checking different part of the experiment object for each experiment

        // Experiment 1
        Assert.That(experiment1.Id, Is.EqualTo(1));
        Assert.That(experiment1.Name, Is.EqualTo("exp_test_ab"));
        Assert.That(experiment1.Iteration, Is.EqualTo(1));
        Assert.That(experiment1.UnitType, Is.EqualTo("session_id"));
        Assert.That(experiment1.SeedHi, Is.EqualTo(3603515));
        Assert.That(experiment1.SeedLo, Is.EqualTo(233373850));

        // Experiment 2
        Assert.That(experiment2.Split[0], Is.EqualTo(0.34));
        Assert.That(experiment2.Split[1], Is.EqualTo(0.33));
        Assert.That(experiment2.Split[2], Is.EqualTo(0.33));
        Assert.That(experiment2.TrafficSeedHi, Is.EqualTo(705671872));
        Assert.That(experiment2.TrafficSeedLo, Is.EqualTo(212903484));

        // Experiment 3
        Assert.That(experiment3.TrafficSplit[0], Is.EqualTo(0.99));
        Assert.That(experiment3.TrafficSplit[1], Is.EqualTo(0.01));
        Assert.That(experiment3.FullOnVariant, Is.EqualTo(0));
        Assert.That(experiment3.Applications[0].Name, Is.EqualTo("website"));
        Assert.That(experiment3.Audience, Is.EqualTo("{}"));

        // Experiment 4
        Assert.That(experiment4.Variants[0].Name, Is.EqualTo("A"));
        Assert.That(experiment4.Variants[0].Config, Is.EqualTo(null));
        Assert.That(experiment4.Variants[1].Name, Is.EqualTo("B"));
        Assert.That(experiment4.Variants[1].Config, Is.EqualTo("{\"submit.color\":\"red\",\"submit.shape\":\"circle\"}"));
        Assert.That(experiment4.Variants[2].Name, Is.EqualTo("C"));
        Assert.That(experiment4.Variants[2].Config, Is.EqualTo("{\"submit.color\":\"blue\",\"submit.shape\":\"rect\"}"));
        Assert.That(experiment4.Variants[3].Name, Is.EqualTo("D"));
        Assert.That(experiment4.Variants[3].Config, Is.EqualTo("{\"submit.color\":\"green\",\"submit.shape\":\"square\"}"));
    }

    [Test]
    public void Deserialize_InvalidValue_Returns_Null()
    {
        // Arrange
        var deserializer = new DefaultContextDataDeserializer(null);
        var contextBytes = Encoding.UTF8.GetBytes(TestData.Json.Context);

        // Act
        // 'ruin' the bytes with random offset and length..
        var resultContextData = deserializer.Deserialize(contextBytes, 0 + 1, contextBytes.Length - 4);

        // Assert
        Assert.That(resultContextData, Is.Null);
    }
}