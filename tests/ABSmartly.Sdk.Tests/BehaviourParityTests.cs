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

    #endregion
}
