using System.Reflection;
using ABSmartly.Internal.Hashing;
using ABSmartly.Models;
using ABSmartly.Services;
using ABSmartly.Time;
using Attribute = ABSmartly.Models.Attribute;

namespace ABSmartly.Sdk.Tests;

// Holdouts, ported case by case from java-sdk ContextHoldoutTest (java-sdk#12) with the
// javascript-sdk (#65) behaviour where the two differ: a holdout's own exposure follows the
// covered experiment's (JS _triggerExposures), and an override pins its holdout decisions like any
// other assignment (JS _assign). Holdouts arrive in ContextData.Holdouts; an experiment opts in
// through HoldoutIds. Holdout arm 0 holds the unit out; in a 3-arm holdout, arm 1 holds out
// experiments that are not full-on. A held-out experiment gets control, is not assigned and
// publishes no exposure of its own; each holdout publishes its own exposure once per unit type.
[TestFixture]
public class ContextHoldoutTests
{
    private const string UnitType = "session_id";
    private const string Uid = "e791e240fcd3df7d238cfc285f475e8152fcc0ec";
    private const string UidNotHeldOut = "b2c3d4e5f6a1b2c3d4e5f6a1b2c3d4e5f6a1b2c3";

    // split [0.5,0.5], seed 100/200: variant 1 for Uid, variant 0 for UidNotHeldOut.
    private const int NormalSeedHi = 100;
    private const int NormalSeedLo = 200;
    private const int NormalVariant = 1;

    // split [0.1,0.9], seed 13/111: variant 0 (held out) for Uid, 1 for UidNotHeldOut.
    private const int HoldoutASeedHi = 13;
    private const int HoldoutASeedLo = 111;

    // split [0.1,0.9], seed 1/222: variant 1 (not held out) for both units.
    private const int HoldoutBSeedHi = 1;
    private const int HoldoutBSeedLo = 222;

    // A 3-arm holdout: the split length, not holdoutType, selects the arm-1 rule. For Uid:
    // seedLo 1 -> arm 0, 3 -> arm 1, 0 -> arm 2.
    private static readonly double[] ThreeArmSplit = { 0.3, 0.3, 0.4 };
    private const int ThreeArm0SeedLo = 1;
    private const int ThreeArm1SeedLo = 3;
    private const int ThreeArm2SeedLo = 0;

    private const string UsRule = "{\"rules\":[{\"name\":\"US Internal Users\",\"type\":\"assign\"," +
                                  "\"conditions\":{\"and\":[{\"eq\":[{\"var\":\"country\"},{\"value\":\"US\"}]}]}," +
                                  "\"environments\":[],\"variant\":1}]}";

    private readonly Clock _clock = Clock.Fixed(1_620_000_000_000L);
    private long Now => _clock.Millis();

    private Mock<IContextDataProvider> _dataProvider = null!;
    private Mock<IContextEventLogger> _eventLogger = null!;
    private IContextPublisher _publisher = null!;
    private List<(IContext Context, PublishEvent Event)> _published = null!;

    [SetUp]
    public void SetUp()
    {
        _dataProvider = new Mock<IContextDataProvider>();
        _eventLogger = new Mock<IContextEventLogger>();
        _publisher = Mock.Of<IContextPublisher>();
        _published = new List<(IContext, PublishEvent)>();
        Mock.Get(_publisher)
            .Setup(x => x.PublishAsync(It.IsAny<IContext>(), It.IsAny<PublishEvent>()))
            .Callback<IContext, PublishEvent>((c, e) => _published.Add((c, e)))
            .Returns(Task.CompletedTask);
    }

    #region Helpers

    private Context CreateContext(ContextConfig config, ContextData data)
    {
        config.PublishDelay = TimeSpan.FromMinutes(10);
        return Context.CreateWithData(config, data, _clock, _dataProvider.Object, _publisher, _eventLogger.Object,
            new DefaultVariableParser(), new AudienceMatcher(new DefaultAudienceDeserializer()), null,
            new Dictionary<string, object> { ["application"] = "website", ["environment"] = "production" });
    }

    private Context CreateContext(string uid, ContextData data) =>
        CreateContext(new ContextConfig().SetUnit(UnitType, uid), data);

    private Context CreateContext(ContextData data) => CreateContext(Uid, data);

    private Context CreateContextWithoutUnits(ContextData data) => CreateContext(new ContextConfig(), data);

    private static Experiment NewExperiment(int id, string name, int fullOnVariant = 0) =>
        NewExperiment(id, name, UnitType, fullOnVariant);

    private static Experiment NewExperiment(int id, string name, string unitType, int fullOnVariant) => new()
    {
        Id = id,
        Name = name,
        UnitType = unitType,
        Iteration = 1,
        SeedHi = NormalSeedHi,
        SeedLo = NormalSeedLo,
        Split = new[] { 0.5, 0.5 },
        TrafficSeedHi = 1,
        TrafficSeedLo = 2,
        TrafficSplit = new[] { 0.0, 1.0 },
        FullOnVariant = fullOnVariant,
        Applications = new[] { new ExperimentApplication { Name = "website" } },
        Variants = new[] { new ExperimentVariant { Name = "A" }, new ExperimentVariant { Name = "B" } }
    };

    private static Experiment NewHoldout(int id, string name, int seedHi, int seedLo) =>
        NewHoldout(id, name, UnitType, seedHi, seedLo, "full");

    private static Experiment NewHoldout(int id, string name, string unitType, int seedHi, int seedLo,
        string holdoutType) => new()
    {
        Id = id,
        Name = name,
        UnitType = unitType,
        Iteration = 1,
        SeedHi = seedHi,
        SeedLo = seedLo,
        Split = new[] { 0.1, 0.9 },
        TrafficSplit = new[] { 0.0, 1.0 },
        Applications = Array.Empty<ExperimentApplication>(),
        Variants = new[] { new ExperimentVariant { Name = "A" }, new ExperimentVariant { Name = "B" } },
        HoldoutType = holdoutType
    };

    // holdoutType is set for wire fidelity only; the arm count comes from the split.
    private static Experiment NewThreeArmHoldout(int id, string name, int seedLo)
    {
        var holdout = NewHoldout(id, name, UnitType, 0, seedLo, "all_full_on");
        holdout.Split = ThreeArmSplit;
        holdout.Variants = new[]
        {
            new ExperimentVariant { Name = "A" }, new ExperimentVariant { Name = "B" },
            new ExperimentVariant { Name = "C" }
        };
        return holdout;
    }

    private static Experiment CoveredBy(Experiment experiment, params int[] holdoutIds)
    {
        experiment.HoldoutIds = holdoutIds;
        return experiment;
    }

    private static Experiment WithVariables(Experiment experiment, string key)
    {
        experiment.Variants = new[]
        {
            new ExperimentVariant { Name = "A", Config = "{\"" + key + "\":\"control\"}" },
            new ExperimentVariant { Name = "B", Config = "{\"" + key + "\":\"treatment\"}" }
        };
        return experiment;
    }

    private static ContextData DataOf(params Experiment[] experiments) => new(experiments);

    private static ContextData DataOf(Experiment?[]? holdouts, params Experiment[] experiments) =>
        new(experiments, holdouts!);

    private void Refresh(Context context, ContextData data)
    {
        _dataProvider.Setup(x => x.GetContextDataAsync()).ReturnsAsync(data);
        context.Refresh();
    }

    private Exposure Exposure(int id, string name, int variant, bool assigned = true, bool overridden = false,
        bool fullOn = false, bool custom = false, bool audienceMismatch = false, bool ruleOverride = false,
        string unitType = UnitType) => new()
    {
        Id = id,
        Name = name,
        Unit = unitType,
        Variant = variant,
        ExposedAt = Now,
        Assigned = assigned,
        Eligible = true,
        Overridden = overridden,
        FullOn = fullOn,
        Custom = custom,
        AudienceMismatch = audienceMismatch,
        RuleOverride = ruleOverride
    };

    private Exposure HoldoutExposure(int id, string name, int variant, string unitType = UnitType) =>
        Exposure(id, name, variant, unitType: unitType);

    private PublishEvent Publish(Context context)
    {
        context.Publish();
        return _published.Where(p => ReferenceEquals(p.Context, context)).Should().ContainSingle().Subject.Event;
    }

    private PublishEvent AssertPublished(Context context, string uid, params Exposure[] exposures) =>
        AssertPublished(context, UnitType, uid, exposures);

    private PublishEvent AssertPublished(Context context, string unitType, string uid, params Exposure[] exposures)
    {
        var published = Publish(context);
        published.Hashed.Should().BeTrue();
        published.PublishedAt.Should().Be(Now);
        published.Units.Should().Equal(new Unit { Type = unitType, Uid = Md5.Hash(uid) });
        (published.Exposures ?? Array.Empty<Exposure>()).Should().Equal(exposures);
        return published;
    }

    private static object InstalledHoldouts(Context context) =>
        typeof(Context).GetField("_holdoutsById", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(context)!;

    private static bool FlushScheduled(Context context) =>
        typeof(Context).GetField("_timeout", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(context) != null;

    private void ThrowOnExposure(Func<Exposure, bool> predicate) =>
        _eventLogger.Setup(x => x.HandleEvent(It.IsAny<IContext>(), EventType.Exposure,
                It.Is<object>(o => o is Exposure && predicate((Exposure)o))))
            .Throws(new InvalidOperationException("boom"));

    #endregion

    [Test]
    public void HeldOutUnitGetsControlValuesAndEmitsNoExposureForCoveredExperiment()
    {
        var experiment = CoveredBy(NewExperiment(1, "exp_holdout_in"), 11);
        var context = CreateContext(DataOf(new[] { NewHoldout(11, "holdout_a", HoldoutASeedHi, HoldoutASeedLo) },
            experiment));

        context.PeekTreatment("exp_holdout_in").Should().Be(0);
        context.GetTreatment("exp_holdout_in");

        AssertPublished(context, Uid, HoldoutExposure(11, "holdout_a", 0));
    }

    [Test]
    public void NotHeldOutUnitEmitsHoldoutExposureVariantOneAndNormalExposure()
    {
        var experiment = CoveredBy(NewExperiment(1, "exp_holdout_out"), 11);
        var context = CreateContext(UidNotHeldOut,
            DataOf(new[] { NewHoldout(11, "holdout_a", HoldoutASeedHi, HoldoutASeedLo) }, experiment));

        context.GetTreatment("exp_holdout_out").Should().Be(0); // UidNotHeldOut's normal variant is 0

        AssertPublished(context, UidNotHeldOut, Exposure(1, "exp_holdout_out", 0),
            HoldoutExposure(11, "holdout_a", 1));
    }

    [Test]
    public void ExperimentWithoutHoldoutIdsEmitsNormallyEvenForHeldOutUnit()
    {
        var covered = CoveredBy(NewExperiment(1, "exp_holdout_in"), 11);
        var notCovered = NewExperiment(2, "exp_not_covered");
        var context = CreateContext(DataOf(new[] { NewHoldout(11, "holdout_a", HoldoutASeedHi, HoldoutASeedLo) },
            covered, notCovered));

        context.GetTreatment("exp_holdout_in").Should().Be(0);
        context.GetTreatment("exp_not_covered").Should().Be(NormalVariant);

        AssertPublished(context, Uid, HoldoutExposure(11, "holdout_a", 0),
            Exposure(2, "exp_not_covered", NormalVariant));
    }

    [Test]
    public void UnionOfApplicableHoldoutsSuppressesExperimentAndBothEmitOwnExposure()
    {
        var experiment = CoveredBy(NewExperiment(1, "exp_multi_holdout"), 11, 12);
        var context = CreateContext(DataOf(new[]
        {
            NewHoldout(11, "holdout_a", HoldoutASeedHi, HoldoutASeedLo), // holds Uid out
            NewHoldout(12, "holdout_b", HoldoutBSeedHi, HoldoutBSeedLo) // does not
        }, experiment));

        context.GetTreatment("exp_multi_holdout").Should().Be(0);

        AssertPublished(context, Uid, HoldoutExposure(11, "holdout_a", 0), HoldoutExposure(12, "holdout_b", 1));
    }

    // The deciding holdout is the higher id one, so a check of only the first holdout fails.
    [Test]
    public void UnionRuleAppliesWhenTheHigherIdHoldoutIsTheOnlyOneHoldingUnitOut()
    {
        var experiment = CoveredBy(NewExperiment(1, "exp_union_high_id_decides"), 11, 12);
        var context = CreateContext(DataOf(new[]
        {
            NewHoldout(11, "holdout_low_id", HoldoutBSeedHi, HoldoutBSeedLo),
            NewHoldout(12, "holdout_high_id", HoldoutASeedHi, HoldoutASeedLo)
        }, experiment));

        context.GetTreatment("exp_union_high_id_decides").Should().Be(0);

        AssertPublished(context, Uid, HoldoutExposure(11, "holdout_low_id", 1),
            HoldoutExposure(12, "holdout_high_id", 0));
    }

    [Test]
    public void UnitNotHeldOutByEitherHoldoutIsNotSuppressed()
    {
        var experiment = CoveredBy(NewExperiment(1, "exp_multi_holdout_miss"), 11, 12);
        var context = CreateContext(UidNotHeldOut, DataOf(new[]
        {
            NewHoldout(11, "holdout_a", HoldoutASeedHi, HoldoutASeedLo),
            NewHoldout(12, "holdout_b", HoldoutBSeedHi, HoldoutBSeedLo)
        }, experiment));

        context.GetTreatment("exp_multi_holdout_miss").Should().Be(0);

        AssertPublished(context, UidNotHeldOut, Exposure(1, "exp_multi_holdout_miss", 0),
            HoldoutExposure(11, "holdout_a", 1), HoldoutExposure(12, "holdout_b", 1));
    }

    [Test]
    public void HoldoutSuppressesFullOnExperimentRegardlessOfFullOnVariant()
    {
        var experiment = CoveredBy(NewExperiment(1, "exp_fullon_holdout", 2), 11);
        var context = CreateContext(DataOf(new[] { NewHoldout(11, "holdout_a", HoldoutASeedHi, HoldoutASeedLo) },
            experiment));

        context.GetTreatment("exp_fullon_holdout").Should().Be(0);
    }

    [Test]
    public void ThreeArmVariantZeroHoldsOutBothFullOnAndNonFullOnExperiments()
    {
        var nonFullOn = CoveredBy(NewExperiment(1, "exp_three_arm_0_non_fullon"), 21);
        var fullOn = CoveredBy(NewExperiment(2, "exp_three_arm_0_fullon", 2), 21);
        var context = CreateContext(DataOf(new[] { NewThreeArmHoldout(21, "holdout_three_arm", ThreeArm0SeedLo) },
            nonFullOn, fullOn));

        context.GetTreatment("exp_three_arm_0_non_fullon").Should().Be(0);
        context.GetTreatment("exp_three_arm_0_fullon").Should().Be(0);

        AssertPublished(context, Uid, HoldoutExposure(21, "holdout_three_arm", 0));
    }

    [Test]
    public void ThreeArmVariantOneForcesNonFullOnExperimentToControlButAssignsFullOnExperimentNormally()
    {
        var nonFullOn = CoveredBy(NewExperiment(1, "exp_three_arm_1_non_fullon"), 21);
        var fullOn = CoveredBy(NewExperiment(2, "exp_three_arm_1_fullon", 2), 21);
        var context = CreateContext(DataOf(new[] { NewThreeArmHoldout(21, "holdout_three_arm", ThreeArm1SeedLo) },
            nonFullOn, fullOn));

        context.GetTreatment("exp_three_arm_1_non_fullon").Should().Be(0);
        context.GetTreatment("exp_three_arm_1_fullon").Should().Be(2);

        AssertPublished(context, Uid, HoldoutExposure(21, "holdout_three_arm", 1),
            Exposure(2, "exp_three_arm_1_fullon", 2, fullOn: true));
    }

    [Test]
    public void ThreeArmVariantTwoEvaluatesBothExperimentKindsNormally()
    {
        var nonFullOn = CoveredBy(NewExperiment(1, "exp_three_arm_2_non_fullon"), 21);
        var fullOn = CoveredBy(NewExperiment(2, "exp_three_arm_2_fullon", 2), 21);
        var context = CreateContext(DataOf(new[] { NewThreeArmHoldout(21, "holdout_three_arm", ThreeArm2SeedLo) },
            nonFullOn, fullOn));

        context.GetTreatment("exp_three_arm_2_non_fullon").Should().Be(NormalVariant);
        context.GetTreatment("exp_three_arm_2_fullon").Should().Be(2);

        AssertPublished(context, Uid, Exposure(1, "exp_three_arm_2_non_fullon", NormalVariant),
            HoldoutExposure(21, "holdout_three_arm", 2), Exposure(2, "exp_three_arm_2_fullon", 2, fullOn: true));
    }

    // Cross-sdk scenario 222: the arm count comes from the split, never from holdoutType. A 3-arm
    // split without holdoutType (the collector's slim shape) still applies the arm-1 rule, and a
    // 2-arm split labelled "all_full_on" does not.
    [Test]
    public void ArityComesFromSplitLengthNotHoldoutType()
    {
        var untyped = NewThreeArmHoldout(21, "holdout_untyped", ThreeArm1SeedLo);
        untyped.HoldoutType = null;
        var context = CreateContext(DataOf(new[] { untyped }, CoveredBy(NewExperiment(1, "exp_untyped"), 21)));

        context.GetTreatment("exp_untyped").Should().Be(0);
        AssertPublished(context, Uid, HoldoutExposure(21, "holdout_untyped", 1));

        var mislabelled = NewHoldout(31, "holdout_mislabelled", UnitType, HoldoutBSeedHi, HoldoutBSeedLo, "all_full_on");
        var twoArm = CreateContext(DataOf(new[] { mislabelled }, CoveredBy(NewExperiment(1, "exp_mislabelled"), 31)));

        twoArm.GetTreatment("exp_mislabelled").Should().Be(NormalVariant);
        AssertPublished(twoArm, Uid, Exposure(1, "exp_mislabelled", NormalVariant),
            HoldoutExposure(31, "holdout_mislabelled", 1));
    }

    [Test]
    public void TwoArmHoldoutWinsUnionEvenWhenThreeArmWouldNotHoldOut()
    {
        var experiment = CoveredBy(NewExperiment(1, "exp_union_two_arm_wins"), 11, 22);
        var context = CreateContext(DataOf(new[]
        {
            NewHoldout(11, "holdout_two_arm", HoldoutASeedHi, HoldoutASeedLo),
            NewThreeArmHoldout(22, "holdout_three_arm", ThreeArm2SeedLo)
        }, experiment));

        context.GetTreatment("exp_union_two_arm_wins").Should().Be(0);

        AssertPublished(context, Uid, HoldoutExposure(11, "holdout_two_arm", 0),
            HoldoutExposure(22, "holdout_three_arm", 2));
    }

    [Test]
    public void ThreeArmHoldoutWinsUnionEvenWhenTwoArmWouldNotHoldOut()
    {
        var experiment = CoveredBy(NewExperiment(1, "exp_union_three_arm_wins"), 11, 22);
        var context = CreateContext(DataOf(new[]
        {
            NewHoldout(11, "holdout_two_arm", HoldoutBSeedHi, HoldoutBSeedLo),
            NewThreeArmHoldout(22, "holdout_three_arm", ThreeArm1SeedLo)
        }, experiment));

        context.GetTreatment("exp_union_three_arm_wins").Should().Be(0);

        AssertPublished(context, Uid, HoldoutExposure(11, "holdout_two_arm", 1),
            HoldoutExposure(22, "holdout_three_arm", 1));
    }

    // Arm 1 of a 3-arm holdout defers a full-on experiment to its normal path, where a strict
    // audience mismatch still gives control, exactly as arm 2 does.
    [Test]
    public void ThreeArmArmOneDefersToNormalPathSoAudienceMismatchStillWinsForFullOnExperiment()
    {
        const string audience = "{\"filter\":[{\"gte\":[{\"var\":\"age\"},{\"value\":20}]}]}";

        Context ContextFor(string name, int seedLo)
        {
            var fullOn = CoveredBy(NewExperiment(1, name, 2), 21);
            fullOn.AudienceStrict = true;
            fullOn.Audience = audience;
            var context = CreateContext(DataOf(new[] { NewThreeArmHoldout(21, "holdout_three_arm", seedLo) }, fullOn));
            context.SetAttribute("age", 5);
            return context;
        }

        var arm1 = ContextFor("exp_three_arm_1_audience_fullon", ThreeArm1SeedLo);
        var arm2 = ContextFor("exp_three_arm_2_audience_fullon", ThreeArm2SeedLo);

        arm1.GetTreatment("exp_three_arm_1_audience_fullon").Should().Be(0);
        arm2.GetTreatment("exp_three_arm_2_audience_fullon").Should().Be(0);

        AssertPublished(arm1, Uid,
                Exposure(1, "exp_three_arm_1_audience_fullon", 0, assigned: false, audienceMismatch: true),
                HoldoutExposure(21, "holdout_three_arm", 1))
            .Attributes.Should().Equal(new Attribute { Name = "age", Value = 5, SetAt = Now });
        AssertPublished(arm2, Uid,
            Exposure(1, "exp_three_arm_2_audience_fullon", 0, assigned: false, audienceMismatch: true),
            HoldoutExposure(21, "holdout_three_arm", 2));
    }

    [Test]
    public void UnconfiguredUnitTypeGetsControlValuesAndEmitsNoExposureAtAll()
    {
        var experiment = CoveredBy(NewExperiment(1, "exp_user_holdout", "user_id", 0), 11);
        var holdout = NewHoldout(11, "holdout_user", "user_id", HoldoutASeedHi, HoldoutASeedLo, "full");
        var context = CreateContext(DataOf(new[] { holdout }, experiment)); // session_id only

        context.PeekTreatment("exp_user_holdout").Should().Be(0);
        context.GetTreatment("exp_user_holdout");
        context.Publish();

        // The unassigned experiment's own exposure is published as before; the holdout, which
        // cannot be resolved without the unit, publishes nothing.
        context.PendingCount.Should().Be(0);
        _published.Single().Event.Exposures.Should().Equal(Exposure(1, "exp_user_holdout", 0, assigned: false,
            unitType: "user_id"));
    }

    [Test]
    public void AssignsNormallyWhenHoldoutsKeyIsAbsent()
    {
        var context = CreateContext(DataOf(NewExperiment(1, "exp_no_holdouts_key")));

        context.PeekTreatment("exp_no_holdouts_key").Should().Be(NormalVariant);
    }

    [Test]
    public void HoldoutIdAbsentFromHoldoutsArrayIsIgnoredAndExperimentAssignsNormally()
    {
        var context = CreateContext(DataOf(CoveredBy(NewExperiment(1, "exp_dangling_holdout_id"), 999)));

        context.PeekTreatment("exp_dangling_holdout_id").Should().Be(NormalVariant);
    }

    [Test]
    public void CustomAssignmentCannotOverrideHeldOutVariant()
    {
        var experiment = CoveredBy(NewExperiment(1, "exp_holdout_custom"), 11);
        var context = CreateContext(new ContextConfig().SetUnit(UnitType, Uid).SetCustomAssignment("exp_holdout_custom", 3),
            DataOf(new[] { NewHoldout(11, "holdout_a", HoldoutASeedHi, HoldoutASeedLo) }, experiment));

        context.PeekTreatment("exp_holdout_custom").Should().Be(0);
    }

    [Test]
    public void ReusesCachedHeldOutAssignmentWithCustomAssignment()
    {
        var experiment = CoveredBy(NewExperiment(1, "exp_holdout_custom_cache"), 11);
        var context = CreateContext(
            new ContextConfig().SetUnit(UnitType, Uid).SetCustomAssignment("exp_holdout_custom_cache", 3),
            DataOf(new[] { NewHoldout(11, "holdout_a", HoldoutASeedHi, HoldoutASeedLo) }, experiment));

        context.GetTreatment("exp_holdout_custom_cache").Should().Be(0);
        context.GetTreatment("exp_holdout_custom_cache").Should().Be(0);
        context.PendingCount.Should().Be(1);
    }

    [Test]
    public void OverrideTakesPrecedenceOverHoldout()
    {
        var experiment = CoveredBy(NewExperiment(1, "exp_holdout_override"), 11);
        var context = CreateContext(new ContextConfig().SetUnit(UnitType, Uid).SetOverride("exp_holdout_override", 3),
            DataOf(new[] { NewHoldout(11, "holdout_a", HoldoutASeedHi, HoldoutASeedLo) }, experiment));

        context.PeekTreatment("exp_holdout_override").Should().Be(3);
    }

    [Test]
    public void GetTreatmentOnOverriddenExperimentTriggersApplicableHoldoutExposureBothArms()
    {
        var heldOut = CreateContext(
            new ContextConfig().SetUnit(UnitType, Uid).SetOverride("exp_holdout_override_held_out", 3),
            DataOf(new[] { NewHoldout(11, "holdout_a", HoldoutASeedHi, HoldoutASeedLo) },
                CoveredBy(NewExperiment(1, "exp_holdout_override_held_out"), 11)));

        heldOut.GetTreatment("exp_holdout_override_held_out").Should().Be(3);
        AssertPublished(heldOut, Uid,
            Exposure(1, "exp_holdout_override_held_out", 3, assigned: false, overridden: true),
            HoldoutExposure(11, "holdout_a", 0));

        var notHeldOut = CreateContext(
            new ContextConfig().SetUnit(UnitType, UidNotHeldOut).SetOverride("exp_holdout_override_not_held_out", 3),
            DataOf(new[] { NewHoldout(11, "holdout_a", HoldoutASeedHi, HoldoutASeedLo) },
                CoveredBy(NewExperiment(1, "exp_holdout_override_not_held_out"), 11)));

        notHeldOut.GetTreatment("exp_holdout_override_not_held_out").Should().Be(3);
        AssertPublished(notHeldOut, UidNotHeldOut,
            Exposure(1, "exp_holdout_override_not_held_out", 3, assigned: false, overridden: true),
            HoldoutExposure(11, "holdout_a", 1));
    }

    [Test]
    public void HoldoutTakesPrecedenceOverAudienceMismatch()
    {
        var experiment = CoveredBy(NewExperiment(1, "exp_holdout_audience"), 11);
        experiment.AudienceStrict = true;
        experiment.Audience = "{\"filter\":[{\"gte\":[{\"var\":\"age\"},{\"value\":20}]}]}";
        var context = CreateContext(DataOf(new[] { NewHoldout(11, "holdout_a", HoldoutASeedHi, HoldoutASeedLo) },
            experiment));
        context.SetAttribute("age", 5);

        context.PeekTreatment("exp_holdout_audience").Should().Be(0);
    }

    [Test]
    public void HoldoutExposureEmittedOncePerContextNotPerSuppressedExperiment()
    {
        var context = CreateContext(DataOf(new[] { NewHoldout(11, "holdout_a", HoldoutASeedHi, HoldoutASeedLo) },
            CoveredBy(NewExperiment(1, "exp_shared_a"), 11), CoveredBy(NewExperiment(2, "exp_shared_b"), 11)));

        context.GetTreatment("exp_shared_a").Should().Be(0);
        context.GetTreatment("exp_shared_b").Should().Be(0);

        AssertPublished(context, Uid, HoldoutExposure(11, "holdout_a", 0));
    }

    [Test]
    public void HoldoutExposureFiresOnceEvenWhenTriggeringExperimentIsNotSuppressed()
    {
        var context = CreateContext(UidNotHeldOut,
            DataOf(new[] { NewHoldout(11, "holdout_a", HoldoutASeedHi, HoldoutASeedLo) },
                CoveredBy(NewExperiment(1, "exp_shared_a"), 11), CoveredBy(NewExperiment(2, "exp_shared_b"), 11)));

        context.GetTreatment("exp_shared_a");
        context.GetTreatment("exp_shared_b");

        AssertPublished(context, UidNotHeldOut, Exposure(1, "exp_shared_a", 0), HoldoutExposure(11, "holdout_a", 1),
            Exposure(2, "exp_shared_b", 0));
    }

    [Test]
    public void ReusesCachedSuppressedAssignmentAcrossRepeatedCalls()
    {
        var context = CreateContext(DataOf(new[] { NewHoldout(11, "holdout_a", HoldoutASeedHi, HoldoutASeedLo) },
            CoveredBy(NewExperiment(1, "exp_holdout_cache"), 11)));

        context.GetTreatment("exp_holdout_cache").Should().Be(0);
        context.GetTreatment("exp_holdout_cache").Should().Be(0);
        context.PendingCount.Should().Be(1);
    }

    // A same-iteration seed edit must not move an exposed unit to the other arm.
    [Test]
    public void RefreshWithSeedChangeSameIterationKeepsUnitInOriginalArmAndDoesNotReExpose()
    {
        var context = CreateContext(DataOf(new[] { NewHoldout(11, "holdout_a", HoldoutASeedHi, HoldoutASeedLo) },
            CoveredBy(NewExperiment(1, "exp_holdout_seed_thrash"), 11)));

        context.GetTreatment("exp_holdout_seed_thrash").Should().Be(0);
        context.PendingCount.Should().Be(1);

        Refresh(context, DataOf(new[] { NewHoldout(11, "holdout_a", HoldoutBSeedHi, HoldoutBSeedLo) },
            CoveredBy(NewExperiment(1, "exp_holdout_seed_thrash"), 11)));

        context.GetTreatment("exp_holdout_seed_thrash").Should().Be(0);
        context.PendingCount.Should().Be(1);
    }

    [Test]
    public void RefreshWithCosmeticHoldoutEditDoesNotReExposeHeldOutUnit()
    {
        var context = CreateContext(DataOf(new[] { NewHoldout(11, "holdout_a", HoldoutASeedHi, HoldoutASeedLo) },
            CoveredBy(NewExperiment(1, "exp_holdout_cosmetic"), 11)));

        context.GetTreatment("exp_holdout_cosmetic").Should().Be(0);
        context.PendingCount.Should().Be(1);

        var cosmetic = NewHoldout(11, "holdout_a_renamed", HoldoutASeedHi, HoldoutASeedLo);
        cosmetic.Applications = new[] { new ExperimentApplication { Name = "website" } };
        cosmetic.Variants = new[] { new ExperimentVariant { Name = "Control" }, new ExperimentVariant { Name = "HeldOut" } };
        Refresh(context, DataOf(new[] { cosmetic }, CoveredBy(NewExperiment(1, "exp_holdout_cosmetic"), 11)));

        context.GetTreatment("exp_holdout_cosmetic").Should().Be(0);
        context.PendingCount.Should().Be(1);
    }

    [Test]
    public void RefreshWithCosmeticHoldoutEditDoesNotDuplicateCoveredExperimentExposure()
    {
        var context = CreateContext(UidNotHeldOut,
            DataOf(new[] { NewHoldout(11, "holdout_a", HoldoutASeedHi, HoldoutASeedLo) },
                CoveredBy(NewExperiment(1, "exp_holdout_cosmetic_covered"), 11)));

        context.GetTreatment("exp_holdout_cosmetic_covered").Should().Be(0);
        context.PendingCount.Should().Be(2);

        Refresh(context, DataOf(new[] { NewHoldout(11, "holdout_a_renamed", HoldoutASeedHi, HoldoutASeedLo) },
            CoveredBy(NewExperiment(1, "exp_holdout_cosmetic_covered"), 11)));

        context.GetTreatment("exp_holdout_cosmetic_covered").Should().Be(0);
        context.PendingCount.Should().Be(2);
    }

    // An iteration bump is a new epoch: the unit is re-assigned once, into exactly one arm.
    [Test]
    public void RefreshWithIterationBumpReassignsToExactlyOneNewArm()
    {
        var context = CreateContext(DataOf(new[] { NewHoldout(11, "holdout_a", HoldoutBSeedHi, HoldoutBSeedLo) },
            CoveredBy(NewExperiment(1, "exp_holdout_iteration"), 11)));

        context.GetTreatment("exp_holdout_iteration").Should().Be(NormalVariant);
        context.PendingCount.Should().Be(2);

        var newEpoch = NewHoldout(11, "holdout_a", HoldoutASeedHi, HoldoutASeedLo);
        newEpoch.Iteration = 2;
        Refresh(context, DataOf(new[] { newEpoch }, CoveredBy(NewExperiment(1, "exp_holdout_iteration"), 11)));

        context.GetTreatment("exp_holdout_iteration").Should().Be(0);
        context.PendingCount.Should().Be(3);
        context.GetTreatment("exp_holdout_iteration").Should().Be(0);
        context.PendingCount.Should().Be(3);
    }

    // Suppression of an experiment first evaluated after a same-iteration seed edit comes from the
    // pinned holdout arm, not the live seed.
    [Test]
    public void RefreshedSeedDoesNotDesyncSuppressionFromPinnedHoldoutArmForNewlyEvaluatedExperiment()
    {
        var context = CreateContext(DataOf(new[] { NewHoldout(11, "holdout_a", HoldoutASeedHi, HoldoutASeedLo) },
            CoveredBy(NewExperiment(1, "exp_holdout_desync_a"), 11)));

        context.GetTreatment("exp_holdout_desync_a").Should().Be(0);
        context.PendingCount.Should().Be(1);

        Refresh(context, DataOf(new[] { NewHoldout(11, "holdout_a", HoldoutBSeedHi, HoldoutBSeedLo) },
            CoveredBy(NewExperiment(1, "exp_holdout_desync_a"), 11),
            CoveredBy(NewExperiment(2, "exp_holdout_desync_b"), 11)));

        context.GetTreatment("exp_holdout_desync_b").Should().Be(0);
        context.GetTreatment("exp_holdout_desync_a").Should().Be(0);

        AssertPublished(context, Uid, HoldoutExposure(11, "holdout_a", 0));
    }

    // The pinned arm keeps the arity it was drawn with: arm 1 of 2 still defers after a
    // same-iteration refresh to 3 arms.
    [Test]
    public void RefreshFromTwoArmToThreeArmSameIterationDoesNotReinterpretDeferringArmAsFullOnOnly()
    {
        var context = CreateContext(DataOf(new[] { NewHoldout(31, "holdout_arity", HoldoutBSeedHi, HoldoutBSeedLo) },
            CoveredBy(NewExperiment(1, "exp_arity_2to3_a"), 31)));

        context.GetTreatment("exp_arity_2to3_a").Should().Be(NormalVariant);

        Refresh(context, DataOf(new[] { NewThreeArmHoldout(31, "holdout_arity", ThreeArm1SeedLo) },
            CoveredBy(NewExperiment(1, "exp_arity_2to3_a"), 31), CoveredBy(NewExperiment(2, "exp_arity_2to3_b"), 31)));

        context.GetTreatment("exp_arity_2to3_b").Should().Be(NormalVariant);

        AssertPublished(context, Uid, Exposure(1, "exp_arity_2to3_a", NormalVariant),
            HoldoutExposure(31, "holdout_arity", 1), Exposure(2, "exp_arity_2to3_b", NormalVariant));
    }

    [Test]
    public void RefreshFromThreeArmToTwoArmSameIterationKeepsFullOnOnlyArmSuppressingNonFullOnExperiment()
    {
        var context = CreateContext(DataOf(new[] { NewThreeArmHoldout(41, "holdout_arity_rev", ThreeArm1SeedLo) },
            CoveredBy(NewExperiment(1, "exp_arity_3to2_a"), 41)));

        context.GetTreatment("exp_arity_3to2_a").Should().Be(0);
        context.PendingCount.Should().Be(1);

        Refresh(context, DataOf(new[] { NewHoldout(41, "holdout_arity_rev", HoldoutBSeedHi, HoldoutBSeedLo) },
            CoveredBy(NewExperiment(1, "exp_arity_3to2_a"), 41), CoveredBy(NewExperiment(2, "exp_arity_3to2_b"), 41)));

        context.GetTreatment("exp_arity_3to2_b").Should().Be(0);

        AssertPublished(context, Uid, HoldoutExposure(41, "holdout_arity_rev", 1));
    }

    [Test]
    public void ExperimentsEvaluatedBeforeAndAfterSameIterationArityChangeAgreeOnSuppression()
    {
        var context = CreateContext(DataOf(
            new[] { NewHoldout(71, "holdout_arity_consistency", HoldoutBSeedHi, HoldoutBSeedLo) },
            CoveredBy(NewExperiment(1, "exp_arity_consistency_pre"), 71)));

        context.GetTreatment("exp_arity_consistency_pre").Should().Be(NormalVariant);

        Refresh(context, DataOf(new[] { NewThreeArmHoldout(71, "holdout_arity_consistency", ThreeArm1SeedLo) },
            CoveredBy(NewExperiment(1, "exp_arity_consistency_pre"), 71),
            CoveredBy(NewExperiment(2, "exp_arity_consistency_post"), 71)));

        context.GetTreatment("exp_arity_consistency_post").Should().Be(NormalVariant);
        context.GetTreatment("exp_arity_consistency_pre").Should().Be(NormalVariant);
    }

    // A refresh between the decision and the holdout trigger (here from the event logger, after
    // the experiment's own exposure) must not publish the new iteration's arm.
    [Test]
    public void TriggerFiresHoldoutExposureFromDecisionEpochDespiteRefreshRacingBetweenDecisionAndTrigger()
    {
        var iteration2 = NewHoldout(11, "holdout_h", HoldoutASeedHi, HoldoutASeedLo);
        iteration2.Iteration = 2;
        _dataProvider.Setup(x => x.GetContextDataAsync())
            .ReturnsAsync(DataOf(new[] { iteration2 }, CoveredBy(NewExperiment(1, "exp_epoch_race"), 11)));

        var context = CreateContext(DataOf(new[] { NewHoldout(11, "holdout_h", HoldoutBSeedHi, HoldoutBSeedLo) },
            CoveredBy(NewExperiment(1, "exp_epoch_race"), 11)));
        _eventLogger.Setup(x => x.HandleEvent(It.IsAny<IContext>(), EventType.Exposure,
                It.Is<object>(o => o is Exposure && ((Exposure)o).Id == 1)))
            .Callback(() => context.Refresh());

        context.GetTreatment("exp_epoch_race").Should().Be(NormalVariant);

        AssertPublished(context, Uid, Exposure(1, "exp_epoch_race", NormalVariant),
            HoldoutExposure(11, "holdout_h", 1));
    }

    // A holdout resolved while the unit was missing is resolved again once SetUnit installs it,
    // so its exposure is not lost.
    [Test]
    public void SetUnitInvalidatesNullHoldoutSnapshotSoTheHoldoutExposureIsNotLostForever()
    {
        var context = CreateContextWithoutUnits(DataOf(
            new[] { NewHoldout(11, "holdout_user_id", "user_id", HoldoutBSeedHi, HoldoutBSeedLo, "full") },
            CoveredBy(NewExperiment(1, "exp_null_snapshot_fullon", "user_id", 2), 11)));

        context.PeekTreatment("exp_null_snapshot_fullon").Should().Be(2);

        context.SetUnit("user_id", Uid);

        context.GetTreatment("exp_null_snapshot_fullon").Should().Be(2);
        context.PendingCount.Should().Be(2);
        AssertPublished(context, "user_id", Uid,
            Exposure(1, "exp_null_snapshot_fullon", 2, fullOn: true, unitType: "user_id"),
            HoldoutExposure(11, "holdout_user_id", 1, "user_id"));
    }

    [Test]
    public void SetUnitDoesNotEvictAlreadyExposedAssignmentSoNoDuplicateExposure()
    {
        var context = CreateContextWithoutUnits(DataOf(
            new[] { NewHoldout(11, "holdout_user_id", "user_id", HoldoutBSeedHi, HoldoutBSeedLo, "full") },
            CoveredBy(NewExperiment(1, "exp_exposed_before_unit", "user_id", 2), 11)));

        context.GetTreatment("exp_exposed_before_unit").Should().Be(2);
        context.PendingCount.Should().Be(1);

        context.SetUnit("user_id", Uid);

        context.GetTreatment("exp_exposed_before_unit").Should().Be(2);
        context.PendingCount.Should().Be(1);
    }

    [Test]
    public void SetUnitForUnrelatedUnitTypeDoesNotEvictExposedAssignment()
    {
        var context = CreateContext(DataOf(new[] { NewHoldout(11, "holdout_a", HoldoutBSeedHi, HoldoutBSeedLo) },
            CoveredBy(NewExperiment(1, "exp_unrelated_unit"), 11)));

        context.GetTreatment("exp_unrelated_unit").Should().Be(NormalVariant);
        context.PendingCount.Should().Be(2);

        context.SetUnit("other_unit_type", "some-other-uid");

        context.GetTreatment("exp_unrelated_unit").Should().Be(NormalVariant);
        context.PendingCount.Should().Be(2);
    }

    [Test]
    public void SetUnitEvictsOnlyTheAffectedEntryAndLeavesTheOtherUntouched()
    {
        var context = CreateContext(DataOf(new[]
            {
                NewHoldout(11, "holdout_late", "user_id", HoldoutBSeedHi, HoldoutBSeedLo, "full"),
                NewHoldout(12, "holdout_present", HoldoutBSeedHi, HoldoutBSeedLo)
            },
            CoveredBy(NewExperiment(1, "exp_late_unit", "user_id", 2), 11),
            CoveredBy(NewExperiment(2, "exp_present_unit"), 12)));

        context.PeekTreatment("exp_late_unit").Should().Be(2);
        context.GetTreatment("exp_present_unit").Should().Be(NormalVariant);
        context.PendingCount.Should().Be(2);

        context.SetUnit("user_id", Uid);

        context.GetTreatment("exp_late_unit").Should().Be(2);
        context.PendingCount.Should().Be(4);
        context.GetTreatment("exp_present_unit").Should().Be(NormalVariant);
        context.PendingCount.Should().Be(4);
    }

    // The holdout is resolved with the covered experiment's unit type, not its own.
    [Test]
    public void SetUnitEvictsUsingTheCoveredExperimentsUnitTypeNotTheHoldoutsDeclaredUnitType()
    {
        var context = CreateContextWithoutUnits(DataOf(
            new[] { NewHoldout(11, "holdout_declares_b", "B", HoldoutBSeedHi, HoldoutBSeedLo, "full") },
            CoveredBy(NewExperiment(1, "exp_mismatched_unit_type", "A", 2), 11)));

        context.PeekTreatment("exp_mismatched_unit_type").Should().Be(2);

        context.SetUnit("A", Uid);

        context.GetTreatment("exp_mismatched_unit_type").Should().Be(2);
        context.PendingCount.Should().Be(2);
    }

    // With both units set, the arm is still hashed with the covered experiment's unit (held out),
    // not the holdout's declared one (not held out), and its exposure carries that unit type.
    [Test]
    public void HoldoutArmIsHashedWithTheCoveredExperimentsUnitEvenWhenItsOwnUnitIsSet()
    {
        var context = CreateContext(new ContextConfig().SetUnit(UnitType, Uid).SetUnit("user_id", UidNotHeldOut),
            DataOf(new[] { NewHoldout(11, "holdout_declares_user", "user_id", HoldoutASeedHi, HoldoutASeedLo, "full") },
                CoveredBy(NewExperiment(1, "exp_session_covered"), 11)));

        context.GetTreatment("exp_session_covered").Should().Be(0);

        context.Publish();
        var exposures = _published.Should().ContainSingle().Subject.Event.Exposures;
        exposures.Should().Equal(HoldoutExposure(11, "holdout_declares_user", 0));
    }

    // Recomputed as a whole, so a late holdout that holds the unit out flips the experiment to
    // control instead of publishing a held-out exposure beside a participating one.
    [Test]
    public void SetUnitRecomputesSuppressionCoherentlyWhenLateResolvedHoldoutSuppresses()
    {
        var context = CreateContextWithoutUnits(DataOf(
            new[] { NewHoldout(11, "holdout_user_id_suppress", "user_id", HoldoutASeedHi, HoldoutASeedLo, "full") },
            CoveredBy(NewExperiment(1, "exp_null_snapshot_suppresses", "user_id", 2), 11)));

        context.PeekTreatment("exp_null_snapshot_suppresses").Should().Be(2);

        context.SetUnit("user_id", Uid);

        context.GetTreatment("exp_null_snapshot_suppresses").Should().Be(0);
        context.PendingCount.Should().Be(1);
        AssertPublished(context, "user_id", Uid, HoldoutExposure(11, "holdout_user_id_suppress", 0, "user_id"));
    }

    // Java's detached-assignment cases: a GetTreatment holding the pre-SetUnit assignment when
    // SetUnit retires it re-resolves (dotnet's retire-and-retry loop) and exposes only the
    // replacement's decision.
    [Test]
    public void DetachedTreatmentAssignmentCannotPublishBesideSuppressingReplacement()
    {
        var context = CreateContextWithoutUnits(DataOf(
            new[] { NewHoldout(11, "holdout_detached_suppressed", "user_id", HoldoutASeedHi, HoldoutASeedLo, "full") },
            CoveredBy(NewExperiment(1, "exp_detached_suppressed", "user_id", 2), 11)));
        context.PeekTreatment("exp_detached_suppressed").Should().Be(2);

        context.AfterAssignmentLookup = () => context.SetUnit("user_id", Uid);

        context.GetTreatment("exp_detached_suppressed").Should().Be(0);
        context.PendingCount.Should().Be(1);
        AssertPublished(context, "user_id", Uid, HoldoutExposure(11, "holdout_detached_suppressed", 0, "user_id"));
    }

    [Test]
    public void DetachedTreatmentAssignmentRetriesCurrentEntryWithoutLosingExposure()
    {
        var context = CreateContextWithoutUnits(DataOf(
            new[] { NewHoldout(11, "holdout_detached_retry", "user_id", HoldoutBSeedHi, HoldoutBSeedLo, "full") },
            CoveredBy(NewExperiment(1, "exp_detached_retry", "user_id", 2), 11)));
        context.PeekTreatment("exp_detached_retry").Should().Be(2);

        context.AfterAssignmentLookup = () => context.SetUnit("user_id", Uid);

        context.GetTreatment("exp_detached_retry").Should().Be(2);
        context.PendingCount.Should().Be(2);
        context.GetTreatment("exp_detached_retry").Should().Be(2);
        context.PendingCount.Should().Be(2);
        AssertPublished(context, "user_id", Uid,
            Exposure(1, "exp_detached_retry", 2, fullOn: true, unitType: "user_id"),
            HoldoutExposure(11, "holdout_detached_retry", 1, "user_id"));
    }

    [Test]
    public void DetachedTreatmentReturnsTheReplacementVariant()
    {
        var context = CreateContextWithoutUnits(DataOf(
            new[] { NewHoldout(11, "holdout_detached_variant", "user_id", HoldoutASeedHi, HoldoutASeedLo, "full") },
            CoveredBy(NewExperiment(1, "exp_detached_variant", "user_id", 2), 11)));
        context.PeekTreatment("exp_detached_variant");

        context.AfterAssignmentLookup = () => context.SetUnit("user_id", Uid);

        context.GetTreatment("exp_detached_variant").Should().Be(0);
        context.PendingCount.Should().Be(1);
    }

    // Java bounds its settle loop at three attempts; dotnet re-resolves until the assignment it
    // holds is not retired. Only the SetUnit of the experiment's own unit type retires it: a later
    // SetUnit of another type leaves the replacement in place, so the read settles on its second
    // resolution and exposes once.
    [Test]
    public void TreatmentExposureSettlesAfterOneRetirementDespiteALaterUnrelatedSetUnit()
    {
        var context = CreateContextWithoutUnits(DataOf(
            new[] { NewHoldout(11, "holdout_settle", "user_id", HoldoutBSeedHi, HoldoutBSeedLo, "full") },
            CoveredBy(NewExperiment(1, "exp_settle", "user_id", 2), 11)));
        var resolutions = 0;
        context.PeekTreatment("exp_settle");

        void Retire()
        {
            resolutions++;
            if (resolutions == 1)
            {
                // Retires the first assignment; the replacement is built while user_id is set.
                context.SetUnit("user_id", Uid);
                context.AfterAssignmentLookup = Retire;
            }
            else if (resolutions == 2)
            {
                // A second SetUnit (another unit type) does not retire a fully resolved assignment.
                context.SetUnit("device_id", "device");
                context.AfterAssignmentLookup = Retire;
            }
        }

        context.AfterAssignmentLookup = Retire;

        context.GetTreatment("exp_settle").Should().Be(2);
        resolutions.Should().Be(2);
        context.PendingCount.Should().Be(2);
    }

    [Test]
    public void TreatmentWithoutRetirementResolvesOnce()
    {
        var context = CreateContext(DataOf(NewExperiment(1, "exp_bounded_retry", 1)));
        var resolutions = 0;
        context.AfterAssignmentLookup = () => resolutions++;

        context.GetTreatment("exp_bounded_retry").Should().Be(1);

        resolutions.Should().Be(1);
        context.PendingCount.Should().Be(1);
    }

    // A variable read holding the pre-SetUnit assignment re-resolves the key, which after a
    // refresh adding a lower-id experiment is that new winner.
    [Test]
    public void DetachedVariableAssignmentReresolvesTheKeyToANewWinningExperiment()
    {
        var holdout = NewHoldout(11, "holdout_detached_variable", "user_id", HoldoutBSeedHi, HoldoutBSeedLo, "full");
        var original = CoveredBy(NewExperiment(2, "exp_detached_variable_old", "user_id", 1), 11);
        original.Variants[1].Config = "{\"detached_var\":\"old\"}";
        var context = CreateContextWithoutUnits(DataOf(new[] { holdout }, original));
        context.PeekVariableValue("detached_var", "default").Should().Be("old");

        var winner = CoveredBy(NewExperiment(1, "exp_detached_variable_winner", "user_id", 1), 11);
        winner.Variants[1].Config = "{\"detached_var\":\"new winner\"}";
        var refreshedOriginal = CoveredBy(NewExperiment(2, "exp_detached_variable_old", "user_id", 1), 11);
        refreshedOriginal.Variants[1].Config = "{\"detached_var\":\"old\"}";
        context.AfterAssignmentLookup = () =>
        {
            context.SetUnit("user_id", Uid);
            Refresh(context, DataOf(new[] { holdout }, winner, refreshedOriginal));
        };

        context.GetVariableValue("detached_var", "default").Should().Be("new winner");
        context.PendingCount.Should().Be(2);
        context.GetVariableValue("detached_var", "default").Should().Be("new winner");
        context.PendingCount.Should().Be(2);

        AssertPublished(context, "user_id", Uid,
            Exposure(1, "exp_detached_variable_winner", 1, fullOn: true, unitType: "user_id"),
            HoldoutExposure(11, "holdout_detached_variable", 1, "user_id"));
    }

    // An exposed assignment keeps its null holdout entry after SetUnit; a later variable read
    // must not resolve it live and publish a held-out exposure beside the participating one.
    [Test]
    public void TriggerApplicableHoldoutExposuresNeverLiveResolvesAPinnedNullEntryForAnAlreadyExposedAssignment()
    {
        var experiment = CoveredBy(NewExperiment(1, "exp_a_mutant_pinned_null", "user_id", 1), 11);
        experiment.Variants[1].Config = "{\"my_var\":\"value_from_variant\"}";
        var context = CreateContextWithoutUnits(DataOf(
            new[] { NewHoldout(11, "holdout_a_mutant_pinned_null", "user_id", HoldoutASeedHi, HoldoutASeedLo, "full") },
            experiment));

        context.GetTreatment("exp_a_mutant_pinned_null").Should().Be(1);
        context.PendingCount.Should().Be(1);

        context.SetUnit("user_id", Uid);

        context.GetVariableValue("my_var", "default").Should().Be("value_from_variant");
        context.PendingCount.Should().Be(1);
    }

    [Test]
    public void SetUnitEvictsAssignmentWithMultipleHoldoutsDeclaringDifferentUnitTypes()
    {
        var context = CreateContextWithoutUnits(DataOf(new[]
            {
                NewHoldout(11, "holdout_11", "type_x", HoldoutBSeedHi, HoldoutBSeedLo, "full"),
                NewHoldout(12, "holdout_12", "type_y", HoldoutBSeedHi, HoldoutBSeedLo, "full"),
                NewHoldout(13, "holdout_13", "user_id", HoldoutBSeedHi, HoldoutBSeedLo, "full")
            },
            CoveredBy(NewExperiment(1, "exp_multi_holdout_unit_types", "user_id", 2), 11, 12, 13)));

        context.PeekTreatment("exp_multi_holdout_unit_types").Should().Be(2);

        context.SetUnit("user_id", Uid);

        context.GetTreatment("exp_multi_holdout_unit_types").Should().Be(2);
        context.PendingCount.Should().Be(4);
    }

    // One holdout covering two unit types keeps a decision, and an exposure, per unit type.
    [Test]
    public void OneHoldoutCoveringTwoUnitTypesKeepsIndependentExposureStatePerUnitType()
    {
        var context = CreateContext(new ContextConfig().SetUnit("user_id", Uid).SetUnit("session_id", Uid),
            DataOf(new[] { NewHoldout(11, "holdout_shared", "user_id", HoldoutBSeedHi, HoldoutBSeedLo, "full") },
                CoveredBy(NewExperiment(1, "exp_unit_a1", "user_id", 0), 11),
                CoveredBy(NewExperiment(2, "exp_unit_b", "session_id", 0), 11),
                CoveredBy(NewExperiment(3, "exp_unit_a2", "user_id", 0), 11)));

        context.GetTreatment("exp_unit_a1").Should().Be(NormalVariant);
        context.GetTreatment("exp_unit_b").Should().Be(NormalVariant);
        context.GetTreatment("exp_unit_a2").Should().Be(NormalVariant);
        context.PendingCount.Should().Be(5);

        var exposures = Publish(context).Exposures;
        exposures.Should().HaveCount(5);
        exposures.Where(e => e.Id == 11).Select(e => e.Unit).Should().Equal("user_id", "session_id");
    }

    [Test]
    public void SetUnitWithNoCachedAssignmentDoesNotThrowAndSubsequentAssignmentIsNormal()
    {
        var context = CreateContextWithoutUnits(DataOf(
            new[] { NewHoldout(11, "holdout_a", UnitType, HoldoutBSeedHi, HoldoutBSeedLo, "full") },
            CoveredBy(NewExperiment(1, "exp_no_prior_assignment"), 11)));

        context.SetUnit(UnitType, Uid);

        context.GetTreatment("exp_no_prior_assignment").Should().Be(NormalVariant);
    }

    [Test]
    public void SetUnitCalledTwiceWithSameUidIsANoOpAndDoesNotThrow()
    {
        var context = CreateContext(DataOf(new[] { NewHoldout(11, "holdout_a", HoldoutBSeedHi, HoldoutBSeedLo) },
            CoveredBy(NewExperiment(1, "exp_set_unit_twice"), 11)));

        context.GetTreatment("exp_set_unit_twice").Should().Be(NormalVariant);
        context.PendingCount.Should().Be(2);

        context.SetUnit(UnitType, Uid);

        context.GetTreatment("exp_set_unit_twice").Should().Be(NormalVariant);
        context.PendingCount.Should().Be(2);
    }

    // A fully resolved (no null entry) unexposed assignment survives a redundant SetUnit: a
    // same-iteration split edit, which does not invalidate it, would otherwise change its variant.
    [Test]
    public void SetUnitRedundantCallDoesNotEvictFullyResolvedUnexposedHoldoutSnapshot()
    {
        var holdout = NewHoldout(11, "holdout_a", HoldoutBSeedHi, HoldoutBSeedLo);
        var context = CreateContext(DataOf(new[] { holdout }, CoveredBy(NewExperiment(1, "exp_redundant_set_unit"), 11)));

        context.PeekTreatment("exp_redundant_set_unit").Should().Be(NormalVariant);
        context.PendingCount.Should().Be(0);

        var refreshed = CoveredBy(NewExperiment(1, "exp_redundant_set_unit"), 11);
        refreshed.Split = new[] { 1.0, 0.0 };
        Refresh(context, DataOf(new[] { holdout }, refreshed));

        context.SetUnit(UnitType, Uid);

        context.GetTreatment("exp_redundant_set_unit").Should().Be(NormalVariant);
        context.PendingCount.Should().Be(2);
        AssertPublished(context, Uid, Exposure(1, "exp_redundant_set_unit", NormalVariant),
            HoldoutExposure(11, "holdout_a", 1));
    }

    [Test]
    public void PeekVariableValueThenSetUnitThenGetVariableValueRecomputesUnderLateSuppressingHoldout()
    {
        var experiment = CoveredBy(NewExperiment(1, "exp_var_late_unit_suppress", "user_id", 1), 11);
        experiment.Variants[1].Config = "{\"my_var\":\"value_from_variant\"}";
        var context = CreateContextWithoutUnits(DataOf(
            new[] { NewHoldout(11, "holdout_var_late", "user_id", HoldoutASeedHi, HoldoutASeedLo, "full") },
            experiment));

        context.PeekVariableValue("my_var", "default").Should().Be("value_from_variant");
        context.PendingCount.Should().Be(0);

        context.SetUnit("user_id", Uid);

        context.GetVariableValue("my_var", "default").Should().Be("default");
        context.PendingCount.Should().Be(1);
        AssertPublished(context, "user_id", Uid, HoldoutExposure(11, "holdout_var_late", 0, "user_id"));
    }

    // As JS, an override pins its holdout decisions too; one made while the unit was missing is
    // evicted by SetUnit like any other unexposed assignment, so the holdout still fires.
    [Test]
    public void SetUnitDoesNotEvictOverrideAssignmentAndHoldoutResolvesLiveAtExposureTime()
    {
        var context = CreateContext(new ContextConfig().SetOverride("exp_override_late_unit", 3), DataOf(
            new[] { NewHoldout(11, "holdout_override_late", "user_id", HoldoutBSeedHi, HoldoutBSeedLo, "full") },
            CoveredBy(NewExperiment(1, "exp_override_late_unit", "user_id", 0), 11)));

        context.PeekTreatment("exp_override_late_unit").Should().Be(3);

        context.SetUnit("user_id", Uid);

        context.GetTreatment("exp_override_late_unit").Should().Be(3);
        context.PendingCount.Should().Be(2);
        AssertPublished(context, "user_id", Uid,
            Exposure(1, "exp_override_late_unit", 3, assigned: false, overridden: true, unitType: "user_id"),
            HoldoutExposure(11, "holdout_override_late", 1, "user_id"));
    }

    [Test]
    public void SetUnitEvictsNullSnapshotForCustomAssignmentAndCustomAppliesAfterRecompute()
    {
        var context = CreateContext(new ContextConfig().SetCustomAssignment("exp_custom_late_unit", 3), DataOf(
            new[] { NewHoldout(11, "holdout_custom_late", "user_id", HoldoutBSeedHi, HoldoutBSeedLo, "full") },
            CoveredBy(NewExperiment(1, "exp_custom_late_unit", "user_id", 0), 11)));

        context.PeekTreatment("exp_custom_late_unit").Should().Be(0);

        context.SetUnit("user_id", Uid);

        context.GetTreatment("exp_custom_late_unit").Should().Be(3);
        context.PendingCount.Should().Be(2);
        AssertPublished(context, "user_id", Uid,
            Exposure(1, "exp_custom_late_unit", 3, custom: true, unitType: "user_id"),
            HoldoutExposure(11, "holdout_custom_late", 1, "user_id"));
    }

    #region Cross-SDK verdict vectors (collector TestCollectorHoldoutVerdictVectors)

    private const string VerdictUnitType = "verdict_unit_type";

    private Context VerdictContext(string uid, params Experiment[] holdouts)
    {
        var experiment = CoveredBy(NewExperiment(9, "verdict_vectors_experiment", VerdictUnitType, 0),
            holdouts.Select(h => h.Id).ToArray());
        return CreateContext(new ContextConfig().SetUnit(VerdictUnitType, uid), DataOf(holdouts, experiment));
    }

    private static Experiment VerdictHoldout(int id, int seedHi, int seedLo, double[] split)
    {
        var holdout = NewHoldout(id, "holdout_" + id, VerdictUnitType, seedHi, seedLo, "full");
        holdout.Split = split;
        return holdout;
    }

    private void AssertHeldOutBy(Context context, int holdoutId, string holdoutName)
    {
        context.GetTreatment("verdict_vectors_experiment").Should().Be(0);
        var exposure = Publish(context).Exposures.Should().ContainSingle().Subject;
        (exposure.Id, exposure.Name, exposure.Variant).Should().Be((holdoutId, holdoutName, 0));
    }

    private void AssertNotHeldOut(Context context, int holdoutId, string holdoutName)
    {
        var variant = context.GetTreatment("verdict_vectors_experiment");
        var exposures = Publish(context).Exposures;
        exposures.Should().Contain(e => e.Id == 9 && e.Name == "verdict_vectors_experiment" && e.Assigned &&
                                        e.Variant == variant);
        exposures.Should().Contain(e => e.Id == holdoutId && e.Name == holdoutName && e.Variant == 1);
    }

    [Test]
    public void UnicodeUnitIds()
    {
        AssertHeldOutBy(VerdictContext("façade", VerdictHoldout(107, 0, 42, new[] { 0.1, 0.9 })), 107,
            "holdout_107");
        AssertNotHeldOut(VerdictContext("unicode_emoji_🚀_0", VerdictHoldout(107, 0, 42, new[] { 0.1, 0.9 })),
            107, "holdout_107");
    }

    [Test]
    public void SignedSeedHalves()
    {
        // 0x8000000080000000: the sign bit is set in both 32-bit halves.
        const long seed = -9223372034707292160L;
        var seedHi = (int)(seed >> 32);
        var seedLo = unchecked((int)seed);

        AssertHeldOutBy(VerdictContext("signed_unit_4", VerdictHoldout(106, seedHi, seedLo, new[] { 0.1, 0.9 })), 106,
            "holdout_106");
        AssertNotHeldOut(VerdictContext("signed_unit_b_1", VerdictHoldout(106, seedHi, seedLo, new[] { 0.1, 0.9 })),
            106, "holdout_106");
    }

    [Test]
    public void PercentageBoundaryAtOnePercent()
    {
        AssertHeldOutBy(VerdictContext("pct1_unit_188", VerdictHoldout(105, 0, 999999, new[] { 0.01, 0.99 })), 105,
            "holdout_105");
        AssertNotHeldOut(VerdictContext("pct1_unit_0", VerdictHoldout(105, 0, 999999, new[] { 0.01, 0.99 })), 105,
            "holdout_105");
    }

    [Test]
    public void AssignmentProbabilityBoundaryAroundTenPercent()
    {
        AssertHeldOutBy(VerdictContext("boundary_unit_175653", VerdictHoldout(107, 0, 42, new[] { 0.1, 0.9 })), 107,
            "holdout_107");
        AssertNotHeldOut(VerdictContext("boundary_unit_75792", VerdictHoldout(107, 0, 42, new[] { 0.1, 0.9 })), 107,
            "holdout_107");
    }

    [Test]
    public void UnitHeldOutByTwoHoldoutsSimultaneously()
    {
        var context = VerdictContext("dual_holdout_unit_5", VerdictHoldout(107, 0, 42, new[] { 0.1, 0.9 }),
            VerdictHoldout(108, 0, 55, new[] { 0.1, 0.9 }));

        context.GetTreatment("verdict_vectors_experiment").Should().Be(0);

        AssertPublished(context, VerdictUnitType, "dual_holdout_unit_5",
            HoldoutExposure(107, "holdout_107", 0, VerdictUnitType),
            HoldoutExposure(108, "holdout_108", 0, VerdictUnitType));
    }

    #endregion

    [Test]
    public void VariableLookupFiresHoldoutExposureForBothHeldOutAndNonHeldOutUnits()
    {
        var heldOutExperiment = CoveredBy(NewExperiment(1, "exp_var_held_out"), 11);
        heldOutExperiment.Variants[1].Config = "{\"var_a\":\"value_a\"}";
        var heldOut = CreateContext(DataOf(new[] { NewHoldout(11, "holdout_a", HoldoutASeedHi, HoldoutASeedLo) },
            heldOutExperiment));

        heldOut.GetVariableValue("var_a", "default").Should().Be("default");
        heldOut.PendingCount.Should().Be(1);

        var notHeldOutExperiment = CoveredBy(NewExperiment(1, "exp_var_not_held_out"), 11);
        notHeldOutExperiment.Variants[1].Config = "{\"var_a\":\"value_a\"}";
        var notHeldOut = CreateContext(UidNotHeldOut,
            DataOf(new[] { NewHoldout(11, "holdout_a", HoldoutASeedHi, HoldoutASeedLo) }, notHeldOutExperiment));

        notHeldOut.GetVariableValue("var_a", "default").Should().Be("default");
        notHeldOut.PendingCount.Should().Be(2);
    }

    // A held-out lower-id experiment never wins a shared key over an assigned one, but it was
    // evaluated, so its holdout still fires.
    [Test]
    public void VariableKeyResolutionSkipsSuppressedAssignmentInFavorOfAssignedOne()
    {
        var suppressed = CoveredBy(NewExperiment(1, "exp_var_key_suppressed"), 11);
        suppressed.Variants[1].Config = "{\"shared\":\"from_suppressed\"}";
        var assigned = NewExperiment(2, "exp_var_key_assigned");
        assigned.Variants[1].Config = "{\"shared\":\"from_assigned\"}";
        var context = CreateContext(DataOf(new[] { NewHoldout(11, "holdout_a", HoldoutASeedHi, HoldoutASeedLo) },
            suppressed, assigned));

        context.GetVariableValue("shared", "default").Should().Be("from_assigned");

        AssertPublished(context, Uid, HoldoutExposure(11, "holdout_a", 0),
            Exposure(2, "exp_var_key_assigned", NormalVariant));
    }

    [Test]
    public void PeekVariableValueNeverTriggersHoldoutExposureForEvaluatedCandidates()
    {
        var suppressed = CoveredBy(NewExperiment(1, "exp_var_key_peek_suppressed"), 11);
        suppressed.Variants[1].Config = "{\"shared_peek\":\"from_suppressed\"}";
        var assigned = NewExperiment(2, "exp_var_key_peek_assigned");
        assigned.Variants[1].Config = "{\"shared_peek\":\"from_assigned\"}";
        var context = CreateContext(DataOf(new[] { NewHoldout(11, "holdout_a", HoldoutASeedHi, HoldoutASeedLo) },
            suppressed, assigned));

        context.PeekVariableValue("shared_peek", "default").Should().Be("from_assigned");
        context.PendingCount.Should().Be(0);
    }

    [Test]
    public void ThrowingLoggerDoesNotPermanentlyLoseSiblingHoldoutExposure()
    {
        ThrowOnExposure(e => e.Id == 11);
        var context = CreateContext(DataOf(new[]
        {
            NewHoldout(11, "holdout_a", HoldoutASeedHi, HoldoutASeedLo),
            NewHoldout(12, "holdout_b", HoldoutBSeedHi, HoldoutBSeedLo)
        }, CoveredBy(NewExperiment(1, "exp_multi_holdout_throwing_logger"), 11, 12)));

        var act = () => context.GetTreatment("exp_multi_holdout_throwing_logger");

        act.Should().Throw<InvalidOperationException>().WithMessage("boom");
        context.PendingCount.Should().Be(2);
    }

    [Test]
    public void SiblingHoldoutLoggerFailuresRethrowTheFirstAndQueueBothExposures()
    {
        var first = new InvalidOperationException("first");
        _eventLogger.Setup(x => x.HandleEvent(It.IsAny<IContext>(), EventType.Exposure,
                It.Is<object>(o => o is Exposure && ((Exposure)o).Id == 11)))
            .Throws(first);
        _eventLogger.Setup(x => x.HandleEvent(It.IsAny<IContext>(), EventType.Exposure,
                It.Is<object>(o => o is Exposure && ((Exposure)o).Id == 12)))
            .Throws(new InvalidOperationException("second"));
        var context = CreateContext(DataOf(new[]
        {
            NewHoldout(11, "holdout_a", HoldoutASeedHi, HoldoutASeedLo),
            NewHoldout(12, "holdout_b", HoldoutBSeedHi, HoldoutBSeedLo)
        }, CoveredBy(NewExperiment(1, "exp_sibling_failures"), 12, 11)));

        var act = () => context.GetTreatment("exp_sibling_failures");

        act.Should().Throw<InvalidOperationException>().Which.Should().BeSameAs(first);
        context.PendingCount.Should().Be(2);
        _eventLogger.Reset();
        context.GetTreatment("exp_sibling_failures").Should().Be(0);
        AssertPublished(context, Uid, HoldoutExposure(11, "holdout_a", 0), HoldoutExposure(12, "holdout_b", 1));
    }

    // When both the experiment's exposure and its holdout's throw, the experiment's exception
    // propagates (it is queued first, as JS _triggerExposures) and both stay queued.
    [Test]
    public void ExperimentLoggerFailureIsRethrownBeforeItsHoldoutsFailure()
    {
        var experimentFailure = new InvalidOperationException("experiment");
        _eventLogger.Setup(x => x.HandleEvent(It.IsAny<IContext>(), EventType.Exposure,
                It.Is<object>(o => o is Exposure && ((Exposure)o).Id == 1)))
            .Throws(experimentFailure);
        _eventLogger.Setup(x => x.HandleEvent(It.IsAny<IContext>(), EventType.Exposure,
                It.Is<object>(o => o is Exposure && ((Exposure)o).Id == 11)))
            .Throws(new InvalidOperationException("holdout"));
        var context = CreateContext(DataOf(new[] { NewHoldout(11, "holdout_b", HoldoutBSeedHi, HoldoutBSeedLo) },
            CoveredBy(NewExperiment(1, "exp_both_fail"), 11)));

        var act = () => context.GetTreatment("exp_both_fail");

        act.Should().Throw<InvalidOperationException>().Which.Should().BeSameAs(experimentFailure);
        context.PendingCount.Should().Be(2);
    }

    [Test]
    public void ThrowingLoggerDoesNotMakeExperimentOrHoldoutExposureRetryable()
    {
        ThrowOnExposure(e => e.Id == 1);
        var context = CreateContext(DataOf(new[] { NewHoldout(11, "holdout_terminal", HoldoutBSeedHi, HoldoutBSeedLo) },
            CoveredBy(NewExperiment(1, "exp_throwing_logger_terminal"), 11)));

        var act = () => context.GetTreatment("exp_throwing_logger_terminal");
        act.Should().Throw<InvalidOperationException>();
        _eventLogger.Reset();
        context.GetTreatment("exp_throwing_logger_terminal").Should().Be(NormalVariant);

        AssertPublished(context, Uid, Exposure(1, "exp_throwing_logger_terminal", NormalVariant),
            HoldoutExposure(11, "holdout_terminal", 1));
    }

    // On the variable path, a logger throwing on a losing candidate's holdout exposure still
    // propagates (as Java) once the key is resolved; that exposure stays queued, and so does the
    // winner's own.
    [Test]
    public void ThrowingLoggerOnAVariableCandidatesHoldoutPropagates()
    {
        ThrowOnExposure(e => e.Id == 11);
        var suppressed = CoveredBy(NewExperiment(1, "exp_var_throw_suppressed"), 11);
        suppressed.Variants[1].Config = "{\"throw_key\":\"from_suppressed\"}";
        var assigned = NewExperiment(2, "exp_var_throw_assigned");
        assigned.Variants[1].Config = "{\"throw_key\":\"from_assigned\"}";
        var context = CreateContext(DataOf(new[] { NewHoldout(11, "holdout_a", HoldoutASeedHi, HoldoutASeedLo) },
            suppressed, assigned));

        var act = () => context.GetVariableValue("throw_key", "default");

        act.Should().Throw<InvalidOperationException>().WithMessage("boom");
        context.PendingCount.Should().Be(2);
        _eventLogger.Reset();
        AssertPublished(context, Uid, HoldoutExposure(11, "holdout_a", 0),
            Exposure(2, "exp_var_throw_assigned", NormalVariant));
    }

    // Two losing candidates with distinct holdouts: the first holdout's logger throws, and the
    // second candidate's holdout is still queued before the first exception propagates.
    [Test]
    public void ThrowingLoggerDoesNotSkipLaterVariableCandidatesHoldouts()
    {
        ThrowOnExposure(e => e.Id == 11);
        var first = CoveredBy(NewExperiment(1, "exp_var_loser_a"), 11);
        first.Variants[1].Config = "{\"loser_key\":\"a\"}";
        var second = CoveredBy(NewExperiment(2, "exp_var_loser_b"), 12);
        second.Variants[1].Config = "{\"loser_key\":\"b\"}";
        var context = CreateContext(DataOf(new[]
        {
            NewHoldout(11, "holdout_a", HoldoutASeedHi, HoldoutASeedLo),
            NewHoldout(12, "holdout_b", HoldoutASeedHi, HoldoutASeedLo)
        }, first, second));

        var act = () => context.GetVariableValue("loser_key", "default");

        act.Should().Throw<InvalidOperationException>().WithMessage("boom");
        context.PendingCount.Should().Be(2);
        _eventLogger.Reset();
        AssertPublished(context, Uid, HoldoutExposure(11, "holdout_a", 0), HoldoutExposure(12, "holdout_b", 0));
    }

    [Test]
    public void ThrowingLoggerOnALosingCandidateStillExposesTheWinnerAndItsHoldouts()
    {
        ThrowOnExposure(e => e.Id == 11);
        var first = CoveredBy(NewExperiment(1, "exp_var_held_out"), 11);
        first.Variants[1].Config = "{\"winner_key\":\"a\"}";
        var winner = CoveredBy(NewExperiment(2, "exp_var_winner"), 12);
        winner.Variants[1].Config = "{\"winner_key\":\"b\"}";
        var context = CreateContext(DataOf(new[]
        {
            NewHoldout(11, "holdout_a", HoldoutASeedHi, HoldoutASeedLo),
            NewHoldout(12, "holdout_b", HoldoutBSeedHi, HoldoutBSeedLo)
        }, first, winner));

        var act = () => context.GetVariableValue("winner_key", "default");

        act.Should().Throw<InvalidOperationException>().WithMessage("boom");
        context.PendingCount.Should().Be(3);
        _eventLogger.Reset();
        context.GetVariableValue("winner_key", "default").Should().Be("b");
        context.PendingCount.Should().Be(3);
        AssertPublished(context, Uid, HoldoutExposure(11, "holdout_a", 0),
            Exposure(2, "exp_var_winner", NormalVariant), HoldoutExposure(12, "holdout_b", 1));
    }

    [Test]
    public void UncoveredCachedReadsAllocateNoHoldoutState()
    {
        var context = CreateContext(DataOf(WithVariables(NewExperiment(1, "exp_cached_plain"), "plain_key")));
        for (var i = 0; i < 1000; i++)
        {
            context.GetTreatment("exp_cached_plain");
            context.PeekTreatment("exp_cached_plain");
            context.GetVariableValue("plain_key", "default");
        }

        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 1000; i++)
        {
            context.GetTreatment("exp_cached_plain");
            context.PeekTreatment("exp_cached_plain");
            context.GetVariableValue("plain_key", "default");
        }

        (GC.GetAllocatedBytesForCurrentThread() - before).Should().Be(0);
    }

    // A warmed covered read reuses the coverage resolved when the data was installed: no
    // per-read resolving, sorting or allocation.
    [Test]
    public void CoveredCachedReadsDoNotResolveCoverageAgain()
    {
        var context = CreateContext(DataOf(new[] { NewHoldout(11, "holdout_a", HoldoutASeedHi, HoldoutASeedLo) },
            CoveredBy(NewExperiment(1, "exp_cached_coverage"), 11)));
        for (var i = 0; i < 1000; i++) context.GetTreatment("exp_cached_coverage");

        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 1000; i++) context.GetTreatment("exp_cached_coverage");

        (GC.GetAllocatedBytesForCurrentThread() - before).Should().Be(0);
    }

    [Test]
    public void HoldoutDecisionStateIsCreatedOnlyOnTheFirstHoldoutDecision()
    {
        var field = typeof(Context).GetField("_holdoutAssignments", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var plain = CreateContext(DataOf(NewExperiment(1, "exp_no_holdouts")));
        plain.GetTreatment("exp_no_holdouts");
        field.GetValue(plain).Should().BeNull();

        var covered = CreateContext(DataOf(new[] { NewHoldout(11, "holdout_a", HoldoutASeedHi, HoldoutASeedLo) },
            CoveredBy(NewExperiment(1, "exp_lazy_holdouts"), 11)));
        field.GetValue(covered).Should().BeNull();
        covered.GetTreatment("exp_lazy_holdouts").Should().Be(0);
        field.GetValue(covered).Should().NotBeNull();
    }

    // When several candidates' holdout loggers throw, the first failure is the one rethrown.
    [Test]
    public void VariablePathRethrowsTheFirstHoldoutLoggerFailure()
    {
        _eventLogger.Setup(x => x.HandleEvent(It.IsAny<IContext>(), EventType.Exposure,
                It.Is<object>(o => o is Exposure && ((Exposure)o).Id == 11)))
            .Throws(new InvalidOperationException("first"));
        _eventLogger.Setup(x => x.HandleEvent(It.IsAny<IContext>(), EventType.Exposure,
                It.Is<object>(o => o is Exposure && ((Exposure)o).Id == 12)))
            .Throws(new InvalidOperationException("second"));
        var first = CoveredBy(NewExperiment(1, "exp_var_first"), 11);
        first.Variants[1].Config = "{\"order_key\":\"a\"}";
        var second = CoveredBy(NewExperiment(2, "exp_var_second"), 12);
        second.Variants[1].Config = "{\"order_key\":\"b\"}";
        var context = CreateContext(DataOf(new[]
        {
            NewHoldout(11, "holdout_a", HoldoutASeedHi, HoldoutASeedLo),
            NewHoldout(12, "holdout_b", HoldoutASeedHi, HoldoutASeedLo)
        }, first, second));

        var act = () => context.GetVariableValue("order_key", "default");

        act.Should().Throw<InvalidOperationException>().WithMessage("first");
        context.PendingCount.Should().Be(2);
    }

    // The held-out experiment has no exposure of its own, so only the holdout's exposure can
    // schedule the flush, even when the logger throws on it.
    [Test]
    public void ThrowingLoggerOnSuppressedExperimentsOnlyHoldoutStillSchedulesFlush()
    {
        ThrowOnExposure(_ => true);
        var context = CreateContext(DataOf(new[] { NewHoldout(11, "holdout_a", HoldoutASeedHi, HoldoutASeedLo) },
            CoveredBy(NewExperiment(1, "exp_stranded_flush"), 11)));
        FlushScheduled(context).Should().BeFalse();

        var act = () => context.GetTreatment("exp_stranded_flush");

        act.Should().Throw<InvalidOperationException>();
        context.PendingCount.Should().Be(1);
        FlushScheduled(context).Should().BeTrue();
    }

    [Test]
    public void NullHoldoutArrayElementIsIgnoredAndExperimentAssignsNormally()
    {
        var context = CreateContext(DataOf(new Experiment?[] { null }, NewExperiment(1, "exp_null_holdout_entry")));

        context.GetTreatment("exp_null_holdout_entry").Should().Be(NormalVariant);
        AssertPublished(context, Uid, Exposure(1, "exp_null_holdout_entry", NormalVariant));
    }

    [Test]
    public void HoldoutWithNullSplitIsIgnoredAndExperimentAssignsNormally()
    {
        var malformed = NewHoldout(11, "holdout_null_split", HoldoutASeedHi, HoldoutASeedLo);
        malformed.Split = null!;
        var context = CreateContext(DataOf(new[] { malformed }, CoveredBy(NewExperiment(1, "exp_null_split_holdout"), 11)));

        context.GetTreatment("exp_null_split_holdout").Should().Be(NormalVariant);
        AssertPublished(context, Uid, Exposure(1, "exp_null_split_holdout", NormalVariant));
    }

    [Test]
    public void HoldoutWithEmptySplitIsIgnoredAndExperimentAssignsNormally()
    {
        var malformed = NewHoldout(11, "holdout_empty_split", HoldoutASeedHi, HoldoutASeedLo);
        malformed.Split = Array.Empty<double>();
        var context = CreateContext(DataOf(new[] { malformed }, CoveredBy(NewExperiment(1, "exp_empty_split_holdout"), 11)));

        context.GetTreatment("exp_empty_split_holdout").Should().Be(NormalVariant);
        AssertPublished(context, Uid, Exposure(1, "exp_empty_split_holdout", NormalVariant));
    }

    // A stale holdout definition (an older iteration) reaching the holdout resolution must resolve
    // the live one by id and reuse its exposed decision, not overwrite it.
    [Test]
    public void StaleHoldoutReferenceNeverOverwritesNewerPinnedCacheEntry()
    {
        var context = CreateContext(DataOf(new[] { NewHoldout(11, "holdout_h", HoldoutASeedHi, HoldoutASeedLo) },
            CoveredBy(NewExperiment(1, "exp_f"), 11)));

        context.GetTreatment("exp_f").Should().Be(0);
        context.PendingCount.Should().Be(1);

        var iteration2 = NewHoldout(11, "holdout_h", HoldoutBSeedHi, HoldoutBSeedLo);
        iteration2.Iteration = 2;
        Refresh(context, DataOf(new[] { iteration2 }, CoveredBy(NewExperiment(1, "exp_f"), 11),
            CoveredBy(NewExperiment(2, "exp_g"), 11)));

        context.GetTreatment("exp_g").Should().Be(NormalVariant);
        context.PendingCount.Should().Be(3);

        var stale = NewHoldout(11, "holdout_h", HoldoutASeedHi, HoldoutASeedLo);
        var resolve = typeof(Context).GetMethod("GetHoldoutAssignment", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var resolved = (Context.Assignment)resolve.Invoke(context, new object[] { stale, UnitType, InstalledHoldouts(context) })!;

        resolved.Iteration.Should().Be(2);
        resolved.Variant.Should().Be(1);
        resolved.Exposed.Should().Be(1);
        context.PendingCount.Should().Be(3);
        AssertPublished(context, Uid, HoldoutExposure(11, "holdout_h", 0), Exposure(2, "exp_g", NormalVariant),
            HoldoutExposure(11, "holdout_h", 1));
    }

    // An override is rebuilt when a refresh changes its holdout coverage, so a newly applicable
    // holdout still fires (and the override is exposed again, like any rebuilt assignment).
    [Test]
    public void HoldoutBecomingApplicableAfterRefreshStillFiresForOverriddenExperiment()
    {
        var context = CreateContext(new ContextConfig().SetUnit(UnitType, Uid).SetOverride("exp_override_late_holdout", 3),
            DataOf(NewExperiment(1, "exp_override_late_holdout")));

        context.GetTreatment("exp_override_late_holdout").Should().Be(3);
        context.PendingCount.Should().Be(1);

        Refresh(context, DataOf(new[] { NewHoldout(11, "holdout_late", HoldoutASeedHi, HoldoutASeedLo) },
            CoveredBy(NewExperiment(1, "exp_override_late_holdout"), 11)));

        context.GetTreatment("exp_override_late_holdout").Should().Be(3);
        context.PendingCount.Should().Be(3);

        var own = Exposure(1, "exp_override_late_holdout", 3, assigned: false, overridden: true);
        AssertPublished(context, Uid, own, own, HoldoutExposure(11, "holdout_late", 0));
    }

    [Test]
    public void VariablePathHoldoutExposureSchedulesFlushWithoutAnyOtherEvent()
    {
        var suppressed = CoveredBy(NewExperiment(1, "exp_var_flush_suppressed"), 11);
        suppressed.Variants[1].Config = "{\"flush_key\":\"from_suppressed\"}";
        var assigned = NewExperiment(2, "exp_var_flush_assigned");
        assigned.Variants[1].Config = "{\"flush_key\":\"from_assigned\"}";
        var context = CreateContext(DataOf(new[] { NewHoldout(11, "holdout_flush", HoldoutASeedHi, HoldoutASeedLo) },
            suppressed, assigned));
        // The winner is exposed first, so the variable read itself queues only the holdout.
        context.GetTreatment("exp_var_flush_assigned");
        context.Publish();
        FlushScheduled(context).Should().BeFalse();

        context.GetVariableValue("flush_key", "default").Should().Be("from_assigned");

        context.PendingCount.Should().Be(1);
        FlushScheduled(context).Should().BeTrue();
    }

    [Test]
    public void SetDataDoesNotMutateCallerSuppliedHoldoutIdsArray()
    {
        var callerArray = new[] { 12, 11 }; // unsorted, with a dangling id
        var covered = NewExperiment(1, "exp_holdout_no_mutate_in");
        covered.HoldoutIds = callerArray;
        var context = CreateContext(DataOf(
            new[] { NewHoldout(11, "holdout_no_mutate", HoldoutASeedHi, HoldoutASeedLo) },
            covered, NewExperiment(2, "exp_holdout_no_mutate_not_covered")));

        context.GetTreatment("exp_holdout_no_mutate_in").Should().Be(0);
        context.GetTreatment("exp_holdout_no_mutate_not_covered").Should().Be(NormalVariant);
        callerArray.Should().Equal(12, 11);
    }

    #region Caching and ordering (beyond the Java port; each kills a measured mutant)

    // Counts the assignment rebuilds (cache misses that take the write lock).
    private static Func<int> CountRebuilds(Context context)
    {
        var rebuilds = 0;
        void Arm() => context.BeforeAssignmentRebuild = () =>
        {
            rebuilds++;
            Arm();
        };
        Arm();
        return () => rebuilds;
    }

    private static Context.Assignment CachedAssignment(Context context, string experimentName)
    {
        var cache = (Dictionary<string, Context.Assignment>)typeof(Context)
            .GetField("_assignmentCache", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(context)!;
        return cache[experimentName];
    }

    // A held-out assignment never applies the custom assignment, so a custom variant other than
    // control does not make it stale: it is not rebuilt on every read.
    [Test]
    public void HeldOutAssignmentWithCustomAssignmentIsNotRebuiltOnEveryRead()
    {
        var context = CreateContext(
            new ContextConfig().SetUnit(UnitType, Uid).SetCustomAssignment("exp_held_out_custom", 1),
            DataOf(new[] { NewHoldout(11, "holdout_a", HoldoutASeedHi, HoldoutASeedLo) },
                CoveredBy(NewExperiment(1, "exp_held_out_custom"), 11)));
        context.GetTreatment("exp_held_out_custom").Should().Be(0);
        var rebuilds = CountRebuilds(context);

        for (var i = 0; i < 3; i++) context.GetTreatment("exp_held_out_custom").Should().Be(0);

        rebuilds().Should().Be(0);
    }

    // As JS, a held-out experiment's audience is not evaluated, and a later attribute change does
    // not re-evaluate it either.
    [Test]
    public void HeldOutExperimentDoesNotEvaluateItsAudience()
    {
        var experiment = CoveredBy(NewExperiment(1, "exp_held_out_audience"), 11);
        experiment.Audience = "{\"filter\":[{\"gte\":[{\"var\":\"age\"},{\"value\":20}]}]}";
        var context = CreateContext(DataOf(new[] { NewHoldout(11, "holdout_a", HoldoutASeedHi, HoldoutASeedLo) },
            experiment));
        context.SetAttribute("age", 5);

        context.GetTreatment("exp_held_out_audience").Should().Be(0);
        CachedAssignment(context, "exp_held_out_audience").AudienceMismatch.Should().BeFalse();

        var rebuilds = CountRebuilds(context);
        context.SetAttribute("age", 6);
        context.GetTreatment("exp_held_out_audience").Should().Be(0);
        rebuilds().Should().Be(0);
    }

    // An assignment without holdouts allocates no holdout state (one null reference), so payloads
    // without holdouts cost no extra memory per assignment beyond it.
    [Test]
    public void AssignmentWithoutHoldoutsAllocatesNoHoldoutState()
    {
        var context = CreateContext(DataOf(new[] { NewHoldout(11, "holdout_a", HoldoutASeedHi, HoldoutASeedLo) },
            NewExperiment(1, "exp_uncovered"), CoveredBy(NewExperiment(2, "exp_covered"), 11)));

        context.GetTreatment("exp_uncovered").Should().Be(NormalVariant);
        context.GetTreatment("exp_covered").Should().Be(0);

        CachedAssignment(context, "exp_uncovered").Holdout.Should().BeNull();
        CachedAssignment(context, "exp_covered").Holdout.Should().NotBeNull();
    }

    // With nothing assigned, a held-out experiment gives its control value even when a lower-id
    // candidate (unassigned for want of its unit) has the key too; the first held-out one wins.
    [Test]
    public void SharedVariableKeyFallsBackToTheFirstHeldOutExperimentsControlValue()
    {
        var unassigned = NewExperiment(1, "exp_unassigned", "device_id", 0);
        unassigned.Variants = new[]
        {
            new ExperimentVariant { Name = "A", Config = "{\"color\":\"unassigned_control\"}" },
            new ExperimentVariant { Name = "B", Config = "{\"color\":\"unassigned_treatment\"}" }
        };
        var firstHeldOut = CoveredBy(NewExperiment(2, "exp_held_out_first"), 11);
        firstHeldOut.Variants = new[]
        {
            new ExperimentVariant { Name = "A", Config = "{\"color\":\"first_control\"}" },
            new ExperimentVariant { Name = "B", Config = "{\"color\":\"first_treatment\"}" }
        };
        var secondHeldOut = CoveredBy(NewExperiment(3, "exp_held_out_second"), 11);
        secondHeldOut.Variants = new[]
        {
            new ExperimentVariant { Name = "A", Config = "{\"color\":\"second_control\"}" },
            new ExperimentVariant { Name = "B", Config = "{\"color\":\"second_treatment\"}" }
        };
        var context = CreateContext(DataOf(new[] { NewHoldout(11, "holdout_a", HoldoutASeedHi, HoldoutASeedLo) },
            unassigned, firstHeldOut, secondHeldOut));

        context.PeekVariableValue("color", "default").Should().Be("first_control");
        context.GetVariableValue("color", "default").Should().Be("first_control");
    }

    // The holdouts are applied, and their exposures published, in id order whatever the order of
    // holdoutIds; a duplicated id counts once, so a refresh dropping the duplicate is not a
    // coverage change (no re-exposure).
    [Test]
    public void HoldoutIdsAreSortedAndDeduplicated()
    {
        var context = CreateContext(UidNotHeldOut, DataOf(new[]
        {
            NewHoldout(11, "holdout_a", HoldoutASeedHi, HoldoutASeedLo),
            NewHoldout(12, "holdout_b", HoldoutBSeedHi, HoldoutBSeedLo)
        }, CoveredBy(NewExperiment(1, "exp_unsorted"), 12, 11, 12)));

        context.GetTreatment("exp_unsorted").Should().Be(0);
        context.PendingCount.Should().Be(3);

        Refresh(context, DataOf(new[]
        {
            NewHoldout(11, "holdout_a", HoldoutASeedHi, HoldoutASeedLo),
            NewHoldout(12, "holdout_b", HoldoutBSeedHi, HoldoutBSeedLo)
        }, CoveredBy(NewExperiment(1, "exp_unsorted"), 11, 12)));

        context.GetTreatment("exp_unsorted").Should().Be(0);
        AssertPublished(context, UidNotHeldOut, Exposure(1, "exp_unsorted", 0), HoldoutExposure(11, "holdout_a", 1),
            HoldoutExposure(12, "holdout_b", 1));
    }

    // A refresh that adds coverage to an exposed rule-won experiment rebuilds it with the new
    // holdout (as JS), so the holdout's exposure is published even though the rule still decides.
    [Test]
    public void RefreshAddingCoverageToAnExposedRuleWonExperimentFiresTheNewHoldout()
    {
        var experiment = NewExperiment(1, "exp_rule_coverage");
        experiment.AssignmentRules = UsRule;
        var context = CreateContext(DataOf(experiment));
        context.SetAttribute("country", "US");
        context.GetTreatment("exp_rule_coverage").Should().Be(1);

        var covered = CoveredBy(NewExperiment(1, "exp_rule_coverage"), 11);
        covered.AssignmentRules = UsRule;
        Refresh(context, DataOf(new[] { NewHoldout(11, "holdout_late", HoldoutASeedHi, HoldoutASeedLo) }, covered));

        context.GetTreatment("exp_rule_coverage").Should().Be(1);
        var exposures = Publish(context).Exposures;
        exposures.Select(e => (e.Name, e.Variant, e.RuleOverride)).Should().Equal(
            ("exp_rule_coverage", 1, true), ("exp_rule_coverage", 1, true), ("holdout_late", 0, false));
    }

    // A refresh installed after the rebuild read the experiment does not mix payloads: the
    // holdouts come from the index installed with that experiment, not the refreshed one (which
    // drops holdout 11 and bumps holdout 12 to an arm that holds the unit out).
    [Test]
    public void ARefreshAfterTheExperimentIsReadDoesNotMixPayloads()
    {
        var context = CreateContext(UidNotHeldOut, DataOf(new[]
        {
            NewHoldout(11, "holdout_a", HoldoutASeedHi, HoldoutASeedLo),
            NewHoldout(12, "holdout_b", HoldoutASeedHi, HoldoutASeedLo)
        }, CoveredBy(NewExperiment(1, "exp_mid_refresh"), 11, 12)));
        var bumped = NewHoldout(12, "holdout_b_v2", HoldoutASeedHi, HoldoutASeedLo);
        bumped.Iteration = 2;
        bumped.Split = new[] { 1.0, 0.0 };
        context.AfterExperimentSnapshot = () =>
        {
            _dataProvider.Setup(x => x.GetContextDataAsync())
                .ReturnsAsync(DataOf(new[] { bumped }, CoveredBy(NewExperiment(1, "exp_mid_refresh"), 12)));
            Task.Run(() => context.RefreshAsync()).Wait(TimeSpan.FromSeconds(10)).Should().BeTrue();
        };

        context.PeekTreatment("exp_mid_refresh").Should().Be(0); // UidNotHeldOut's normal variant

        context.AfterExperimentSnapshot.Should().BeNull();
        var decision = ((Dictionary<string, Context.Assignment>)typeof(Context)
            .GetField("_assignmentCache", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(context)!)["exp_mid_refresh"];
        decision.Suppressed.Should().BeFalse();
        decision.HoldoutAssignments!.Select(h => (h.Id, h.Name, h.Iteration, h.Variant))
            .Should().Equal((11, "holdout_a", 1, 1), (12, "holdout_b", 1, 1));
    }

    [Test]
    public void ALoggerThatStartsClosingTheContextLeavesNoDeferredWinnerExposure()
    {
        var first = CoveredBy(NewExperiment(1, "exp_var_held_out"), 11);
        first.Variants[1].Config = "{\"closing_key\":\"a\"}";
        var later = CoveredBy(NewExperiment(2, "exp_var_later_held_out"), 13);
        later.Variants[1].Config = "{\"closing_key\":\"c\"}";
        var winner = CoveredBy(NewExperiment(3, "exp_var_winner"), 12);
        winner.Variants[1].Config = "{\"closing_key\":\"b\"}";
        var context = CreateContext(DataOf(new[]
        {
            NewHoldout(11, "holdout_a", HoldoutASeedHi, HoldoutASeedLo),
            NewHoldout(12, "holdout_b", HoldoutBSeedHi, HoldoutBSeedLo),
            NewHoldout(13, "holdout_c", HoldoutASeedHi, HoldoutASeedLo)
        }, first, later, winner));
        var publish = new TaskCompletionSource<bool>();
        Mock.Get(_publisher).Setup(x => x.PublishAsync(It.IsAny<IContext>(), It.IsAny<PublishEvent>()))
            .Callback<IContext, PublishEvent>((c, e) => _published.Add((c, e)))
            .Returns(publish.Task);
        Task finalizing = null!;
        _eventLogger.Setup(x => x.HandleEvent(It.IsAny<IContext>(), EventType.Exposure,
                It.Is<object>(o => o is Exposure && ((Exposure)o).Id == 11)))
            .Callback(() =>
            {
                finalizing = context.FinalizeAsync();
                throw new InvalidOperationException("boom");
            });

        var act = () => context.GetVariableValue("closing_key", "default");

        act.Should().Throw<InvalidOperationException>().WithMessage("boom");
        context.IsClosing().Should().BeTrue();
        context.PendingCount.Should().Be(0);
        publish.SetResult(true);
        finalizing.Wait(TimeSpan.FromSeconds(10)).Should().BeTrue();
        context.IsClosed().Should().BeTrue();
        context.PendingCount.Should().Be(0);
        _published.Should().ContainSingle().Which.Event.Exposures.Should().Equal(HoldoutExposure(11, "holdout_a", 0));
    }

    // Pauses the next Millis() call on one thread until released, to interleave a close there.
    private sealed class GateClock : Clock
    {
        private readonly long _millis;
        private int _armedThread = -1;
        public readonly ManualResetEventSlim Paused = new();
        public readonly ManualResetEventSlim Resume = new();

        public GateClock(long millis) => _millis = millis;

        public void ArmCurrentThread() => Volatile.Write(ref _armedThread, Environment.CurrentManagedThreadId);

        public override long Millis()
        {
            if (Interlocked.CompareExchange(ref _armedThread, -1, Environment.CurrentManagedThreadId) ==
                Environment.CurrentManagedThreadId)
            {
                Paused.Set();
                if (!Resume.Wait(TimeSpan.FromSeconds(10))) throw new TimeoutException("GateClock was not released");
            }

            return _millis;
        }
    }

    // Another thread closes the context after the deferred-winner path has started but before the
    // winner is queued: the close has flushed and sealed, so the winner and its holdouts are not
    // left pending, and the logger's exception still propagates.
    [Test]
    public void AConcurrentCloseLeavesNoDeferredWinnerExposure()
    {
        var first = CoveredBy(NewExperiment(1, "exp_var_held_out"), 11);
        first.Variants[1].Config = "{\"race_key\":\"a\"}";
        var winner = CoveredBy(NewExperiment(2, "exp_var_winner"), 12);
        winner.Variants[1].Config = "{\"race_key\":\"b\"}";
        var clock = new GateClock(Now);
        var config = new ContextConfig().SetUnit(UnitType, Uid);
        config.PublishDelay = TimeSpan.FromMinutes(10);
        var context = Context.CreateWithData(config, DataOf(new[]
            {
                NewHoldout(11, "holdout_a", HoldoutASeedHi, HoldoutASeedLo),
                NewHoldout(12, "holdout_b", HoldoutBSeedHi, HoldoutBSeedLo)
            }, first, winner), clock, _dataProvider.Object, _publisher, _eventLogger.Object,
            new DefaultVariableParser(), new AudienceMatcher(new DefaultAudienceDeserializer()), null,
            new Dictionary<string, object> { ["application"] = "website", ["environment"] = "production" });
        _eventLogger.Setup(x => x.HandleEvent(It.IsAny<IContext>(), EventType.Exposure,
                It.Is<object>(o => o is Exposure && ((Exposure)o).Id == 11)))
            .Callback(() =>
            {
                // The next exposure built on this thread is the deferred winner's.
                clock.ArmCurrentThread();
                throw new InvalidOperationException("boom");
            });

        var read = Task.Run(() => context.GetVariableValue("race_key", "default"));
        clock.Paused.Wait(TimeSpan.FromSeconds(10)).Should().BeTrue();
        context.FinalizeAsync().Wait(TimeSpan.FromSeconds(10)).Should().BeTrue();
        context.IsClosed().Should().BeTrue();
        clock.Resume.Set();

        var act = () => read.Wait(TimeSpan.FromSeconds(10));
        act.Should().Throw<AggregateException>().WithInnerException<InvalidOperationException>().WithMessage("boom");
        context.PendingCount.Should().Be(0);
        _published.Should().ContainSingle().Which.Event.Exposures.Should().Equal(HoldoutExposure(11, "holdout_a", 0));
    }

    // An experiment's logger that closes the context (without throwing) has flushed it: the
    // holdout exposures that follow are not left pending on the closed context.
    [Test]
    public void ALoggerThatClosesTheContextLeavesNoHoldoutExposurePending()
    {
        var context = CreateContext(UidNotHeldOut, DataOf(new[] { NewHoldout(11, "holdout_a", HoldoutASeedHi, HoldoutASeedLo) },
            CoveredBy(NewExperiment(1, "exp_logger_closes"), 11)));
        _eventLogger.Setup(x => x.HandleEvent(It.IsAny<IContext>(), EventType.Exposure,
                It.Is<object>(o => o is Exposure && ((Exposure)o).Id == 1)))
            .Callback(() => context.Close());

        context.GetTreatment("exp_logger_closes").Should().Be(0);

        context.IsClosed().Should().BeTrue();
        context.PendingCount.Should().Be(0);
        _published.Should().ContainSingle().Which.Event.Exposures.Should().Equal(Exposure(1, "exp_logger_closes", 0));
    }

    // A read already past its checks when a close starts still has its exposures published by
    // that close: the close waits for an earlier publication, the read then queues the experiment
    // and holdout exposures, and the close's flush sends them.
    [Test]
    public void ExposuresQueuedWhileACloseWaitsAreStillPublishedByIt()
    {
        var clock = new GateClock(Now);
        var config = new ContextConfig().SetUnit(UnitType, UidNotHeldOut);
        config.PublishDelay = TimeSpan.FromMinutes(10);
        var context = Context.CreateWithData(config,
            DataOf(new[] { NewHoldout(11, "holdout_a", HoldoutASeedHi, HoldoutASeedLo) },
                CoveredBy(NewExperiment(1, "exp_closing_read"), 11)), clock, _dataProvider.Object, _publisher,
            _eventLogger.Object, new DefaultVariableParser(), new AudienceMatcher(new DefaultAudienceDeserializer()), null,
            new Dictionary<string, object> { ["application"] = "website", ["environment"] = "production" });
        var earlier = new TaskCompletionSource<bool>();
        var publishes = 0;
        Mock.Get(_publisher).Setup(x => x.PublishAsync(It.IsAny<IContext>(), It.IsAny<PublishEvent>()))
            .Callback<IContext, PublishEvent>((c, e) => _published.Add((c, e)))
            .Returns(() => Interlocked.Increment(ref publishes) == 1 ? earlier.Task : Task.CompletedTask);
        context.Track("goal_before_close", null);
        var earlierPublish = context.PublishAsync();

        var read = Task.Run(() =>
        {
            clock.ArmCurrentThread();
            return context.GetTreatment("exp_closing_read");
        });
        clock.Paused.Wait(TimeSpan.FromSeconds(10)).Should().BeTrue();
        var closing = context.FinalizeAsync();
        context.IsClosing().Should().BeTrue();
        clock.Resume.Set();
        read.Wait(TimeSpan.FromSeconds(10)).Should().BeTrue();
        read.Result.Should().Be(0);
        earlier.SetResult(true);
        earlierPublish.Wait(TimeSpan.FromSeconds(10)).Should().BeTrue();
        closing.Wait(TimeSpan.FromSeconds(10)).Should().BeTrue();

        context.IsClosed().Should().BeTrue();
        context.PendingCount.Should().Be(0);
        _published.Should().HaveCount(2);
        _published[1].Event.Exposures.Should().Equal(Exposure(1, "exp_closing_read", 0), HoldoutExposure(11, "holdout_a", 1));
    }

    // The deferred winner's own exposure is queued and its logger starts a close that fails: the
    // winner's holdout is still exposed (queued during the close, kept by its failure), as on an
    // ordinary read, and the retry publishes all three once.
    [Test]
    public void ADeferredWinnersHoldoutSurvivesAFailedCloseStartedByTheWinnersLogger()
    {
        var first = CoveredBy(NewExperiment(1, "exp_var_held_out"), 11);
        first.Variants[1].Config = "{\"winner_close_key\":\"a\"}";
        var winner = CoveredBy(NewExperiment(2, "exp_var_winner"), 12);
        winner.Variants[1].Config = "{\"winner_close_key\":\"b\"}";
        var context = CreateContext(DataOf(new[]
        {
            NewHoldout(11, "holdout_a", HoldoutASeedHi, HoldoutASeedLo),
            NewHoldout(12, "holdout_b", HoldoutBSeedHi, HoldoutBSeedLo)
        }, first, winner));
        var publish = new TaskCompletionSource<bool>();
        var publishes = 0;
        Mock.Get(_publisher).Setup(x => x.PublishAsync(It.IsAny<IContext>(), It.IsAny<PublishEvent>()))
            .Callback<IContext, PublishEvent>((c, e) => _published.Add((c, e)))
            .Returns(() => Interlocked.Increment(ref publishes) == 1 ? publish.Task : Task.CompletedTask);
        ThrowOnExposure(e => e.Id == 11);
        Task finalizing = null!;
        _eventLogger.Setup(x => x.HandleEvent(It.IsAny<IContext>(), EventType.Exposure,
                It.Is<object>(o => o is Exposure && ((Exposure)o).Id == 2)))
            .Callback(() => finalizing = context.FinalizeAsync());

        var act = () => context.GetVariableValue("winner_close_key", "default");
        act.Should().Throw<InvalidOperationException>().WithMessage("boom");
        publish.SetException(new HttpRequestException("collector down"));
        var settle = () => finalizing.Wait(TimeSpan.FromSeconds(10));
        settle.Should().Throw<AggregateException>();
        context.IsClosed().Should().BeFalse();
        _eventLogger.Reset();
        _published.Clear();

        context.GetVariableValue("winner_close_key", "default").Should().Be("b");

        AssertPublished(context, Uid, HoldoutExposure(11, "holdout_a", 0), Exposure(2, "exp_var_winner", NormalVariant),
            HoldoutExposure(12, "holdout_b", 1));
    }

    // A later held-out candidate's holdout, skipped because the earlier logger started a close that
    // then fails, is exposed by the next read.
    [Test]
    public void ALaterCandidatesHoldoutSkippedByAFailedCloseIsExposedByTheNextRead()
    {
        var first = CoveredBy(NewExperiment(1, "exp_var_held_out_a"), 11);
        first.Variants[1].Config = "{\"suppressed_close_key\":\"a\"}";
        var second = CoveredBy(NewExperiment(2, "exp_var_held_out_b"), 12);
        second.Variants[1].Config = "{\"suppressed_close_key\":\"b\"}";
        var context = CreateContext(DataOf(new[]
        {
            NewHoldout(11, "holdout_a", HoldoutASeedHi, HoldoutASeedLo),
            NewHoldout(12, "holdout_b", HoldoutASeedHi, HoldoutASeedLo)
        }, first, second));
        var publish = new TaskCompletionSource<bool>();
        var publishes = 0;
        Mock.Get(_publisher).Setup(x => x.PublishAsync(It.IsAny<IContext>(), It.IsAny<PublishEvent>()))
            .Callback<IContext, PublishEvent>((c, e) => _published.Add((c, e)))
            .Returns(() => Interlocked.Increment(ref publishes) == 1 ? publish.Task : Task.CompletedTask);
        Task finalizing = null!;
        _eventLogger.Setup(x => x.HandleEvent(It.IsAny<IContext>(), EventType.Exposure,
                It.Is<object>(o => o is Exposure && ((Exposure)o).Id == 11)))
            .Callback(() =>
            {
                finalizing = context.FinalizeAsync();
                throw new InvalidOperationException("boom");
            });

        var act = () => context.GetVariableValue("suppressed_close_key", "default");
        act.Should().Throw<InvalidOperationException>().WithMessage("boom");
        context.PendingCount.Should().Be(0);
        publish.SetException(new HttpRequestException("collector down"));
        var settle = () => finalizing.Wait(TimeSpan.FromSeconds(10));
        settle.Should().Throw<AggregateException>();
        context.IsClosed().Should().BeFalse();
        _eventLogger.Reset();
        _published.Clear();

        context.GetVariableValue("suppressed_close_key", "default").Should().Be("default"); // control has no key

        AssertPublished(context, Uid, HoldoutExposure(11, "holdout_a", 0), HoldoutExposure(12, "holdout_b", 0));
    }

    // The winner's logger starts a close whose publish then succeeds: the winner's holdout, queued
    // after the close took its batch, is published by that close before it seals.
    [Test]
    public void ADeferredWinnersHoldoutQueuedDuringASuccessfulCloseIsPublishedByIt()
    {
        var first = CoveredBy(NewExperiment(1, "exp_var_held_out"), 11);
        first.Variants[1].Config = "{\"close_ok_key\":\"a\"}";
        var winner = CoveredBy(NewExperiment(2, "exp_var_winner"), 12);
        winner.Variants[1].Config = "{\"close_ok_key\":\"b\"}";
        var context = CreateContext(DataOf(new[]
        {
            NewHoldout(11, "holdout_a", HoldoutASeedHi, HoldoutASeedLo),
            NewHoldout(12, "holdout_b", HoldoutBSeedHi, HoldoutBSeedLo)
        }, first, winner));
        var publish = new TaskCompletionSource<bool>();
        var publishes = 0;
        Mock.Get(_publisher).Setup(x => x.PublishAsync(It.IsAny<IContext>(), It.IsAny<PublishEvent>()))
            .Callback<IContext, PublishEvent>((c, e) => _published.Add((c, e)))
            .Returns(() => Interlocked.Increment(ref publishes) == 1 ? publish.Task : Task.CompletedTask);
        ThrowOnExposure(e => e.Id == 11);
        Task finalizing = null!;
        _eventLogger.Setup(x => x.HandleEvent(It.IsAny<IContext>(), EventType.Exposure,
                It.Is<object>(o => o is Exposure && ((Exposure)o).Id == 2)))
            .Callback(() => finalizing = context.FinalizeAsync());

        var act = () => context.GetVariableValue("close_ok_key", "default");
        act.Should().Throw<InvalidOperationException>().WithMessage("boom");
        publish.SetResult(true);
        finalizing.Wait(TimeSpan.FromSeconds(10)).Should().BeTrue();

        context.IsClosed().Should().BeTrue();
        context.PendingCount.Should().Be(0);
        _published.SelectMany(p => p.Event.Exposures!).Select(e => e.Id).Should().Equal(11, 2, 12);
        // Each batch the close published is reported, as a Publish event, before Close.
        var events = _eventLogger.Invocations.Select(i => ((EventType)i.Arguments[1], i.Arguments[2]))
            .Where(e => e.Item1 is EventType.Publish or EventType.Close).ToList();
        events.Select(e => e.Item1).Should().Equal(EventType.Publish, EventType.Publish, EventType.Close);
        events.Take(2).Select(e => string.Join(",", ((PublishEvent)e.Item2!).Exposures!.Select(x => x.Id)))
            .Should().Equal("11,2", "12");
    }

    // A losing candidate's logger closes the context and returns normally: the read still returns
    // the winner's value, but the winner is not queued on the sealed context.
    [Test]
    public void ALosingLoggerThatClosesAndReturnsLeavesNoWinnerExposurePending()
    {
        var first = CoveredBy(NewExperiment(1, "exp_var_held_out"), 11);
        first.Variants[1].Config = "{\"closed_key\":\"a\"}";
        var winner = NewExperiment(2, "exp_var_uncovered_winner");
        winner.Variants[1].Config = "{\"closed_key\":\"b\"}";
        var context = CreateContext(DataOf(new[] { NewHoldout(11, "holdout_a", HoldoutASeedHi, HoldoutASeedLo) },
            first, winner));
        _eventLogger.Setup(x => x.HandleEvent(It.IsAny<IContext>(), EventType.Exposure,
                It.Is<object>(o => o is Exposure && ((Exposure)o).Id == 11)))
            .Callback(() => context.Close());

        context.GetVariableValue("closed_key", "default").Should().Be("b");

        context.IsClosed().Should().BeTrue();
        context.PendingCount.Should().Be(0);
        _published.Should().ContainSingle().Which.Event.Exposures.Should().Equal(HoldoutExposure(11, "holdout_a", 0));
    }

    // A losing candidate's logger disposes the context and throws: that same exception propagates,
    // without the scan touching the released locks or queuing the winner.
    [Test]
    public void ALosingLoggerThatDisposesAndThrowsPropagatesItsOwnException()
    {
        var first = CoveredBy(NewExperiment(1, "exp_var_held_out"), 11);
        first.Variants[1].Config = "{\"disposed_key\":\"a\"}";
        var winner = CoveredBy(NewExperiment(2, "exp_var_winner"), 12);
        winner.Variants[1].Config = "{\"disposed_key\":\"b\"}";
        var context = CreateContext(DataOf(new[]
        {
            NewHoldout(11, "holdout_a", HoldoutASeedHi, HoldoutASeedLo),
            NewHoldout(12, "holdout_b", HoldoutBSeedHi, HoldoutBSeedLo)
        }, first, winner));
        var sentinel = new InvalidOperationException("sentinel");
        _eventLogger.Setup(x => x.HandleEvent(It.IsAny<IContext>(), EventType.Exposure,
                It.Is<object>(o => o is Exposure && ((Exposure)o).Id == 11)))
            .Callback(() =>
            {
                context.Dispose();
                throw sentinel;
            });

        var act = () => context.GetVariableValue("disposed_key", "default");

        act.Should().Throw<InvalidOperationException>().Which.Should().BeSameAs(sentinel);
        context.IsClosed().Should().BeTrue();
        context.PendingCount.Should().Be(0);
    }

    // The same disposal without a throw: the read returns the default instead of a lock error.
    [Test]
    public void ALosingLoggerThatDisposesAndReturnsGivesTheDefault()
    {
        var first = CoveredBy(NewExperiment(1, "exp_var_held_out"), 11);
        first.Variants[1].Config = "{\"disposed_ok_key\":\"a\"}";
        var winner = NewExperiment(2, "exp_var_uncovered_winner");
        winner.Variants[1].Config = "{\"disposed_ok_key\":\"b\"}";
        var context = CreateContext(DataOf(new[] { NewHoldout(11, "holdout_a", HoldoutASeedHi, HoldoutASeedLo) },
            first, winner));
        _eventLogger.Setup(x => x.HandleEvent(It.IsAny<IContext>(), EventType.Exposure,
                It.Is<object>(o => o is Exposure && ((Exposure)o).Id == 11)))
            .Callback(() => context.Dispose());

        context.GetVariableValue("disposed_ok_key", "default").Should().Be("default");

        context.IsClosed().Should().BeTrue();
        context.PendingCount.Should().Be(0);
    }

    private const string NoUnitSplit = "{\"rules\":[{\"name\":\"split\",\"type\":\"split\",\"percentages\":\"0/100\"," +
                                       "\"conditions\":null,\"environments\":[]}]}";

    private static Experiment SplitRuled(string rules = NoUnitSplit, int iteration = 1, params int[] holdoutIds)
    {
        var experiment = NewExperiment(1, "exp_pinned_split");
        experiment.AssignmentRules = rules;
        experiment.Iteration = iteration;
        if (holdoutIds.Length > 0) experiment.HoldoutIds = holdoutIds;
        return experiment;
    }

    // An exposed split rule resolved without its unit keeps its variant when a later refresh only
    // adds holdout coverage (the unit having been set meanwhile), as the SetUnit path; the new
    // holdout is exposed. Only the exposed decision is pinned, and only for the same experiment run
    // and rules: an unexposed one, a rule change or a new iteration is resolved with the unit.
    [TestCase(true, false, false, 0)]
    [TestCase(false, false, false, 1)]
    [TestCase(true, true, false, 1)]
    [TestCase(true, false, true, 1)]
    public void ANoUnitSplitIsPinnedAcrossACoverageRefreshOnlyWhileExposedForTheSameRunAndRules(bool exposed,
        bool rulesChanged, bool newIteration, int variantAfter)
    {
        var holdouts = new[] { NewHoldout(11, "holdout_b", HoldoutBSeedHi, HoldoutBSeedLo) };
        var context = CreateContextWithoutUnits(DataOf(holdouts, SplitRuled()));
        if (exposed) context.GetTreatment("exp_pinned_split").Should().Be(0);
        else context.PeekTreatment("exp_pinned_split").Should().Be(0);
        context.SetUnit(UnitType, Uid);

        var rules = rulesChanged ? NoUnitSplit.Replace("\"split\",\"type\"", "\"split_v2\",\"type\"") : NoUnitSplit;
        Refresh(context, DataOf(holdouts, SplitRuled(rules, newIteration ? 2 : 1, 11)));

        context.GetTreatment("exp_pinned_split").Should().Be(variantAfter);
        var exposures = Publish(context).Exposures!.Select(e => (e.Id, e.Variant)).ToList();
        exposures.Should().Contain((11, 1));
        exposures.Should().Contain((1, variantAfter));
        if (variantAfter == 0) exposures.Should().NotContain((1, 1));
    }

    // A split exposed with its unit is not pinned to the no-unit result by a coverage refresh.
    [Test]
    public void ASplitExposedWithItsUnitKeepsItsHashedVariantAcrossACoverageRefresh()
    {
        var holdouts = new[] { NewHoldout(11, "holdout_b", HoldoutBSeedHi, HoldoutBSeedLo) };
        var context = CreateContext(DataOf(holdouts, SplitRuled()));
        context.GetTreatment("exp_pinned_split").Should().Be(1);

        Refresh(context, DataOf(holdouts, SplitRuled(NoUnitSplit, 1, 11)));

        context.GetTreatment("exp_pinned_split").Should().Be(1);
    }

    // Not every winner has holdouts: one no holdout covers is not queued on a closed context either.
    [Test]
    public void ALoggerThatClosesTheContextLeavesNoDeferredUncoveredWinnerExposure()
    {
        var first = CoveredBy(NewExperiment(1, "exp_var_held_out"), 11);
        first.Variants[1].Config = "{\"uncovered_key\":\"a\"}";
        var winner = NewExperiment(2, "exp_var_uncovered_winner");
        winner.Variants[1].Config = "{\"uncovered_key\":\"b\"}";
        var context = CreateContext(DataOf(new[] { NewHoldout(11, "holdout_a", HoldoutASeedHi, HoldoutASeedLo) },
            first, winner));
        _eventLogger.Setup(x => x.HandleEvent(It.IsAny<IContext>(), EventType.Exposure,
                It.Is<object>(o => o is Exposure && ((Exposure)o).Id == 11)))
            .Callback(() =>
            {
                context.Close();
                throw new InvalidOperationException("boom");
            });

        var act = () => context.GetVariableValue("uncovered_key", "default");

        act.Should().Throw<InvalidOperationException>().WithMessage("boom");
        context.IsClosed().Should().BeTrue();
        context.PendingCount.Should().Be(0);
    }

    // A deferred winner skipped because the context was closing is not lost when that close fails:
    // the context stays open, and the next read exposes the winner and its holdout.
    [Test]
    public void ADeferredWinnerSkippedByAFailedCloseIsExposedByTheNextRead()
    {
        var first = CoveredBy(NewExperiment(1, "exp_var_held_out"), 11);
        first.Variants[1].Config = "{\"retry_key\":\"a\"}";
        var winner = CoveredBy(NewExperiment(2, "exp_var_winner"), 12);
        winner.Variants[1].Config = "{\"retry_key\":\"b\"}";
        var context = CreateContext(DataOf(new[]
        {
            NewHoldout(11, "holdout_a", HoldoutASeedHi, HoldoutASeedLo),
            NewHoldout(12, "holdout_b", HoldoutBSeedHi, HoldoutBSeedLo)
        }, first, winner));
        var publish = new TaskCompletionSource<bool>();
        var publishes = 0;
        Mock.Get(_publisher).Setup(x => x.PublishAsync(It.IsAny<IContext>(), It.IsAny<PublishEvent>()))
            .Callback<IContext, PublishEvent>((c, e) => _published.Add((c, e)))
            .Returns(() => Interlocked.Increment(ref publishes) == 1 ? publish.Task : Task.CompletedTask);
        Task finalizing = null!;
        _eventLogger.Setup(x => x.HandleEvent(It.IsAny<IContext>(), EventType.Exposure,
                It.Is<object>(o => o is Exposure && ((Exposure)o).Id == 11)))
            .Callback(() =>
            {
                finalizing = context.FinalizeAsync();
                throw new InvalidOperationException("boom");
            });

        var act = () => context.GetVariableValue("retry_key", "default");
        act.Should().Throw<InvalidOperationException>().WithMessage("boom");
        publish.SetException(new HttpRequestException("collector down"));
        var settle = () => finalizing.Wait(TimeSpan.FromSeconds(10));
        settle.Should().Throw<AggregateException>();
        context.IsClosed().Should().BeFalse();
        _eventLogger.Reset();
        _published.Clear();

        context.GetVariableValue("retry_key", "default").Should().Be("b");

        AssertPublished(context, Uid, HoldoutExposure(11, "holdout_a", 0), Exposure(2, "exp_var_winner", NormalVariant),
            HoldoutExposure(12, "holdout_b", 1));
    }

    // A losing candidate's holdout logger that closes the context before throwing: the close has
    // flushed, so the later winner is not queued, and the logger's exception still propagates.
    [Test]
    public void ALoggerThatClosesTheContextLeavesNoDeferredWinnerExposure()
    {
        var first = CoveredBy(NewExperiment(1, "exp_var_held_out"), 11);
        first.Variants[1].Config = "{\"close_key\":\"a\"}";
        var winner = CoveredBy(NewExperiment(2, "exp_var_winner"), 12);
        winner.Variants[1].Config = "{\"close_key\":\"b\"}";
        var context = CreateContext(DataOf(new[]
        {
            NewHoldout(11, "holdout_a", HoldoutASeedHi, HoldoutASeedLo),
            NewHoldout(12, "holdout_b", HoldoutBSeedHi, HoldoutBSeedLo)
        }, first, winner));
        _eventLogger.Setup(x => x.HandleEvent(It.IsAny<IContext>(), EventType.Exposure,
                It.Is<object>(o => o is Exposure && ((Exposure)o).Id == 11)))
            .Callback(() =>
            {
                context.Close();
                throw new InvalidOperationException("boom");
            });

        var act = () => context.GetVariableValue("close_key", "default");

        act.Should().Throw<InvalidOperationException>().WithMessage("boom");
        context.IsClosed().Should().BeTrue();
        context.PendingCount.Should().Be(0);
        _published.Should().ContainSingle().Which.Event.Exposures.Should().Equal(HoldoutExposure(11, "holdout_a", 0));
    }

    // A refresh that removes, adds or swaps a holdout changes the coverage, so the unit is
    // re-resolved against the new set: 12 holds Uid out, 11 and 13 do not.
    [TestCase(new[] { 11, 12 }, new[] { 11 }, 0, NormalVariant)]
    [TestCase(new[] { 11 }, new[] { 11, 12 }, NormalVariant, 0)]
    [TestCase(new[] { 11 }, new[] { 12 }, NormalVariant, 0)]
    [TestCase(new[] { 12 }, new[] { 11 }, 0, NormalVariant)]
    [TestCase(new[] { 11, 12 }, new[] { 11, 13 }, 0, NormalVariant)]
    [TestCase(new[] { 11, 13 }, new[] { 11, 12 }, NormalVariant, 0)]
    [TestCase(new[] { 12 }, new int[0], 0, NormalVariant)]
    [TestCase(new int[0], new[] { 12 }, NormalVariant, 0)]
    [TestCase(new[] { 12, 13 }, new[] { 11, 13 }, 0, NormalVariant)]
    [TestCase(new[] { 11, 13 }, new[] { 12, 13 }, NormalVariant, 0)]
    public void RefreshChangingTheHoldoutSetReResolvesCoverage(int[] before, int[] after, int variantBefore,
        int variantAfter)
    {
        var holdouts = new[]
        {
            NewHoldout(11, "holdout_b", HoldoutBSeedHi, HoldoutBSeedLo),
            NewHoldout(12, "holdout_a", HoldoutASeedHi, HoldoutASeedLo),
            NewHoldout(13, "holdout_c", HoldoutBSeedHi, HoldoutBSeedLo)
        };
        var context = CreateContext(DataOf(holdouts, CoveredBy(NewExperiment(1, "exp_resized"), before)));
        context.PeekTreatment("exp_resized").Should().Be(variantBefore);

        Refresh(context, DataOf(holdouts, CoveredBy(NewExperiment(1, "exp_resized"), after)));

        context.PeekTreatment("exp_resized").Should().Be(variantAfter);
    }

    // A new iteration of the second of two holdouts re-resolves coverage: its new seeds hold Uid out.
    [Test]
    public void RefreshBumpingTheSecondHoldoutsIterationReResolvesCoverage()
    {
        var context = CreateContext(DataOf(new[]
        {
            NewHoldout(11, "holdout_b", HoldoutBSeedHi, HoldoutBSeedLo),
            NewHoldout(13, "holdout_c", HoldoutBSeedHi, HoldoutBSeedLo)
        }, CoveredBy(NewExperiment(1, "exp_second_bumped"), 11, 13)));
        context.PeekTreatment("exp_second_bumped").Should().Be(NormalVariant);
        var bumped = NewHoldout(13, "holdout_c", HoldoutASeedHi, HoldoutASeedLo);
        bumped.Iteration = 2;

        Refresh(context, DataOf(new[] { NewHoldout(11, "holdout_b", HoldoutBSeedHi, HoldoutBSeedLo), bumped },
            CoveredBy(NewExperiment(1, "exp_second_bumped"), 11, 13)));

        context.PeekTreatment("exp_second_bumped").Should().Be(0);
    }

    [TestCase(new[] { 11, 1, 12, 1 }, new[] { 11, 1, 12, 1 }, true)]
    [TestCase(new[] { 11, 1, 12, 1 }, new[] { 13, 1, 12, 1 }, false)]
    [TestCase(new[] { 11, 1, 12, 1 }, new[] { 11, 1, 13, 1 }, false)]
    [TestCase(new[] { 11, 1, 12, 1 }, new[] { 11, 2, 12, 1 }, false)]
    [TestCase(new[] { 11, 1, 12, 1 }, new[] { 11, 1, 12, 2 }, false)]
    [TestCase(new[] { 11, 1, 12, 1, 13, 1 }, new[] { 11, 1, 14, 1, 13, 1 }, false)]
    [TestCase(new[] { 11, 1, 12, 1, 13, 1 }, new[] { 11, 1, 12, 2, 13, 1 }, false)]
    [TestCase(new[] { 11, 1, 12, 1 }, new[] { 11, 1 }, false)]
    [TestCase(new[] { 11, 1 }, new[] { 11, 1, 12, 1 }, false)]
    [TestCase(new[] { 11, 1 }, null, false)]
    [TestCase(null, new[] { 11, 1 }, false)]
    [TestCase(null, null, true)]
    public void HoldoutSetMatchesComparesEveryIdAndIteration(int[]? current, int[]? pinned, bool matches)
    {
        static Experiment[]? Set(int[]? pairs) => pairs?.Chunk(2)
            .Select(p => new Experiment { Id = p[0], Iteration = p[1], Name = "renamed_" + p[0], SeedHi = p[0] })
            .ToArray();

        var match = typeof(Context).GetMethod("HoldoutSetMatches", BindingFlags.Static | BindingFlags.NonPublic);
        match.Should().NotBeNull();

        ((bool)match!.Invoke(null, new object?[] { Set(current), Set(pinned) })!).Should().Be(matches);
    }

    // A stale holdout definition (here an older iteration under another name) resolves to the
    // live one: its iteration, arm and name.
    [Test]
    public void StaleHoldoutDefinitionResolvesToTheLiveHoldout()
    {
        var live = NewHoldout(11, "holdout_live", HoldoutBSeedHi, HoldoutBSeedLo);
        live.Iteration = 2;
        var context = CreateContext(DataOf(new[] { live }, CoveredBy(NewExperiment(1, "exp_live"), 11)));

        var stale = NewHoldout(11, "holdout_stale", HoldoutASeedHi, HoldoutASeedLo);
        var resolve = typeof(Context).GetMethod("GetHoldoutAssignment", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var resolved = (Context.Assignment)resolve.Invoke(context, new object[] { stale, UnitType, InstalledHoldouts(context) })!;

        (resolved.Name, resolved.Iteration, resolved.Variant).Should().Be(("holdout_live", 2, 1));
    }

    // As JS, a same-iteration rename keeps the unit's arm, and an experiment resolved after the
    // refresh publishes the holdout's live name.
    [Test]
    public void SameIterationRenamePublishesTheLiveHoldoutName()
    {
        var context = CreateContext(UidNotHeldOut,
            DataOf(new[] { NewHoldout(11, "holdout_old_name", HoldoutASeedHi, HoldoutASeedLo) },
                CoveredBy(NewExperiment(1, "exp_rename_a"), 11), CoveredBy(NewExperiment(2, "exp_rename_b"), 11)));
        context.PeekTreatment("exp_rename_a").Should().Be(0);

        Refresh(context, DataOf(new[] { NewHoldout(11, "holdout_new_name", HoldoutASeedHi, HoldoutASeedLo) },
            CoveredBy(NewExperiment(1, "exp_rename_a"), 11), CoveredBy(NewExperiment(2, "exp_rename_b"), 11)));
        context.GetTreatment("exp_rename_b").Should().Be(0);

        AssertPublished(context, UidNotHeldOut, Exposure(2, "exp_rename_b", 0),
            HoldoutExposure(11, "holdout_new_name", 1));
    }

    // As JS, an assignment publishes the name of the holdout snapshot it pinned: one resolved
    // before a same-iteration rename keeps the old name, even after another experiment's read
    // (here a peek) resolves the renamed holdout.
    [Test]
    public void AssignmentPinnedBeforeARenamePublishesItsPinnedHoldoutName()
    {
        var context = CreateContext(UidNotHeldOut,
            DataOf(new[] { NewHoldout(11, "holdout_old_name", HoldoutASeedHi, HoldoutASeedLo) },
                CoveredBy(NewExperiment(1, "exp_pinned_a"), 11), CoveredBy(NewExperiment(2, "exp_pinned_b"), 11)));
        context.PeekTreatment("exp_pinned_a").Should().Be(0);

        Refresh(context, DataOf(new[] { NewHoldout(11, "holdout_new_name", HoldoutASeedHi, HoldoutASeedLo) },
            CoveredBy(NewExperiment(1, "exp_pinned_a"), 11), CoveredBy(NewExperiment(2, "exp_pinned_b"), 11)));
        context.PeekTreatment("exp_pinned_b").Should().Be(0);
        context.GetTreatment("exp_pinned_a").Should().Be(0);

        AssertPublished(context, UidNotHeldOut, Exposure(1, "exp_pinned_a", 0),
            HoldoutExposure(11, "holdout_old_name", 1));
    }

    // The pinned holdout's name is published as is, even null (as JS), not a cached name.
    [Test]
    public void SameIterationRenameToNullPublishesNull()
    {
        var renamed = NewHoldout(11, "holdout_old_name", HoldoutASeedHi, HoldoutASeedLo);
        var context = CreateContext(UidNotHeldOut,
            DataOf(new[] { renamed }, CoveredBy(NewExperiment(1, "exp_null_a"), 11),
                CoveredBy(NewExperiment(2, "exp_null_b"), 11)));
        context.PeekTreatment("exp_null_b").Should().Be(0);

        var nameless = NewHoldout(11, null!, HoldoutASeedHi, HoldoutASeedLo);
        Refresh(context, DataOf(new[] { nameless }, CoveredBy(NewExperiment(1, "exp_null_a"), 11),
            CoveredBy(NewExperiment(2, "exp_null_b"), 11)));
        context.GetTreatment("exp_null_a").Should().Be(0);

        AssertPublished(context, UidNotHeldOut, Exposure(1, "exp_null_a", 0), HoldoutExposure(11, null!, 1));
    }

    #endregion

    #region Assignment rules (javascript-sdk; cross-sdk scenario 305)

    [Test]
    public void MatchingAssignmentRuleWinsOverHoldoutSuppression()
    {
        var experiment = CoveredBy(NewExperiment(1, "exp_holdout_rules"), 11);
        experiment.AssignmentRules = UsRule;
        var context = CreateContext(DataOf(
            new[] { NewHoldout(11, "holdout_rules_suppression", HoldoutASeedHi, HoldoutASeedLo) }, experiment));
        context.SetAttribute("country", "US");

        context.GetTreatment("exp_holdout_rules").Should().Be(1);

        AssertPublished(context, Uid, Exposure(1, "exp_holdout_rules", 1, assigned: false, ruleOverride: true),
                HoldoutExposure(11, "holdout_rules_suppression", 0))
            .Attributes.Should().Equal(new Attribute { Name = "country", Value = "US", SetAt = Now });
    }

    [Test]
    public void MatchingSplitRuleWinsOverHoldoutSuppression()
    {
        var experiment = CoveredBy(NewExperiment(1, "exp_holdout_split_rule"), 11);
        experiment.AssignmentRules = "{\"rules\":[{\"name\":\"Everyone\",\"type\":\"split\",\"conditions\":null," +
                                     "\"environments\":[],\"percentages\":\"0/100\"}]}";
        var context = CreateContext(DataOf(new[] { NewHoldout(11, "holdout_a", HoldoutASeedHi, HoldoutASeedLo) },
            experiment));

        context.PeekTreatment("exp_holdout_split_rule").Should().Be(1);
        context.GetTreatment("exp_holdout_split_rule").Should().Be(1);
        context.PendingCount.Should().Be(2);
    }

    [Test]
    public void HeldOutUnitIsSuppressedWhenRuleDoesNotMatch()
    {
        var experiment = CoveredBy(NewExperiment(1, "exp_holdout_rules"), 11);
        experiment.AssignmentRules = UsRule;
        var context = CreateContext(DataOf(new[] { NewHoldout(11, "holdout_a", HoldoutASeedHi, HoldoutASeedLo) },
            experiment));
        context.SetAttribute("country", "GB");

        context.GetTreatment("exp_holdout_rules").Should().Be(0);
        context.PendingCount.Should().Be(1);

        context.SetAttribute("country", "US");
        context.GetTreatment("exp_holdout_rules").Should().Be(1);
        context.PendingCount.Should().Be(2);
    }

    [Test]
    public void HeldOutUnitIsSuppressedAgainWhenRuleStopsMatching()
    {
        var experiment = CoveredBy(NewExperiment(1, "exp_holdout_rules"), 11);
        experiment.AssignmentRules = UsRule;
        var context = CreateContext(DataOf(new[] { NewHoldout(11, "holdout_a", HoldoutASeedHi, HoldoutASeedLo) },
            experiment));
        context.SetAttribute("country", "US");

        context.GetTreatment("exp_holdout_rules").Should().Be(1);
        context.PendingCount.Should().Be(2);

        context.SetAttribute("country", "GB");
        context.GetTreatment("exp_holdout_rules").Should().Be(0);
        context.PeekTreatment("exp_holdout_rules").Should().Be(0);
        context.PendingCount.Should().Be(2);
    }

    // As JS, the experiment's own exposure precedes its holdouts' (Java publishes the holdout
    // first on the variable path).
    [Test]
    public void VariableValueOnHeldOutRuleWonExperiment()
    {
        var experiment = WithVariables(CoveredBy(NewExperiment(1, "exp_holdout_rules"), 11), "color");
        experiment.AssignmentRules = UsRule;
        var context = CreateContext(DataOf(
            new[] { NewHoldout(11, "holdout_rules_suppression", HoldoutASeedHi, HoldoutASeedLo) }, experiment));
        context.SetAttribute("country", "US");

        context.GetVariableValue("color", "default").Should().Be("treatment");

        AssertPublished(context, Uid, Exposure(1, "exp_holdout_rules", 1, assigned: false, ruleOverride: true),
            HoldoutExposure(11, "holdout_rules_suppression", 0));
    }

    [Test]
    public void SharedVariableKeyPrefersRuleWonOverSuppressedExperiment()
    {
        var heldOut = WithVariables(CoveredBy(NewExperiment(1, "exp_held_out"), 11), "color");
        var ruleWon = WithVariables(CoveredBy(NewExperiment(2, "exp_rule_won"), 11), "color");
        ruleWon.AssignmentRules = UsRule;
        var context = CreateContext(DataOf(new[] { NewHoldout(11, "holdout_a", HoldoutASeedHi, HoldoutASeedLo) },
            heldOut, ruleWon));

        context.SetAttribute("country", "GB");
        context.PeekVariableValue("color", "default").Should().Be("control");

        context.SetAttribute("country", "US");
        context.PeekVariableValue("color", "default").Should().Be("treatment");
    }

    [Test]
    public void SharedVariableKeyPrefersEarlierAssignedOverLaterRuleWonExperiment()
    {
        var assigned = WithVariables(NewExperiment(1, "exp_assigned"), "color");
        assigned.Split = new[] { 1.0, 0.0 };
        var ruleWon = WithVariables(NewExperiment(2, "exp_rule_won"), "color");
        ruleWon.AssignmentRules = UsRule;
        var context = CreateContext(DataOf(Array.Empty<Experiment>(), assigned, ruleWon));
        context.SetAttribute("country", "US");

        context.PeekVariableValue("color", "default").Should().Be("control");
        context.PeekTreatment("exp_rule_won").Should().Be(1);
    }

    #endregion
}
