using System.Text;
using System.Text.Json;
using ABSmartly.Models;
using ABSmartly.Services;
using ABSmartly.Services.Json;
using ABSmartly.Time;
using Microsoft.Extensions.Logging;
using Attribute = ABSmartly.Models.Attribute;

namespace ABSmartly.Sdk.Tests;

[TestFixture]
public class LargePayloadTests
{
    [SetUp]
    public void SetUp()
    {
        _dataProvider = Mock.Of<IContextDataProvider>();
        _eventLogger = Mock.Of<IContextEventLogger>();
        _eventHandler = Mock.Of<IContextEventHandler>();
        _variableParser = new DefaultVariableParser();
        _audienceMatcher = new AudienceMatcher(new DefaultAudienceDeserializer());
    }

    private readonly Dictionary<string, string> _units = new()
    {
        ["session_id"] = "e791e240fcd3df7d238cfc285f475e8152fcc0ec",
        ["user_id"] = "123456789"
    };

    private readonly Clock _clock = Clock.Fixed(1_620_000_000_000L);

    private IContextDataProvider _dataProvider = null!;
    private IContextEventLogger _eventLogger = null!;
    private IContextEventHandler _eventHandler = null!;
    private IVariableParser _variableParser = null!;
    private AudienceMatcher _audienceMatcher = null!;

    [Test]
    public void TestLargeContextDataHandling()
    {
        var experimentCount = 100;
        var experiments = new Experiment[experimentCount];

        for (var i = 0; i < experimentCount; i++)
        {
            experiments[i] = new Experiment
            {
                Id = i + 1,
                Name = $"exp_test_{i}",
                UnitType = "session_id",
                Iteration = 1,
                SeedHi = 3603515 + i,
                SeedLo = 233373850 + i,
                Split = new[] { 0.5, 0.5 },
                TrafficSeedHi = 449867249,
                TrafficSeedLo = 455443629,
                TrafficSplit = new[] { 0.0, 1.0 },
                FullOnVariant = 0,
                Applications = new[] { new ExperimentApplication { Name = "website" } },
                Variants = new[]
                {
                    new ExperimentVariant { Name = "A", Config = null },
                    new ExperimentVariant { Name = "B", Config = $"{{\"variable_{i}\":\"value_{i}\"}}" }
                }
            };
        }

        var data = new ContextData(experiments);

        var config = new ContextConfig().SetUnits(_units);
        var context = CreateContext(config, data);

        context.IsReady().Should().BeTrue();
        context.IsFailed().Should().BeFalse();
        context.GetExperiments().Length.Should().Be(experimentCount);

        for (var i = 0; i < experimentCount; i++)
        {
            var treatment = context.GetTreatment($"exp_test_{i}");
            treatment.Should().BeGreaterThanOrEqualTo(0);
            treatment.Should().BeLessThanOrEqualTo(1);
        }

        context.PendingCount.Should().Be(experimentCount);
    }

    [Test]
    public void TestLargeEventBatching()
    {
        var experiment = new Experiment
        {
            Id = 1,
            Name = "exp_test_ab",
            UnitType = "session_id",
            Iteration = 1,
            SeedHi = 3603515,
            SeedLo = 233373850,
            Split = new[] { 0.5, 0.5 },
            TrafficSeedHi = 449867249,
            TrafficSeedLo = 455443629,
            TrafficSplit = new[] { 0.0, 1.0 },
            FullOnVariant = 0,
            Applications = new[] { new ExperimentApplication { Name = "website" } },
            Variants = new[]
            {
                new ExperimentVariant { Name = "A", Config = null },
                new ExperimentVariant { Name = "B", Config = "{\"banner.size\":\"large\"}" }
            }
        };

        var data = new ContextData(new[] { experiment });

        var config = new ContextConfig().SetUnits(_units);
        config.PublishDelay = TimeSpan.FromMinutes(10);
        var context = CreateContext(config, data);

        context.IsReady().Should().BeTrue();
        context.IsFailed().Should().BeFalse();

        var goalCount = 1000;
        for (var i = 0; i < goalCount; i++)
        {
            context.Track($"goal_{i}", new Dictionary<string, object>
            {
                ["amount"] = i * 10,
                ["item_count"] = i,
                ["timestamp"] = _clock.Millis() + i
            });
        }

        context.PendingCount.Should().Be(goalCount);

        PublishEvent capturedEvent = null!;
        Mock.Get(_eventHandler)
            .Setup(x => x.PublishAsync(It.IsAny<IContext>(), It.IsAny<PublishEvent>()))
            .Callback<IContext, PublishEvent>((_, e) => capturedEvent = e)
            .Returns(Task.CompletedTask);

        context.Publish();

        capturedEvent.Should().NotBeNull();
        capturedEvent.Goals.Should().NotBeNull();
        capturedEvent.Goals.Length.Should().Be(goalCount);

        for (var i = 0; i < goalCount; i++)
        {
            capturedEvent.Goals[i].Name.Should().Be($"goal_{i}");
        }

        context.PendingCount.Should().Be(0);
    }

    [Test]
    public void TestMemoryEfficientDeserialization()
    {
        var experimentCount = 50;
        var variantsPerExperiment = 10;

        var jsonBuilder = new StringBuilder();
        jsonBuilder.Append("{\"experiments\":[");

        for (var i = 0; i < experimentCount; i++)
        {
            if (i > 0) jsonBuilder.Append(',');

            jsonBuilder.Append($@"{{
                ""id"":{i + 1},
                ""name"":""exp_large_{i}"",
                ""iteration"":1,
                ""unitType"":""session_id"",
                ""seedHi"":{3603515 + i},
                ""seedLo"":{233373850 + i},
                ""split"":[");

            var splitValue = 1.0 / variantsPerExperiment;
            for (var v = 0; v < variantsPerExperiment; v++)
            {
                if (v > 0) jsonBuilder.Append(',');
                jsonBuilder.Append(splitValue.ToString("F6", System.Globalization.CultureInfo.InvariantCulture));
            }

            jsonBuilder.Append($@"],
                ""trafficSeedHi"":449867249,
                ""trafficSeedLo"":455443629,
                ""trafficSplit"":[0.0,1.0],
                ""fullOnVariant"":0,
                ""applications"":[{{""name"":""website""}}],
                ""variants"":[");

            for (var v = 0; v < variantsPerExperiment; v++)
            {
                if (v > 0) jsonBuilder.Append(',');

                var variantName = v == 0 ? "Control" : $"Variant_{v}";
                var config = v == 0 ? "null" : "\"" + "{" + $"\\\"var_{i}_{v}\\\":\\\"value_{i}_{v}\\\"" + "}" + "\"";
                jsonBuilder.Append("{\"name\":\"" + variantName + "\",\"config\":" + config + "}");
            }

            jsonBuilder.Append("],\"audience\":null}");
        }

        jsonBuilder.Append("]}");

        var json = jsonBuilder.ToString();
        json.Length.Should().BeGreaterThan(10000);

        var deserializer = new DefaultContextDataDeserializer();
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));
        var data = deserializer.Deserialize(stream);

        data.Should().NotBeNull();
        data.Experiments.Should().NotBeNull();
        data.Experiments.Length.Should().Be(experimentCount);

        foreach (var exp in data.Experiments)
        {
            exp.Variants.Should().NotBeNull();
            exp.Variants.Length.Should().Be(variantsPerExperiment);
        }
    }

    [Test]
    public async Task TestStreamingResponseHandling()
    {
        var experimentCount = 25;
        var experiments = new Experiment[experimentCount];

        for (var i = 0; i < experimentCount; i++)
        {
            experiments[i] = new Experiment
            {
                Id = i + 1,
                Name = $"exp_streaming_{i}",
                UnitType = "session_id",
                Iteration = 1,
                SeedHi = 1000 + i,
                SeedLo = 2000 + i,
                Split = new[] { 0.5, 0.5 },
                TrafficSeedHi = 3000,
                TrafficSeedLo = 4000,
                TrafficSplit = new[] { 0.0, 1.0 },
                FullOnVariant = 0,
                Applications = new[] { new ExperimentApplication { Name = "website" } },
                Variants = new[]
                {
                    new ExperimentVariant { Name = "A", Config = null },
                    new ExperimentVariant { Name = "B", Config = $"{{\"streaming_var_{i}\":true}}" }
                }
            };
        }

        var json = JsonSerializer.Serialize(new ContextData(experiments), JsonOptionsProvider.Default.SerializerOptions);

        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));

        var deserializer = new DefaultContextDataDeserializer();
        var data = deserializer.Deserialize(stream);

        data.Should().NotBeNull();
        data.Experiments.Should().NotBeNull();
        data.Experiments.Length.Should().Be(experimentCount);

        var config = new ContextConfig().SetUnits(_units);
        var context = CreateContext(config, data);

        context.IsReady().Should().BeTrue();

        var refreshedExperiments = new Experiment[experimentCount + 5];
        Array.Copy(experiments, refreshedExperiments, experimentCount);

        for (var i = 0; i < 5; i++)
        {
            refreshedExperiments[experimentCount + i] = new Experiment
            {
                Id = experimentCount + i + 1,
                Name = $"exp_new_{i}",
                UnitType = "session_id",
                Iteration = 1,
                SeedHi = 5000 + i,
                SeedLo = 6000 + i,
                Split = new[] { 0.5, 0.5 },
                TrafficSeedHi = 7000,
                TrafficSeedLo = 8000,
                TrafficSplit = new[] { 0.0, 1.0 },
                FullOnVariant = 0,
                Applications = new[] { new ExperimentApplication { Name = "website" } },
                Variants = new[]
                {
                    new ExperimentVariant { Name = "A", Config = null },
                    new ExperimentVariant { Name = "B", Config = "{\"new_var\":true}" }
                }
            };
        }

        var refreshedData = new ContextData(refreshedExperiments);
        Mock.Get(_dataProvider)
            .Setup(x => x.GetContextDataAsync())
            .ReturnsAsync(refreshedData);

        await context.RefreshAsync();

        context.GetExperiments().Length.Should().Be(experimentCount + 5);
    }

    private Context CreateContext(ContextConfig config, ContextData data) =>
        new(config, data, _clock, _dataProvider, _eventHandler, _eventLogger, _variableParser,
            _audienceMatcher, new LoggerFactory());
}
