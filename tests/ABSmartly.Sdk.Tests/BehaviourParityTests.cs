using System.Globalization;
using ABSmartly.Models;
using ABSmartly.Services;
using ABSmartly.Time;
using Microsoft.Extensions.Logging;

namespace ABSmartly.Sdk.Tests;

// Regression tests for behaviour that must match 1.3.0 and the Java SDK, and for
// bugs found in review of the 1.4.0 changes.
[TestFixture]
public class BehaviourParityTests
{
    private readonly Clock _clock = Clock.Fixed(1_620_000_000_000L);
    private readonly Dictionary<string, string> _units = new() { ["session_id"] = "e791e240fcd3df7d238cfc285f475e8152fcc0ec" };

    private ContextData _data = null!;
    private IContextDataProvider _dataProvider = null!;
    private IContextPublisher _publisher = null!;
    private IContextEventLogger _eventLogger = null!;
    private IVariableParser _variableParser = null!;
    private AudienceMatcher _audienceMatcher = null!;

    [SetUp]
    public void SetUp()
    {
        using var contextStream =
            GetType().Assembly.GetManifestResourceStream("ABSmartly.Sdk.Tests.Resources.context.json");
        _data = new DefaultContextDataDeserializer().Deserialize(contextStream);

        _dataProvider = Mock.Of<IContextDataProvider>();
        _publisher = Mock.Of<IContextPublisher>();
        _eventLogger = Mock.Of<IContextEventLogger>();
        _variableParser = new DefaultVariableParser();
        _audienceMatcher = new AudienceMatcher(new DefaultAudienceDeserializer());
    }

    private Context CreateContext(ContextData data, ContextConfig? config = null) =>
        new(config ?? new ContextConfig { PublishDelay = TimeSpan.FromSeconds(60) }.SetUnits(_units), data, _clock,
            _dataProvider, _publisher, _eventLogger, _variableParser, _audienceMatcher, new LoggerFactory());

    private static ContextData DataWithCustomFields(params CustomFieldValue[] fields) =>
        new(new[]
        {
            new Experiment
            {
                Id = 1, Name = "exp", UnitType = "session_id", Iteration = 1,
                Split = new[] { 0.5, 0.5 }, TrafficSplit = new[] { 0.0, 1.0 },
                Variants = new[] { new ExperimentVariant { Name = "A" }, new ExperimentVariant { Name = "B" } },
                CustomFieldValues = fields
            }
        });

    #region Custom fields

    [TestCase("de-DE")]
    [TestCase("pt-BR")]
    [TestCase("fr-FR")]
    [TestCase("en-US")]
    public void NumberCustomFieldsParseWithInvariantCulture(string culture)
    {
        var previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo(culture);

            var context = CreateContext(DataWithCustomFields(
                new CustomFieldValue { Name = "ratio", Type = "number", Value = "1.5" },
                new CustomFieldValue { Name = "count", Type = "number", Value = "42" },
                new CustomFieldValue { Name = "grouped", Type = "number", Value = "1,5" }));

            context.GetCustomFieldValue("exp", "ratio").Should().Be(1.5);
            context.GetCustomFieldValue("exp", "count").Should().Be(42L);
            // A culture-specific decimal comma is not a JSON number: not silently read as 15.
            context.GetCustomFieldValue("exp", "grouped").Should().BeNull();
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }

    [Test]
    public void JsonCustomFieldsGoThroughTheConfiguredVariableParser()
    {
        var parsed = new Dictionary<string, object> { ["from"] = "custom parser" };
        var parser = new Mock<IVariableParser>();
        parser.Setup(p => p.Parse(It.IsAny<IContext>(), "exp", "settings", "{\"a\":1}")).Returns(parsed);
        _variableParser = parser.Object;

        var context = CreateContext(DataWithCustomFields(
            new CustomFieldValue { Name = "settings", Type = "json", Value = "{\"a\":1}" }));

        context.GetCustomFieldValue("exp", "settings").Should().BeSameAs(parsed);
        parser.Verify(p => p.Parse(It.IsAny<IContext>(), "exp", "settings", "{\"a\":1}"), Times.Once);
    }

    #endregion

    #region Attributes

    [Test]
    public void AttributeReadsAreSafeAgainstConcurrentSetAttribute()
    {
        var context = CreateContext(_data);
        using var stop = new CancellationTokenSource();
        Exception? readError = null;

        var reader = Task.Run(() =>
        {
            try
            {
                while (!stop.IsCancellationRequested)
                {
                    _ = context.Attributes;
                    _ = context.GetAttribute("attr");
                }
            }
            catch (Exception e)
            {
                readError = e;
            }
        });

        for (var i = 0; i < 20_000 && readError == null; i++)
            context.SetAttribute("attr", i);

        stop.Cancel();
        reader.Wait(TimeSpan.FromSeconds(10)).Should().BeTrue();

        readError.Should().BeNull();
        context.GetAttribute("attr").Should().NotBeNull();
    }

    [Test]
    public void AttributesStayReadableAfterDispose()
    {
        var context = CreateContext(_data);
        context.SetAttribute("a", 1);
        context.SetAttribute("a", 2);
        context.Dispose();

        context.GetAttribute("a").Should().Be(2);
        context.Attributes.Should().BeEquivalentTo(new Dictionary<string, object> { ["a"] = 2 });
    }

    #endregion

    #region Audience

    private sealed class CountingAudienceDeserializer : IAudienceDeserializer
    {
        private readonly DefaultAudienceDeserializer _inner = new();
        public int Calls;

        public Dictionary<string, object> Deserialize(string audience)
        {
            Interlocked.Increment(ref Calls);
            return _inner.Deserialize(audience);
        }
    }

    [Test]
    public void AudienceIsNotReEvaluatedOnEveryCallAfterSetAttribute()
    {
        var counting = new CountingAudienceDeserializer();
        _audienceMatcher = new AudienceMatcher(counting);
        var data = DataWithCustomFields();
        data.Experiments[0].Audience = "{\"filter\":[{\"gte\":[{\"var\":\"age\"},{\"value\":20}]}]}";

        var context = CreateContext(data);
        context.SetAttribute("age", 30);

        var variant = context.GetTreatment("exp");
        counting.Calls.Should().Be(1, "the first assignment evaluates the audience");

        context.SetAttribute("age", 31);
        context.GetTreatment("exp").Should().Be(variant);
        counting.Calls.Should().Be(2, "a changed attribute re-checks the audience once");

        for (var i = 0; i < 10; i++)
        {
            context.GetTreatment("exp").Should().Be(variant);
            context.PeekTreatment("exp").Should().Be(variant);
        }
        counting.Calls.Should().Be(2, "the revalidated assignment is cached until the next SetAttribute");

        // A change that flips the audience result still invalidates the assignment.
        context.SetAttribute("age", 10);
        context.GetTreatment("exp");
        context.PendingCount.Should().Be(2, "the mismatched assignment is a new exposure");
    }

    #endregion

}
