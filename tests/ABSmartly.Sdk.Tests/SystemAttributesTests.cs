using System.Text;
using ABSmartly.Models;
using ABSmartly.Services;
using ABSmartly.Time;
using Microsoft.Extensions.Logging;
using Attribute = ABSmartly.Models.Attribute;

namespace ABSmartly.Sdk.Tests;

// Audiences and assignment conditions must always see the client's `application` and
// `environment`, overlaid by any user-set attribute, without the SDK publishing anything
// extra. `app_version` is never injected. Mirrors the javascript-sdk reference
// (feat/sdk-system-attributes) adapted to this SDK, which evaluates `audience` filters
// only (no separate assignment-rule conditions) and never had an `app_version`.
[TestFixture]
public class SystemAttributesTests
{
    private static readonly TimeSpan PublishDelay = TimeSpan.FromSeconds(60);

    private readonly Dictionary<string, string> _units = new()
    {
        ["session_id"] = "e791e240fcd3df7d238cfc285f475e8152fcc0ec"
    };

    private readonly Clock _clock = Clock.Fixed(1_620_000_000_000L);

    private IContextDataProvider _dataProvider = null!;
    private IContextEventLogger _eventLogger = null!;
    private IContextPublisher _eventHandler = null!;
    private IVariableParser _variableParser = null!;
    private AudienceMatcher _audienceMatcher = null!;

    [SetUp]
    public void SetUp()
    {
        _dataProvider = Mock.Of<IContextDataProvider>();
        _eventLogger = Mock.Of<IContextEventLogger>();
        _eventHandler = Mock.Of<IContextPublisher>();
        _variableParser = new DefaultVariableParser();
        _audienceMatcher = new AudienceMatcher(new DefaultAudienceDeserializer());
    }

    // A single 0/100 experiment that gives variant 1 (B) unless an enforced audience
    // mismatch in strict mode serves control. `audience` is a filter expression.
    private static ContextData AudienceData(string audience, bool strict)
    {
        var json = @"{
  ""experiments"": [
    {
      ""id"": 1,
      ""name"": ""exp_test_ab"",
      ""iteration"": 1,
      ""unitType"": ""session_id"",
      ""seedHi"": 3603515,
      ""seedLo"": 233373850,
      ""split"": [0.0, 1.0],
      ""trafficSeedHi"": 449867249,
      ""trafficSeedLo"": 455443629,
      ""trafficSplit"": [0.0, 1.0],
      ""fullOnVariant"": 0,
      ""applications"": [{ ""name"": ""website"" }],
      ""variants"": [
        { ""name"": ""A"", ""config"": null },
        { ""name"": ""B"", ""config"": ""{\""banner.size\"":\""large\""}"" }
      ],
      ""audienceStrict"": " + (strict ? "true" : "false") + @",
      ""audience"": " + System.Text.Json.JsonSerializer.Serialize(audience) + @"
    }
  ]
}";
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));
        return new DefaultContextDataDeserializer().Deserialize(stream);
    }

    private Context CreateContext(ContextData data,
        IReadOnlyDictionary<string, object> systemAttributes,
        ContextConfig config = null)
    {
        config ??= new ContextConfig { PublishDelay = PublishDelay }.SetUnits(_units);
        return new Context(config, data, _clock, _dataProvider, _eventHandler, _eventLogger, _variableParser,
            _audienceMatcher, new LoggerFactory())
        {
            SystemAttributes = systemAttributes
        };
    }

    private static Dictionary<string, object> SystemAttributes(string application, string environment)
    {
        var attrs = new Dictionary<string, object>();
        if (!string.IsNullOrEmpty(application)) attrs["application"] = application;
        if (!string.IsNullOrEmpty(environment)) attrs["environment"] = environment;
        return attrs;
    }

    private const string ProductionEnvironmentFilter =
        "{\"filter\":[{\"eq\":[{\"var\":\"environment\"},{\"value\":\"production\"}]}]}";
    private const string StagingEnvironmentFilter =
        "{\"filter\":[{\"eq\":[{\"var\":\"environment\"},{\"value\":\"staging\"}]}]}";
    private const string WebsiteApplicationFilter =
        "{\"filter\":[{\"eq\":[{\"var\":\"application\"},{\"value\":\"website\"}]}]}";
    private const string AppVersionFilter =
        "{\"filter\":[{\"eq\":[{\"var\":\"app_version\"},{\"value\":\"1.2.3\"}]}]}";

    // Test 1: a strict audience on the environment matches, so the unit is randomised
    // into variant 1 (B). On the pre-patch behaviour (no system attributes) this would
    // be an audience mismatch and serve control — the regression this pins.
    [Test]
    public void StrictAudienceOnEnvironmentMatches()
    {
        var context = CreateContext(AudienceData(ProductionEnvironmentFilter, strict: true),
            SystemAttributes("website", "production"));

        context.GetTreatment("exp_test_ab").Should().Be(1);
    }

    [Test]
    public void StrictAudienceOnEnvironmentServesControlWithoutSystemAttributes()
    {
        // Pre-patch behaviour: nothing supplies `environment`, so the strict audience
        // mismatches and the unit gets control.
        var context = CreateContext(AudienceData(ProductionEnvironmentFilter, strict: true),
            systemAttributes: null);

        context.GetTreatment("exp_test_ab").Should().Be(0);
    }

    // Test 2: a strict audience on the application matches.
    [Test]
    public void StrictAudienceOnApplicationMatches()
    {
        var context = CreateContext(AudienceData(WebsiteApplicationFilter, strict: true),
            SystemAttributes("website", "production"));

        context.GetTreatment("exp_test_ab").Should().Be(1);
    }

    // Test 3: no match — a strict audience on a different environment serves control and
    // reports an audience mismatch on the exposure.
    [Test]
    public void StrictAudienceNoMatchServesControlAndReportsMismatch()
    {
        var context = CreateContext(AudienceData(StagingEnvironmentFilter, strict: true),
            SystemAttributes("website", "production"));

        context.GetTreatment("exp_test_ab").Should().Be(0);

        PublishEvent captured = null;
        Mock.Get(_eventHandler)
            .Setup(x => x.PublishAsync(It.IsAny<IContext>(), It.IsAny<PublishEvent>()))
            .Callback<IContext, PublishEvent>((_, e) => captured = e)
            .Returns(Task.CompletedTask);

        context.Publish();

        captured.Should().NotBeNull();
        captured!.Exposures.Should().ContainSingle();
        captured.Exposures[0].AudienceMismatch.Should().BeTrue();
    }

    [Test]
    public void StrictAudienceMatchReportsNoMismatch()
    {
        var context = CreateContext(AudienceData(ProductionEnvironmentFilter, strict: true),
            SystemAttributes("website", "production"));

        context.GetTreatment("exp_test_ab").Should().Be(1);

        PublishEvent captured = null;
        Mock.Get(_eventHandler)
            .Setup(x => x.PublishAsync(It.IsAny<IContext>(), It.IsAny<PublishEvent>()))
            .Callback<IContext, PublishEvent>((_, e) => captured = e)
            .Returns(Task.CompletedTask);

        context.Publish();

        captured.Should().NotBeNull();
        captured!.Exposures[0].AudienceMismatch.Should().BeFalse();
    }

    // Test 4: a user-set `environment` overrides the system value, even when set after
    // the context is created. A staging filter then matches and a production filter no
    // longer does. The assignment cached before the attribute was set is invalidated.
    [Test]
    public void UserAttributeOverridesSystemValueForStagingFilter()
    {
        var context = CreateContext(AudienceData(StagingEnvironmentFilter, strict: true),
            SystemAttributes("website", "production"));

        // Before the override the staging filter does not match the "production" system
        // value: control.
        context.PeekTreatment("exp_test_ab").Should().Be(0);

        context.SetAttribute("environment", "staging");

        // The user value wins and the staging filter now matches: variant 1.
        context.PeekTreatment("exp_test_ab").Should().Be(1);
    }

    [Test]
    public void UserAttributeOverridesSystemValueForProductionFilter()
    {
        var context = CreateContext(AudienceData(ProductionEnvironmentFilter, strict: true),
            SystemAttributes("website", "production"));

        // Cache a matching assignment first.
        context.PeekTreatment("exp_test_ab").Should().Be(1);

        // Overriding to staging invalidates it: the production filter no longer matches.
        context.SetAttribute("environment", "staging");

        context.PeekTreatment("exp_test_ab").Should().Be(0);
    }

    // Test 6: no environment configured means no `environment` key in the evaluation map,
    // so a filter on it does not match — and nothing crashes.
    [Test]
    public void MissingEnvironmentConfigLeavesKeyOutAndFilterDoesNotMatch()
    {
        var context = CreateContext(AudienceData(ProductionEnvironmentFilter, strict: true),
            SystemAttributes("website", environment: null));

        context.GetTreatment("exp_test_ab").Should().Be(0);
    }

    // Test 7: the serialized publish payload gets no `application`/`environment`/
    // `app_version` attribute entries. Checked on the serialized bytes, not an in-memory
    // object. The collector attaches `application`/`environment` server-side.
    [Test]
    public void SystemAttributesAreNotPublished()
    {
        var context = CreateContext(AudienceData(ProductionEnvironmentFilter, strict: true),
            SystemAttributes("website", "production"));

        context.GetTreatment("exp_test_ab").Should().Be(1);

        PublishEvent captured = null;
        Mock.Get(_eventHandler)
            .Setup(x => x.PublishAsync(It.IsAny<IContext>(), It.IsAny<PublishEvent>()))
            .Callback<IContext, PublishEvent>((_, e) => captured = e)
            .Returns(Task.CompletedTask);

        context.Publish();

        captured.Should().NotBeNull();

        var serialized = Encoding.UTF8.GetString(new DefaultContextEventSerializer().Serialize(captured));

        serialized.Should().NotContain("application");
        serialized.Should().NotContain("environment");
        serialized.Should().NotContain("app_version");
    }

    [Test]
    public void UserSetSystemNameStillPublishedAsNormalAttribute()
    {
        // A user who deliberately sets `environment` as an attribute still has it
        // published (it is a real attribute); only the injected system values are
        // publish-free.
        var context = CreateContext(AudienceData(ProductionEnvironmentFilter, strict: true),
            SystemAttributes("website", "production"));
        context.SetAttribute("environment", "staging");

        context.GetTreatment("exp_test_ab").Should().Be(0);

        PublishEvent captured = null;
        Mock.Get(_eventHandler)
            .Setup(x => x.PublishAsync(It.IsAny<IContext>(), It.IsAny<PublishEvent>()))
            .Callback<IContext, PublishEvent>((_, e) => captured = e)
            .Returns(Task.CompletedTask);

        context.Publish();

        captured.Should().NotBeNull();
        captured!.Attributes.Should().ContainSingle(a => a.Name == "environment" && (string)a.Value == "staging");
    }

    // Test 8: `app_version` is never injected, so a filter on it does not match even
    // when an application version is otherwise configured.
    [Test]
    public void AppVersionIsNeverInjected()
    {
        var context = CreateContext(AudienceData(AppVersionFilter, strict: true),
            SystemAttributes("website", "production"));

        context.GetTreatment("exp_test_ab").Should().Be(0);
    }

    // End-to-end: the application/environment from the service configuration flow through
    // ABsmartly into the context's audience evaluation, with nothing extra to configure.
    [Test]
    public void ServiceConfigApplicationAndEnvironmentReachAudienceEvaluation()
    {
        var data = AudienceData(WebsiteApplicationFilter, strict: true);

        var httpClient = Mock.Of<IABsmartlyHttpClient>();
        var httpClientFactory = Mock.Of<IABsmartlyHttpClientFactory>(x => x.CreateClient() == httpClient);
        var serviceConfig = H.ServiceConfig("website", "http://localhost/v1", "production", "test-api-key");

        var dataProvider = Mock.Of<IContextDataProvider>();
        Mock.Get(dataProvider).Setup(x => x.GetContextDataAsync()).Returns(Task.FromResult(data));

        var absmartly = new ABsmartly(httpClientFactory, serviceConfig,
            new ABsmartlyConfig { ContextDataProvider = dataProvider, ContextPublisher = _eventHandler });

        var config = new ContextConfig { PublishDelay = PublishDelay }.SetUnits(_units);
        var context = absmartly.CreateContextWith(config, data);

        context.GetTreatment("exp_test_ab").Should().Be(1);
    }
}
