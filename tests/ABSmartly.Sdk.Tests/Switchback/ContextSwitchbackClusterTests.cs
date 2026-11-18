using System.Text;
using ABSmartly.Internal;
using ABSmartly.Models;
using ABSmartly.Services;

namespace ABSmartly.Sdk.Tests.Switchback;

// Ported from java-sdk ContextSwitchbackClusterTest: cluster key selection inside Context. Variants
// are derived from the raw key and SwitchbackAssigner, both pinned by the platform golden vectors.
// These contexts set no cluster attribute at creation; each test sets its own.
[TestFixture]
public class ContextSwitchbackClusterTests : SwitchbackTestBase
{
    // 3 arms over 12 periods: two keys in different buckets are told apart by their schedules.
    private const int Arms = 3;

    private Context Create(ContextData data) => CreateContext(new Dictionary<string, string> { [UnitType] = Uid }, data,
        clusterAttribute: false);

    private Context Create(Dictionary<string, string> units, ContextData data) =>
        CreateContext(units, data, clusterAttribute: false);

    private static Experiment AttributeSwitchback(string? path)
    {
        var experiment = NewSwitchback(Arms, Hour);
        experiment.SwitchbackClusters = Clusters(UnitType, path!);
        return experiment;
    }

    private static int Expected(string key, long periodIndex) => SwitchbackAssigner.Assign(key, periodIndex, Seed, Arms);

    private static int[] ExpectedSchedule(string key) => Enumerable.Range(0, 12).Select(i => Expected(key, i)).ToArray();

    private int[] PeekSchedule(Context context) => PeekSchedule(context, "exp_sb", Hour, 12);

    private static Exposure ClusterExposure(int variant, long exposedAt, bool eligible, bool audienceMismatch) =>
        NewExposure(1, "exp_sb", variant, exposedAt, true, eligible, audienceMismatch: audienceMismatch);

    // The unit uid is never the cluster key: with no cluster value the unit is ineligible.
    [Test]
    public void UnitUidIsNeverTheClusterKey()
    {
        var context = Create(DataOf(AttributeSwitchback("city")));
        for (var i = 0; i < 3; ++i)
        {
            Clock.Now = Anchor + i * Hour;
            context.GetTreatment("exp_sb").Should().Be(0);
        }

        context.PendingCount.Should().Be(0);
    }

    // Independent reference (Python MD5/base64url) for the published hash of the uid "123456789",
    // and the raw-key schedule of 123456789 as a numeric attribute value (murmur3 reference). The
    // .NET SDK's uids are strings, so java-sdk's numeric-uid cases reduce to this one.
    [Test]
    public void NumericAttributeValueMatchesTheIndependentReference()
    {
        int[] schedule = { 0, 2, 1, 0, 1, 2, 0, 1, 2, 2, 1, 0 };
        foreach (var value in new object[] { 123456789, 123456789L, 123456789u })
        {
            var context = Create(new Dictionary<string, string> { [UnitType] = "123456789" },
                DataOf(AttributeSwitchback("region")));
            Clock.Now = Anchor;
            context.SetAttribute("region", value);
            context.GetTreatment("exp_sb");
            var published = PublishEvent(context);

            published.Units.Single().Uid.Should().Be("JfnnlDI7RTiF9RgfG2JNCw");
            published.Exposures.Should().Equal(ClusterExposure(schedule[0], Anchor, true, false));
            PeekSchedule(context).Should().Equal(schedule);
            ExpectedSchedule("123456789").Should().Equal(schedule);
        }
    }

    // Periods 0..11 of the raw key "Lisboa", from the independent murmur3 reference.
    [Test]
    public void AttributeSourceServesTheRawAttributeValue()
    {
        Clock.Now = Anchor;
        var context = Create(DataOf(AttributeSwitchback("city")));
        context.SetAttribute("city", "Lisboa");
        PeekSchedule(context).Should().Equal(0, 2, 1, 2, 1, 0, 1, 0, 2, 0, 1, 2);
        PeekSchedule(context).Should().Equal(ExpectedSchedule("Lisboa"));
    }

    [Test]
    public void UnitsSharingAnAttributeValueShareTheSchedule()
    {
        var lisboa = Create(DataOf(AttributeSwitchback("city")));
        lisboa.SetAttribute("city", "Lisboa");
        var porto = Create(DataOf(AttributeSwitchback("city")));
        porto.SetAttribute("city", "Porto");
        var otherLisboa = Create(new Dictionary<string, string> { [UnitType] = "another-store" },
            DataOf(AttributeSwitchback("city")));
        otherLisboa.SetAttribute("city", "Lisboa");

        PeekSchedule(lisboa).Should().Equal(ExpectedSchedule("Lisboa"));
        PeekSchedule(otherLisboa).Should().Equal(ExpectedSchedule("Lisboa"));
        PeekSchedule(porto).Should().Equal(ExpectedSchedule("Porto"));
        ExpectedSchedule("Lisboa").Should().NotEqual(ExpectedSchedule("Porto"));
    }

    [Test]
    public void NestedAndArrayPathsAndIntegerValuesResolve()
    {
        Clock.Now = Anchor;
        var nested = Create(DataOf(AttributeSwitchback("geo/city")));
        nested.SetAttribute("geo", new Dictionary<string, object> { ["city"] = "Porto" });
        PeekSchedule(nested).Should().Equal(ExpectedSchedule("Porto"));

        Clock.Now = Anchor;
        var array = Create(DataOf(AttributeSwitchback("items/1/city")));
        array.SetAttribute("items", new List<object>
        {
            new Dictionary<string, object> { ["city"] = "Lisbon" }, new Dictionary<string, object> { ["city"] = "Faro" }
        });
        PeekSchedule(array).Should().Equal(ExpectedSchedule("Faro"));

        Clock.Now = Anchor;
        var integer = Create(DataOf(AttributeSwitchback("region")));
        integer.SetAttribute("region", 42L);
        PeekSchedule(integer).Should().Equal(ExpectedSchedule("42"));

        // Values are read through their JSON encoding, as the Collector decodes them: a whole decimal
        // nested in an object becomes an integer, and a char a string.
        Clock.Now = Anchor;
        var dec = Create(DataOf(AttributeSwitchback("geo/city")));
        dec.SetAttribute("geo", new Dictionary<string, object> { ["city"] = 42m });
        PeekSchedule(dec).Should().Equal(ExpectedSchedule("42"));

        Clock.Now = Anchor;
        var character = Create(DataOf(AttributeSwitchback("city")));
        character.SetAttribute("city", 'c');
        PeekSchedule(character).Should().Equal(ExpectedSchedule("c"));

        // A System.Text.Json element (what a JSON-decoding caller passes) reads as its JSON.
        Clock.Now = Anchor;
        var element = Create(DataOf(AttributeSwitchback("geo/city")));
        element.SetAttribute("geo", System.Text.Json.JsonDocument.Parse("{\"city\":\"Porto\"}").RootElement);
        PeekSchedule(element).Should().Equal(ExpectedSchedule("Porto"));

        Clock.Now = Anchor + 10;
        var floating = Create(DataOf(AttributeSwitchback("city")));
        floating.SetAttribute("city", 2.0f);
        floating.GetTreatment("exp_sb").Should().Be(0);
        floating.PendingCount.Should().Be(0);
    }

    // Attribute values that yield no cluster key serve variant 0, not eligible, record no exposure
    // and never throw. Whole floats (3.0) included, wherever they sit.
    [Test]
    public void ValuesWithoutAClusterKeyAreIneligible()
    {
        object?[] values =
        {
            // A decimal is read through its JSON text, as java-sdk reads a BigDecimal: 3.0m keeps its
            // scale and is a float (3m is the integer 3, as BigDecimal("3")).
            null, "", true, 1.5, 2.0, 3.0f, 3.0m, new Dictionary<string, object> { ["a"] = "b" }, new List<object> { "x" },
            System.Text.Json.JsonDocument.Parse("3.0").RootElement
        };
        foreach (var value in values)
        {
            var context = Create(DataOf(AttributeSwitchback("city")));
            context.SetAttribute("city", value!);
            Clock.Now = Anchor + 10;
            context.GetTreatment("exp_sb").Should().Be(0, "{0}", value);
            context.GetVariableValue("sb.arm", -1).Should().Be(0, "{0}", value);
            context.PendingCount.Should().Be(0, "{0}", value);
        }

        foreach (var nested in new object[] { 3.0, 3.0f, 1.5, true })
        {
            var context = Create(DataOf(AttributeSwitchback("geo/city")));
            context.SetAttribute("geo", new Dictionary<string, object> { ["city"] = nested });
            Clock.Now = Anchor + 10;
            context.GetTreatment("exp_sb").Should().Be(0, "nested {0}", nested);
            context.PendingCount.Should().Be(0);
        }

        var missing = Create(DataOf(AttributeSwitchback("geo/city")));
        missing.SetAttribute("geo", new Dictionary<string, object> { ["country"] = "PT" });
        Clock.Now = Anchor + 10;
        missing.GetTreatment("exp_sb").Should().Be(0);
        missing.PendingCount.Should().Be(0);
    }

    // A value with no JSON encoding has no cluster key.
    [Test]
    public void UnencodableAttributeValuesAreIneligible()
    {
        var cyclic = new Dictionary<string, object>();
        cyclic["self"] = cyclic;
        foreach (var value in new object[] { cyclic, typeof(string) })
        {
            var context = Create(DataOf(AttributeSwitchback("geo/city")));
            context.SetAttribute("geo", value);
            context.GetTreatment("exp_sb").Should().Be(0);
            context.PendingCount.Should().Be(0);
        }
    }

    // Unservable configurations serve an ineligible control to linked units, never a unit-assignment
    // draw, and record nothing.
    [Test]
    public void UnservableConfigurationsAreIneligible()
    {
        var experiments = new List<Experiment>();
        foreach (var path in new[] { "items/0", "items/-1/city", "", "geo.city", "'city", "\"city" })
            experiments.Add(AttributeSwitchback(path));
        var noPeriod = AttributeSwitchback("city");
        noPeriod.SwitchbackPeriodMs = null;
        experiments.Add(noPeriod);
        var zeroPeriod = AttributeSwitchback("city");
        zeroPeriod.SwitchbackPeriodMs = 0L;
        experiments.Add(zeroPeriod);
        var negativePeriod = AttributeSwitchback("city");
        negativePeriod.SwitchbackPeriodMs = -Hour;
        experiments.Add(negativePeriod);
        var oneArm = AttributeSwitchback("city");
        oneArm.Split = new[] { 1.0 };
        experiments.Add(oneArm);
        experiments.Add(AttributeSwitchback(null));
        foreach (var experiment in experiments)
        {
            var context = Create(DataOf(experiment));
            context.SetAttribute("city", "Lisboa");
            context.SetAttribute("items", new List<object> { new Dictionary<string, object> { ["city"] = "Lisbon" } });
            for (var i = 0; i < 3; ++i)
            {
                Clock.Now = Anchor + i * Hour;
                context.GetTreatment("exp_sb").Should().Be(0, experiment.ToString());
            }

            context.PendingCount.Should().Be(0, experiment.ToString());
        }
    }

    // A missing or empty cluster map takes the Collector's legacy path: a unit of the declared
    // unitType only identifies an ineligible control; without one the experiment does not apply.
    [Test]
    public void MissingOrEmptyClusterMapIsOnlyALegacyIneligibleControl()
    {
        var none = AttributeSwitchback("city");
        none.SwitchbackClusters = null;
        var empty = AttributeSwitchback("city");
        empty.SwitchbackClusters = Array.Empty<KeyValuePair<string, string>>();
        foreach (var experiment in new[] { none, empty })
        {
            var context = Create(DataOf(experiment));
            context.SetAttribute("city", "Lisboa");
            context.GetTreatment("exp_sb").Should().Be(0);
            context.GetVariableValue("sb.arm", -1).Should().Be(0);
            context.PendingCount.Should().Be(0);

            var other = Create(new Dictionary<string, string> { ["user_id"] = "u1" }, DataOf(experiment));
            other.SetAttribute("city", "Lisboa");
            other.GetTreatment("exp_sb").Should().Be(0);
            other.GetVariableValue("sb.arm", -1).Should().Be(-1);
            other.PendingCount.Should().Be(0);
        }
    }

    // The Collector reads the attribute set at or before the exposure instant, latest setAt first.
    [Test]
    public void LatestAttributeSetAtOrBeforeTheResolutionInstantIsUsed()
    {
        var context = Create(DataOf(AttributeSwitchback("city")));
        Clock.Now = Anchor + 2 * Hour;
        context.SetAttribute("city", "Porto");
        Clock.Now = Anchor + 4 * Hour;
        context.SetAttribute("city", "Lisboa");

        Clock.Now = Anchor + Hour;
        context.PeekTreatment("exp_sb").Should().Be(0);
        Clock.Now = Anchor + 3 * Hour;
        context.PeekTreatment("exp_sb").Should().Be(Expected("Porto", 3));
        Clock.Now = Anchor + 4 * Hour;
        context.PeekTreatment("exp_sb").Should().Be(Expected("Lisboa", 4));
    }

    // Among attributes with the same setAt, the one set last wins; names are matched trimmed, and
    // system attributes (application, environment) are never cluster sources.
    [Test]
    public void TiesGoToTheLastSetAndSystemAttributesAreNotRead()
    {
        // Two keys whose schedules differ, so serving the wrong one fails.
        var first = "Porto";
        var last = new[] { "Lisboa", "Faro", "Braga", "Coimbra", "Aveiro" }
            .First(k => !ExpectedSchedule(k).SequenceEqual(ExpectedSchedule(first)));

        var tie = Create(DataOf(AttributeSwitchback("city")));
        Clock.Now = Anchor;
        tie.SetAttribute("city", first);
        tie.SetAttribute("city", last);
        PeekSchedule(tie).Should().Equal(ExpectedSchedule(last));

        // Names are matched trimmed, as the Collector does.
        var trimmed = Create(DataOf(AttributeSwitchback("city")));
        Clock.Now = Anchor;
        trimmed.SetAttribute("city", first);
        trimmed.SetAttribute(" city ", last);
        PeekSchedule(trimmed).Should().Equal(ExpectedSchedule(last));

        var system = Create(DataOf(AttributeSwitchback("environment")));
        system.GetTreatment("exp_sb").Should().Be(0);
        system.PendingCount.Should().Be(0);
    }

    // Two dictionary keys that publish under the same (camelCase) name: the path reads the one
    // published last, as the Collector reads the published JSON.
    [Test]
    public void KeysCollidingUnderTheNamingPolicyReadTheLastOne()
    {
        var context = Create(DataOf(AttributeSwitchback("geo/region")));
        Clock.Now = Anchor;
        context.SetAttribute("geo", new Dictionary<string, object> { ["Region"] = "Porto", ["region"] = "Lisboa" });
        PeekSchedule(context).Should().Equal(ExpectedSchedule("Lisboa"));
        ExpectedSchedule("Lisboa").Should().NotEqual(ExpectedSchedule("Porto"));
    }

    // Review regression: a whole floating-point value is published as a float (3.0), so the
    // Collector, which re-resolves clusters from the published attributes, does not see an integer
    // key 3 there that would disagree with the served cluster and drop the exposure.
    [Test]
    public void WholeFloatAttributesArePublishedAsFloats()
    {
        var experiment = AttributeSwitchback("city");
        experiment.SwitchbackClusters = Clusters(UnitType, "city", "user_id", "profile/n");
        var context = Create(new Dictionary<string, string> { [UnitType] = Uid, ["user_id"] = "u1" }, DataOf(experiment));
        Clock.Now = Anchor + 10;
        context.SetAttribute("city", "Faro");
        context.SetAttribute("profile", new Dictionary<string, object> { ["n"] = 3.0, ["m"] = 2.5f, ["k"] = 4 });
        context.SetAttribute("plain", 3.0);
        context.GetTreatment("exp_sb").Should().Be(Expected("Faro", 0));

        var json = Encoding.UTF8.GetString(new DefaultContextEventSerializer().Serialize(PublishEvent(context)));
        json.Should().Contain("{\"name\":\"profile\",\"value\":{\"n\":3.0,\"m\":2.5,\"k\":4},\"setAt\":" + (Anchor + 10) + "}");
        json.Should().Contain("{\"name\":\"plain\",\"value\":3.0,");
        json.Should().Contain("\"unit\":\"store_id\"");
    }

    // The published event keeps the values the caller set: the wire form is applied only by the
    // default serializer, so a custom IContextEventSerializer and the Publish event logger see the
    // original objects, and a float keeps its own shortest text.
    [Test]
    public void PublishedAttributeValuesAreTheCallersObjects()
    {
        var context = Create(DataOf(AttributeSwitchback("profile/city")));
        Clock.Now = Anchor + 10;
        var profile = new Dictionary<string, object> { ["city"] = "Faro", ["score"] = 3.0 };
        context.SetAttribute("profile", profile);
        context.SetAttribute("ratio", 0.1f);
        context.SetAttribute("plain", 3.0);
        context.GetTreatment("exp_sb");
        var published = PublishEvent(context);

        published.Attributes.Select(a => a.Value).Should().Equal(profile, 0.1f, 3.0);
        published.Attributes[2].Value.Should().BeOfType<double>();
        Newtonsoft.Json.JsonConvert.SerializeObject(published.Attributes[0].Value)
            .Should().Be("{\"city\":\"Faro\",\"score\":3.0}");
        var json = Encoding.UTF8.GetString(new DefaultContextEventSerializer().Serialize(published));
        json.Should().Contain("{\"name\":\"profile\",\"value\":{\"city\":\"Faro\",\"score\":3.0},\"setAt\":" + (Anchor + 10) + "}");
        json.Should().Contain("{\"name\":\"ratio\",\"value\":0.1,\"setAt\":" + (Anchor + 10) + "}");
    }

    // The wire form starts with the first data holding a switchback, beside unit experiments too,
    // and stays after a refresh removes the switchbacks: an exposure they queued can still be pending,
    // and the Collector must replay its cluster from the same encoding.
    [Test]
    public void WireFormStartsWithTheFirstSwitchbackAndStays()
    {
        var experiment = AttributeSwitchback("city");
        experiment.SwitchbackClusters = Clusters(UnitType, "city", "user_id", "profile/n");
        var context = Create(new Dictionary<string, string> { [UnitType] = Uid, ["user_id"] = "u1" }, DataOf(experiment));
        Clock.Now = Anchor + 10;
        context.SetAttribute("city", "Faro");
        context.SetAttribute("profile", new Dictionary<string, object> { ["n"] = 3.0 });
        context.GetTreatment("exp_sb").Should().Be(Expected("Faro", 0));
        RefreshTo(context, AsUnit(AttributeSwitchback("city")));
        var json = Encoding.UTF8.GetString(new DefaultContextEventSerializer().Serialize(PublishEvent(context)));
        json.Should().Contain("\"value\":{\"n\":3.0}").And.Contain("\"name\":\"exp_sb\"");

        var unit = AsUnit(NewSwitchback(2, "exp_unit", 2, Hour));
        var mixed = Create(DataOf(unit, AttributeSwitchback("city")));
        Clock.Now = Anchor + 10;
        mixed.SetAttribute("plain", 3.0);
        Clock.Now = Anchor + 500;
        mixed.Track("goal", null!);
        Encoding.UTF8.GetString(new DefaultContextEventSerializer().Serialize(PublishEvent(mixed)))
            .Should().Contain("{\"name\":\"plain\",\"value\":3.0,\"setAt\":" + (Anchor + 10) + "}");
    }

    // Only attribute values take the wire form: goal properties, and attributes without a name or a
    // value, publish exactly as without switchbacks.
    [Test]
    public void OnlyAttributeValuesTakeTheWireForm()
    {
        var context = Create(DataOf(AttributeSwitchback("city")));
        Clock.Now = Anchor + 10;
        context.SetAttribute("empty", null!);
        context.SetAttribute(null!, "x");
        context.Track("goal", new Dictionary<string, object> { ["amount"] = 3.0 });
        var json = Encoding.UTF8.GetString(new DefaultContextEventSerializer().Serialize(PublishEvent(context)));
        json.Should().Contain("\"properties\":{\"amount\":3}");
        json.Should().Contain("{\"name\":\"empty\",\"setAt\":" + (Anchor + 10) + "}");
        json.Should().Contain("{\"value\":\"x\",\"setAt\":" + (Anchor + 10) + "}");
    }

    // A dictionary keyed by doubles keeps its published key names (3, not 3.0), so a path through
    // it resolves.
    [Test]
    public void DoubleKeyedDictionariesResolveByTheirPublishedKeys()
    {
        var context = Create(DataOf(AttributeSwitchback("tiers/3")));
        Clock.Now = Anchor + 10;
        context.SetAttribute("tiers", new Dictionary<double, string> { [3.0] = "Faro" });
        context.PeekTreatment("exp_sb").Should().Be(0, "an index segment never reads a map");

        var keyed = Create(DataOf(AttributeSwitchback("tiers/'3'")));
        Clock.Now = Anchor;
        keyed.SetAttribute("tiers", new Dictionary<double, string> { [3.0] = "Faro" });
        PeekSchedule(keyed).Should().Equal(ExpectedSchedule("Faro"));
    }

    // Without switchbacks attributes are published exactly as before (a whole double as 3).
    [Test]
    public void AttributesWithoutSwitchbacksArePublishedAsBefore()
    {
        var context = Create(DataOf(AsUnit(AttributeSwitchback("city"))));
        context.SetAttribute("profile", new Dictionary<string, object> { ["n"] = 3.0 });
        context.GetTreatment("exp_sb");
        var json = Encoding.UTF8.GetString(new DefaultContextEventSerializer().Serialize(PublishEvent(context)));
        json.Should().Contain("\"value\":{\"n\":3}");
    }

    // Review regression: a dictionary keyed by an enum beside the cluster field neither hides the
    // field nor changes how the attribute is published.
    [Test]
    public void EnumKeyedSiblingsDoNotHideTheClusterValue()
    {
        var context = Create(DataOf(AttributeSwitchback("profile/city")));
        Clock.Now = Anchor + 10;
        context.SetAttribute("profile", new Dictionary<string, object>
        {
            ["city"] = "Faro",
            ["hours"] = new Dictionary<DayOfWeek, int> { [DayOfWeek.Monday] = 9 },
            ["day"] = DayOfWeek.Friday
        });
        context.GetTreatment("exp_sb").Should().Be(Expected("Faro", 0));
        var json = Encoding.UTF8.GetString(new DefaultContextEventSerializer().Serialize(PublishEvent(context)));
        json.Should().Contain("\"value\":{\"city\":\"Faro\",\"hours\":{\"monday\":9},\"day\":5}");

        // The enum value itself is still no key.
        var enumValue = Create(DataOf(AttributeSwitchback("profile/day")));
        enumValue.SetAttribute("profile", new Dictionary<string, object> { ["day"] = DayOfWeek.Friday });
        enumValue.GetTreatment("exp_sb").Should().Be(0);
        enumValue.PendingCount.Should().Be(0);
    }

    // Within a period the cluster key is fixed at first resolution; the next period reads the
    // attribute again.
    [Test]
    public void SetAttributeMidPeriodDoesNotReassignWithinThePeriod()
    {
        var context = Create(DataOf(AttributeSwitchback("city")));
        Clock.Now = Anchor + 10;
        context.SetAttribute("city", "Lisboa");
        context.GetTreatment("exp_sb").Should().Be(Expected("Lisboa", 0));

        Clock.Now = Anchor + 20;
        context.SetAttribute("city", "Porto");
        context.GetTreatment("exp_sb").Should().Be(Expected("Lisboa", 0));
        context.PeekTreatment("exp_sb").Should().Be(Expected("Lisboa", 0));
        context.PendingCount.Should().Be(1);

        const int periods = 12;
        var differing = 0;
        for (var i = 1; i < periods; ++i)
        {
            Clock.Now = Anchor + i * Hour;
            context.GetTreatment("exp_sb").Should().Be(Expected("Porto", i));
            if (Expected("Porto", i) != Expected("Lisboa", i)) ++differing;
        }

        differing.Should().NotBe(0);
        context.PendingCount.Should().Be(periods);
    }

    // A unit first resolved without a key stays ineligible (and unrecorded) for the rest of that
    // period.
    [Test]
    public void AttributeSetAfterAnIneligibleResolutionWaitsForTheNextPeriod()
    {
        var context = Create(DataOf(AttributeSwitchback("city")));
        Clock.Now = Anchor + 10;
        context.GetTreatment("exp_sb").Should().Be(0);
        context.SetAttribute("city", "Lisboa");
        Clock.Now = Anchor + 20;
        context.GetTreatment("exp_sb").Should().Be(0);
        context.PendingCount.Should().Be(0);

        Clock.Now = Anchor + Hour;
        context.GetTreatment("exp_sb").Should().Be(Expected("Lisboa", 1));
        PublishExposures(context).Should().Equal(ClusterExposure(Expected("Lisboa", 1), Anchor + Hour, true, false));
    }

    // A slot replaced through an ineligible resolution still keeps the period's first key.
    [Test]
    public void TrafficChangesWithinThePeriodKeepThePeriodsBucket()
    {
        var context = Create(DataOf(AttributeSwitchback("city")));
        Clock.Now = Anchor + 10;
        context.SetAttribute("city", "Lisboa");
        context.GetTreatment("exp_sb").Should().Be(Expected("Lisboa", 0));

        Clock.Now = Anchor + 20;
        context.SetAttribute("city", "Porto");
        var noTraffic = AttributeSwitchback("city");
        noTraffic.TrafficSplit = new[] { 1.0, 0.0 };
        RefreshTo(context, noTraffic);
        context.GetTreatment("exp_sb").Should().Be(0);

        RefreshTo(context, AttributeSwitchback("city"));
        context.GetTreatment("exp_sb").Should().Be(Expected("Lisboa", 0));
        context.PendingCount.Should().Be(3);

        Clock.Now = Anchor + Hour;
        context.GetTreatment("exp_sb").Should().Be(Expected("Porto", 1));
    }

    // A traffic miss of an eligible cluster is recorded under the serving unit, and traffic restored
    // later in the period serves the key read at first resolution, stamped with that read's instant.
    [Test]
    public void TrafficRestoredWithinThePeriodKeepsTheKey()
    {
        var noTraffic = AttributeSwitchback("city");
        noTraffic.TrafficSplit = new[] { 1.0, 0.0 };
        var context = Create(DataOf(noTraffic));
        Clock.Now = Anchor + 10;
        context.SetAttribute("city", "Faro");
        context.GetTreatment("exp_sb").Should().Be(0);

        Clock.Now = Anchor + 20;
        context.SetAttribute("city", "Lisboa");
        RefreshTo(context, AttributeSwitchback("city"));
        Expected("Faro", 0).Should().Be(1);
        context.GetTreatment("exp_sb").Should().Be(Expected("Faro", 0));
        PublishExposures(context).Should().Equal(ClusterExposure(0, Anchor + 10, false, false),
            ClusterExposure(Expected("Faro", 0), Anchor + 10, true, false));
    }

    // An audience change re-resolves the period slot (new exposure state) but keeps its key and the
    // instant it was read, which stamps both exposures.
    [Test]
    public void AudienceRevalidationAfterSetAttributeKeepsThePeriodsBucket()
    {
        var experiment = AttributeSwitchback("city");
        experiment.Audience = StrictGoldAudience;
        var context = Create(DataOf(experiment));
        Clock.Now = Anchor + 10;
        context.SetAttribute("city", "Lisboa");
        context.SetAttribute("tier", "gold");
        context.GetTreatment("exp_sb").Should().Be(Expected("Lisboa", 0));

        Clock.Now = Anchor + 20;
        context.SetAttribute("city", "Porto");
        context.SetAttribute("tier", "silver");
        context.GetTreatment("exp_sb").Should().Be(Expected("Lisboa", 0));

        PublishExposures(context).Should().Equal(ClusterExposure(Expected("Lisboa", 0), Anchor + 10, true, false),
            ClusterExposure(Expected("Lisboa", 0), Anchor + 10, true, true));
    }

    // A slot first resolved without a key carries that missing key into its replacement.
    [Test]
    public void AudienceRevalidationAfterAnIneligibleResolutionKeepsTheMissingKey()
    {
        var experiment = AttributeSwitchback("city");
        experiment.Audience = StrictGoldAudience;
        var context = Create(DataOf(experiment));
        Clock.Now = Anchor + 10;
        context.SetAttribute("tier", "gold");
        context.GetTreatment("exp_sb").Should().Be(0);

        Clock.Now = Anchor + 20;
        context.SetAttribute("city", "Faro");
        context.SetAttribute("tier", "silver");
        Expected("Faro", 0).Should().Be(1);
        context.GetTreatment("exp_sb").Should().Be(0);
        context.PendingCount.Should().Be(0);

        Clock.Now = Anchor + Hour;
        context.PeekTreatment("exp_sb").Should().Be(Expected("Faro", 1));
        Clock.Now = Anchor + 2 * Hour;
        Expected("Faro", 2).Should().Be(2);
        context.PeekTreatment("exp_sb").Should().Be(Expected("Faro", 2));
    }

    [Test]
    public void ClusterPathChangesInvalidateThePeriod()
    {
        var context = Create(DataOf(AttributeSwitchback("city")));
        Clock.Now = Anchor + 10;
        context.SetAttribute("city", "Lisboa");
        context.SetAttribute("geo", new Dictionary<string, object> { ["city"] = "Porto" });
        context.GetTreatment("exp_sb").Should().Be(Expected("Lisboa", 0));
        context.PendingCount.Should().Be(1);

        RefreshTo(context, AttributeSwitchback("city"));
        context.GetTreatment("exp_sb").Should().Be(Expected("Lisboa", 0));
        context.PendingCount.Should().Be(1);

        RefreshTo(context, AttributeSwitchback("geo/city"));
        context.GetTreatment("exp_sb").Should().Be(Expected("Porto", 0));
        context.PendingCount.Should().Be(2);

        var filtered = AttributeSwitchback("geo/city");
        filtered.SwitchbackClusterValues = new[] { "Porto", "Faro" };
        RefreshTo(context, filtered);
        context.GetTreatment("exp_sb").Should().Be(Expected("Porto", 0));
        context.PendingCount.Should().Be(3);
    }

    // Re-linking the experiment to another unit type re-keys the period and moves the exposure unit.
    [Test]
    public void LinkedUnitTypeChangeRekeysThePeriod()
    {
        var context = Create(new Dictionary<string, string> { [UnitType] = Uid, ["user_id"] = "user-0002" },
            DataOf(AttributeSwitchback("city")));
        Clock.Now = Anchor + 1;
        context.SetAttribute("city", "Lisboa");
        context.SetAttribute("ucity", "Faro");
        context.GetTreatment("exp_sb").Should().Be(Expected("Lisboa", 0));

        var byUser = AttributeSwitchback("ucity");
        byUser.SwitchbackClusters = Clusters("user_id", "ucity");
        RefreshTo(context, byUser);
        context.GetTreatment("exp_sb").Should().Be(Expected("Faro", 0));
        PublishExposures(context).Select(e => e.Unit).Should().Equal(UnitType, "user_id");
    }

    [Test]
    public void CustomAssignmentAppliesOnlyToUnitsWithAClusterKey()
    {
        var keyed = Create(DataOf(AttributeSwitchback("city")));
        keyed.SetAttribute("city", "Lisboa");
        keyed.SetCustomAssignment("exp_sb", 2);
        keyed.GetTreatment("exp_sb").Should().Be(2);

        var unkeyed = Create(DataOf(AttributeSwitchback("city")));
        unkeyed.SetCustomAssignment("exp_sb", 2);
        unkeyed.GetTreatment("exp_sb").Should().Be(0);
    }

    // Inertness: a unit experiment ("unit" or absent) carrying every switchback field serves and
    // publishes exactly what the same experiment without them does.
    [TestCase("unit")]
    [TestCase(null)]
    public void UnitExperimentsIgnoreClusterFields(string? type)
    {
        var unit = AttributeSwitchback("city");
        unit.RandomizationType = type;
        unit.SwitchbackClusters = Clusters("user_id", "city", UnitType, "'x");
        unit.SwitchbackClusterValues = new[] { "nothing" };
        unit.SwitchbackPeriodMs = 0L;
        // A split rule randomizes over unitType for a unit experiment, never a cluster unit.
        unit.AssignmentRules = "{\"rules\":[{\"type\":\"split\",\"environments\":[],\"percentages\":\"34/33/33\"}]}";
        var reference = new Experiment
        {
            Id = unit.Id, Name = unit.Name, UnitType = unit.UnitType, Iteration = unit.Iteration, SeedHi = unit.SeedHi,
            SeedLo = unit.SeedLo, Split = unit.Split, TrafficSeedHi = unit.TrafficSeedHi, TrafficSeedLo = unit.TrafficSeedLo,
            TrafficSplit = unit.TrafficSplit, Applications = unit.Applications, Variants = unit.Variants,
            RandomizationType = type, AssignmentRules = unit.AssignmentRules
        };

        var published = new List<Exposure[]>();
        foreach (var experiment in new[] { unit, reference })
        {
            var context = Create(new Dictionary<string, string> { [UnitType] = Uid, ["user_id"] = "u" }, DataOf(experiment));
            context.SetAttribute("city", "Lisboa");
            Clock.Now = Anchor - Hour;
            var first = context.GetTreatment("exp_sb");
            for (var i = 1; i < 6; ++i)
            {
                Clock.Now = Anchor - Hour + i * Hour;
                context.GetTreatment("exp_sb").Should().Be(first);
            }

            published.Add(PublishExposures(context));
        }

        published[0].Should().ContainSingle();
        published[0][0].Eligible.Should().BeTrue();
        published[0][0].RuleOverride.Should().BeTrue();
        published[0][0].ExposedAt.Should().Be(Anchor - Hour);
        published[0].Should().Equal(published[1]);
    }
}
