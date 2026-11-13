using System.Text;
using System.Text.Json;
using ABSmartly.Models;
using ABSmartly.Services;
using ABSmartly.Time;
using Microsoft.Extensions.Logging;

namespace ABSmartly.Sdk.Tests;

// Assignment rules (`assign` and `split`), ported from javascript-sdk#66 and java-sdk#17. The
// fixture is context.json, whose exp_test_abc has the same seeds and session unit as the
// JavaScript fixture: the unit hashes to ~0.748 with the experiment seed, and normal assignment
// gives variant 2.
[TestFixture]
public class ContextAssignmentRulesTests
{
    private const string Experiment = "exp_test_abc";
    private const string SessionId = "e791e240fcd3df7d238cfc285f475e8152fcc0ec";
    private const string UserId = "123456789";
    private const int NormalVariant = 2;

    private const string UsCondition = "{\"and\":[{\"eq\":[{\"var\":\"country\"},{\"value\":\"US\"}]}]}";

    private readonly Clock _clock = Clock.Fixed(1_620_000_000_000L);

    private IContextDataProvider _dataProvider = null!;
    private IContextEventLogger _eventLogger = null!;
    private IContextPublisher _publisher = null!;
    private List<PublishEvent> _published = null!;

    [SetUp]
    public void SetUp()
    {
        _dataProvider = Mock.Of<IContextDataProvider>();
        _eventLogger = Mock.Of<IContextEventLogger>();
        _publisher = Mock.Of<IContextPublisher>();
        _published = new List<PublishEvent>();
        Mock.Get(_publisher)
            .Setup(x => x.PublishAsync(It.IsAny<IContext>(), It.IsAny<PublishEvent>()))
            .Callback<IContext, PublishEvent>((_, e) => _published.Add(e))
            .Returns(Task.CompletedTask);
    }

    private ContextData Data(string assignmentRules, Action<Experiment>? configure = null)
    {
        using var stream = GetType().Assembly.GetManifestResourceStream("ABSmartly.Sdk.Tests.Resources.context.json");
        var data = new DefaultContextDataDeserializer().Deserialize(stream!);
        var experiment = data.Experiments.Single(e => e.Name == Experiment);
        experiment.AssignmentRules = assignmentRules;
        configure?.Invoke(experiment);
        return data;
    }

    private static string Rules(params string[] rules) => "{\"rules\":[" + string.Join(",", rules) + "]}";

    private static string Assign(int variant, string? conditions = UsCondition, string environments = "[]") =>
        "{\"name\":\"assign\",\"type\":\"assign\",\"variant\":" + variant + ",\"conditions\":" +
        (conditions ?? "null") + ",\"environments\":" + environments + "}";

    private static string Split(string percentages, string? conditions = UsCondition, string environments = "[]") =>
        "{\"name\":\"split\",\"type\":\"split\",\"percentages\":" + JsonSerializer.Serialize(percentages) +
        ",\"conditions\":" + (conditions ?? "null") + ",\"environments\":" + environments + "}";

    private Context CreateContext(ContextData data, bool withSession = true, string? environment = "production")
    {
        var config = new ContextConfig { PublishDelay = TimeSpan.FromSeconds(60) }.SetUnit("user_id", UserId);
        if (withSession) config.SetUnit("session_id", SessionId);

        var systemAttributes = new Dictionary<string, object> { ["application"] = "website" };
        if (environment != null) systemAttributes["environment"] = environment;

        return Context.CreateWithData(config, data, _clock, _dataProvider, _publisher, _eventLogger,
            new DefaultVariableParser(), new AudienceMatcher(new DefaultAudienceDeserializer()), new LoggerFactory(),
            systemAttributes);
    }

    private List<Exposure> PublishedExposures(Context context)
    {
        context.Publish();
        return _published.SelectMany(e => e.Exposures ?? Array.Empty<Exposure>())
            .Where(e => e.Name == Experiment).ToList();
    }

    [Test]
    public void AssignRuleSetsTheVariantWhenConditionsMatch()
    {
        var context = CreateContext(Data(Rules(Assign(1))));
        context.SetAttribute("country", "US");

        context.GetTreatment(Experiment).Should().Be(1);
    }

    [Test]
    public void NormalAssignmentWhenNoRuleMatches()
    {
        var context = CreateContext(Data(Rules(Assign(1))));
        context.SetAttribute("country", "GB");

        context.GetTreatment(Experiment).Should().Be(NormalVariant);
    }

    [Test]
    public void AssignRuleExposureIsFlaggedRuleOverride()
    {
        var context = CreateContext(Data(Rules(Assign(1))));
        context.SetAttribute("country", "US");
        context.GetTreatment(Experiment);

        var exposure = PublishedExposures(context).Single();
        exposure.Variant.Should().Be(1);
        exposure.RuleOverride.Should().BeTrue();
        exposure.Assigned.Should().BeFalse();
        exposure.Eligible.Should().BeTrue();
        exposure.Overridden.Should().BeFalse();
        exposure.Custom.Should().BeFalse();
        exposure.FullOn.Should().BeFalse();
        exposure.AudienceMismatch.Should().BeFalse();
    }

    [Test]
    public void NormalExposureIsNotFlaggedRuleOverride()
    {
        var context = CreateContext(Data(Rules(Assign(1))));
        context.GetTreatment(Experiment);

        PublishedExposures(context).Single().RuleOverride.Should().BeFalse();
    }

    [Test]
    public void RuleOverrideIsSerializedOnTheExposure()
    {
        var context = CreateContext(Data(Rules(Assign(1))));
        context.SetAttribute("country", "US");
        context.GetTreatment(Experiment);
        context.Publish();

        var json = Encoding.UTF8.GetString(new DefaultContextEventSerializer().Serialize(_published.Single()));
        json.Should().Contain("\"ruleOverride\":true");
    }

    [Test]
    public void OverrideWinsOverRules()
    {
        var context = CreateContext(Data(Rules(Assign(1))));
        context.SetAttribute("country", "US");
        context.SetOverride(Experiment, 0);

        context.GetTreatment(Experiment).Should().Be(0);
        PublishedExposures(context).Single().Overridden.Should().BeTrue();
    }

    [Test]
    public void RuleWinsOverCustomAssignment()
    {
        var context = CreateContext(Data(Rules(Assign(1))));
        context.SetAttribute("country", "US");
        context.SetCustomAssignment(Experiment, 2);

        context.GetTreatment(Experiment).Should().Be(1);
    }

    // The bug fixed in javascript-sdk#66: a rule that wins over a different custom assignment
    // was treated as stale on every call, rebuilt and exposed again.
    [Test]
    public void RuleThatWinsOverCustomAssignmentIsExposedOnlyOnce()
    {
        var context = CreateContext(Data(Rules(Assign(1))));
        context.SetAttribute("country", "US");
        context.SetCustomAssignment(Experiment, 2);

        context.GetTreatment(Experiment).Should().Be(1);
        context.GetTreatment(Experiment).Should().Be(1);
        context.PendingCount.Should().Be(1);

        context.SetAttribute("country", "GB");
        context.GetTreatment(Experiment).Should().Be(2);
    }

    [Test]
    public void RuleWinsOverStrictAudienceMismatch()
    {
        var context = CreateContext(Data(Rules(Assign(1)), e =>
        {
            e.AudienceStrict = true;
            e.Audience = "{\"filter\":[{\"eq\":[{\"var\":\"country\"},{\"value\":\"GB\"}]}]}";
        }));
        context.SetAttribute("country", "US");

        context.GetTreatment(Experiment).Should().Be(1);
        PublishedExposures(context).Single().AudienceMismatch.Should().BeFalse();
    }

    [Test]
    public void RuleWinsOverTrafficEligibility()
    {
        var context = CreateContext(Data(Rules(Assign(1)), e => e.TrafficSplit = new[] { 1.0, 0.0 }));
        context.SetAttribute("country", "US");

        context.GetTreatment(Experiment).Should().Be(1);
        PublishedExposures(context).Single().Eligible.Should().BeTrue();
    }

    [Test]
    public void RuleWinsOverFullOn()
    {
        var context = CreateContext(Data(Rules(Assign(1)), e => e.FullOnVariant = 2));
        context.SetAttribute("country", "US");

        context.GetTreatment(Experiment).Should().Be(1);
        PublishedExposures(context).Single().FullOn.Should().BeFalse();
    }

    [Test]
    public void FirstMatchingRuleWins()
    {
        var context = CreateContext(Data(Rules(Assign(1), Assign(2, conditions: null))));
        context.SetAttribute("country", "US");
        context.PeekTreatment(Experiment).Should().Be(1);

        var other = CreateContext(Data(Rules(Assign(1), Assign(0, conditions: null))));
        other.SetAttribute("country", "GB");
        other.PeekTreatment(Experiment).Should().Be(0);
    }

    [Test]
    public void OutOfRangeAssignVariantFallsBackWithoutTryingLaterRules()
    {
        var context = CreateContext(Data(Rules(Assign(3, conditions: null), Assign(1, conditions: null))));

        context.GetTreatment(Experiment).Should().Be(NormalVariant);
        PublishedExposures(context).Single().RuleOverride.Should().BeFalse();
    }

    [Test]
    public void EnvironmentScopedRuleMatchesOnlyItsEnvironment()
    {
        var rules = Rules(Assign(1, conditions: null, environments: "[\"production\"]"));

        CreateContext(Data(rules), environment: "production").PeekTreatment(Experiment).Should().Be(1);
        CreateContext(Data(rules), environment: "staging").PeekTreatment(Experiment).Should().Be(NormalVariant);
        CreateContext(Data(rules), environment: null).PeekTreatment(Experiment).Should().Be(NormalVariant);
    }

    [Test]
    public void RuleConditionsSeeSystemAttributes()
    {
        var condition = "{\"eq\":[{\"var\":\"application\"},{\"value\":\"website\"}]}";
        var context = CreateContext(Data(Rules(Assign(1, condition))));

        context.PeekTreatment(Experiment).Should().Be(1);
    }

    [Test]
    public void RuleConditionsSupportSemverOperators()
    {
        var condition = "{\"semver_gte\":[{\"var\":\"app_version\"},{\"value\":\"2.0.0\"}]}";
        var data = Data(Rules(Assign(1, condition)));

        var newer = CreateContext(data);
        newer.SetAttribute("app_version", "v2.10.0");
        newer.PeekTreatment(Experiment).Should().Be(1);

        var older = CreateContext(data);
        older.SetAttribute("app_version", "2.0.0-beta.1");
        older.PeekTreatment(Experiment).Should().Be(NormalVariant);
    }

    [Test]
    public void AttributeChangeReevaluatesRules()
    {
        var context = CreateContext(Data(Rules(Assign(1))));
        context.PeekTreatment(Experiment).Should().Be(NormalVariant);

        context.SetAttribute("country", "US");
        context.PeekTreatment(Experiment).Should().Be(1);
    }

    [Test]
    public void RefreshThatChangesTheRulesReassigns()
    {
        var context = CreateContext(Data(Rules(Assign(1))));
        context.SetAttribute("country", "US");
        context.PeekTreatment(Experiment).Should().Be(1);

        Mock.Get(_dataProvider).Setup(x => x.GetContextDataAsync()).ReturnsAsync(Data(Rules(Assign(0))));
        context.Refresh();

        context.PeekTreatment(Experiment).Should().Be(0);
    }

    [Test]
    public void InvalidRulesJsonIsIgnored()
    {
        var context = CreateContext(Data("not json"));

        context.GetTreatment(Experiment).Should().Be(NormalVariant);
    }

    [Test]
    public void NonStringAssignmentRulesInPayloadAreIgnoredForThatExperimentOnly()
    {
        using var stream = GetType().Assembly.GetManifestResourceStream("ABSmartly.Sdk.Tests.Resources.context.json");
        var json = new StreamReader(stream!).ReadToEnd();
        json = json.Replace("\"name\":\"exp_test_abc\",",
            "\"name\":\"exp_test_abc\",\"assignmentRules\":{\"rules\":[]},");
        json.Should().Contain("\"assignmentRules\":{");

        var data = new DefaultContextDataDeserializer().Deserialize(new MemoryStream(Encoding.UTF8.GetBytes(json)));

        data.Should().NotBeNull();
        data.Experiments.Single(e => e.Name == Experiment).AssignmentRules.Should().BeNull();
        data.Experiments.Should().HaveCount(4);
    }

    [Test]
    public void AssignmentRulesStringIsDeserialized()
    {
        var rules = Rules(Assign(1));
        var json = "{\"experiments\":[{\"id\":1,\"name\":\"x\",\"assignmentRules\":" + JsonSerializer.Serialize(rules) + "}]}";

        var data = new DefaultContextDataDeserializer().Deserialize(new MemoryStream(Encoding.UTF8.GetBytes(json)));

        data.Experiments[0].AssignmentRules.Should().Be(rules);
    }

    [Test]
    public void RuleWonAssignmentWinsVariableResolution()
    {
        var context = CreateContext(Data(Rules(Assign(1))));
        context.SetAttribute("country", "US");

        // exp_test_abc variant 1 sets button.color to "blue".
        context.GetVariableValue("button.color", "default").Should().Be("blue");
    }

    // Split rules.

    [Test]
    public void SplitRuleHashesTheUnitWithTheExperimentSeed()
    {
        var context = CreateContext(Data(Rules(Split("70/10/20"))));
        context.SetAttribute("country", "US");

        context.GetTreatment(Experiment).Should().Be(1);
    }

    [Test]
    public void SplitRuleAssignsTheOnlyVariantWithANonZeroShare()
    {
        var context = CreateContext(Data(Rules(Split("0/100/0"))));
        context.SetAttribute("country", "US");

        context.GetTreatment(Experiment).Should().Be(1);
    }

    [Test]
    public void SplitRuleEqualToTheExperimentSplitKeepsTheNormalVariant()
    {
        var context = CreateContext(Data(Rules(Split("34/33/33"))));
        context.SetAttribute("country", "US");

        context.GetTreatment(Experiment).Should().Be(NormalVariant);
        PublishedExposures(context).Single().RuleOverride.Should().BeTrue();
    }

    [Test]
    public void SplitRuleSetsTheSameExposureFlagsAsAnAssignRule()
    {
        var context = CreateContext(Data(Rules(Split("0/100/0"))));
        context.SetAttribute("country", "US");
        context.GetTreatment(Experiment);

        var exposure = PublishedExposures(context).Single();
        exposure.Should().BeEquivalentTo(new Exposure
        {
            Id = 2,
            Name = Experiment,
            Unit = "session_id",
            Variant = 1,
            ExposedAt = _clock.Millis(),
            Assigned = false,
            Eligible = true,
            Overridden = false,
            FullOn = false,
            Custom = false,
            AudienceMismatch = false,
            RuleOverride = true
        });
    }

    [Test]
    public void SplitRuleTakesPriorityOverCustomAssignment()
    {
        var context = CreateContext(Data(Rules(Split("0/100/0"))));
        context.SetAttribute("country", "US");
        context.SetCustomAssignment(Experiment, 2);

        context.GetTreatment(Experiment).Should().Be(1);
    }

    [Test]
    public void InvalidSplitIsSkippedForTheNextRule()
    {
        var context = CreateContext(Data(Rules(Split("50/40"), Assign(1))));
        context.SetAttribute("country", "US");

        context.PeekTreatment(Experiment).Should().Be(1);
    }

    // An out-of-range percentage parses as infinity (double.Parse throws on .NET Framework), so the
    // split fails the sum check and is skipped.
    [Test]
    public void SplitWithAnOverflowingPercentageIsSkipped()
    {
        AudienceMatcher.ParseFloat("1e999").Should().Be(double.PositiveInfinity);
        AudienceMatcher.ParseFloat("-1e999").Should().Be(double.NegativeInfinity);

        var context = CreateContext(Data(Rules(Split("1e999/0/0"), Assign(1, conditions: null))));
        context.SetAttribute("country", "US");

        context.GetTreatment(Experiment).Should().Be(1);
    }

    [Test]
    public void SplitNotCoveringEveryVariantFallsBackWithoutTryingLaterRules()
    {
        var context = CreateContext(Data(Rules(Split("0/100"), Assign(1, conditions: null))));
        context.SetAttribute("country", "US");

        context.GetTreatment(Experiment).Should().Be(NormalVariant);
    }

    [Test]
    public void SplitRuleGivesVariantZeroWhileTheUnitIsMissingThenReresolvesOnceItIsSet()
    {
        var context = CreateContext(Data(Rules(Split("0/100/0"), Assign(2, conditions: null))), withSession: false);
        context.SetAttribute("country", "US");
        context.PeekTreatment(Experiment).Should().Be(0);

        context.SetUnit("session_id", SessionId);
        context.PeekTreatment(Experiment).Should().Be(1);
    }

    [Test]
    public void AssignRuleKeepsItsCachedAssignmentWhenTheUnitIsSetLater()
    {
        var context = CreateContext(Data(Rules(Assign(1))), withSession: false);
        context.SetAttribute("country", "US");
        context.GetTreatment(Experiment).Should().Be(1);

        context.SetUnit("session_id", SessionId);
        context.GetTreatment(Experiment).Should().Be(1);
        context.PendingCount.Should().Be(1);
    }

    [Test]
    public void SplitRuleStartingToMatchOnTheSameVariantReresolvesOnceTheUnitIsSet()
    {
        var context = CreateContext(Data(Rules(Split("0/100/0"), Assign(0, conditions: null))), withSession: false);
        context.PeekTreatment(Experiment).Should().Be(0);

        context.SetAttribute("country", "US");
        context.PeekTreatment(Experiment).Should().Be(0);

        context.SetUnit("session_id", SessionId);
        context.PeekTreatment(Experiment).Should().Be(1);
    }

    [Test]
    public void ExposedSplitStaysOnVariantZeroAfterItsUnitIsSetAndIsExposedOnce()
    {
        var context = CreateContext(Data(Rules(Split("0/100/0"))), withSession: false);
        context.SetAttribute("country", "US");
        context.GetTreatment(Experiment).Should().Be(0);

        context.SetUnit("session_id", SessionId);
        context.SetAttribute("unrelated", true);
        context.GetTreatment(Experiment).Should().Be(0);

        PublishedExposures(context).Select(e => e.Variant).Should().Equal(0);
    }

    [Test]
    public void ExposedSplitStaysOnVariantZeroWhenACustomAssignmentLostToIt()
    {
        var context = CreateContext(Data(Rules(Split("0/100/0"))), withSession: false);
        context.SetAttribute("country", "US");
        context.SetCustomAssignment(Experiment, 2);
        context.GetTreatment(Experiment).Should().Be(0);

        context.SetUnit("session_id", SessionId);
        context.GetTreatment(Experiment).Should().Be(0);

        PublishedExposures(context).Select(e => e.Variant).Should().Equal(0);
    }

    // SetUnit retires the no-unit assignment after GetTreatment has looked it up but before it is
    // exposed: the read must re-resolve rather than return variant 0 with no exposure.
    [Test]
    public void GetTreatmentReresolvesAnAssignmentRetiredBeforeItsExposure()
    {
        var context = CreateContext(Data(Rules(Split("0/100/0"))), withSession: false);
        context.SetAttribute("country", "US");
        context.PeekTreatment(Experiment).Should().Be(0);

        context.AfterAssignmentLookup = () => context.SetUnit("session_id", SessionId);

        context.GetTreatment(Experiment).Should().Be(1);
        PublishedExposures(context).Select(e => e.Variant).Should().Equal(1);
    }

    [Test]
    public void GetVariableValueReresolvesAnAssignmentRetiredBeforeItsExposure()
    {
        var context = CreateContext(Data(Rules(Split("0/100/0"))), withSession: false);
        context.SetAttribute("country", "US");
        context.PeekTreatment(Experiment).Should().Be(0);

        context.AfterAssignmentLookup = () => context.SetUnit("session_id", SessionId);

        context.GetVariableValue("button.color", "default").Should().Be("blue");
        PublishedExposures(context).Select(e => e.Variant).Should().Equal(1);
    }

    [Test]
    public void SplitRuleGivesTheFullOnVariantWhileTheUnitIsMissingOnAFullOnExperiment()
    {
        var context = CreateContext(Data(Rules(Split("0/100/0", conditions: null)), e => e.FullOnVariant = 2),
            withSession: false);
        context.PeekTreatment(Experiment).Should().Be(2);

        context.SetUnit("session_id", SessionId);
        context.PeekTreatment(Experiment).Should().Be(1);
    }
}
