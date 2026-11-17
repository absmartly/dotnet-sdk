using System.Diagnostics;
using System.Text.Json.Serialization;
using ABSmartly.Services.Json;
using ABSmartly.EqualityComparison;
using ABSmartly.Extensions;

namespace ABSmartly.Models;

[DebuggerDisplay("{DebugView},nq")]
public class Experiment
{
    public int Id { get; set; }
    public string Name { get; set; }
    public string UnitType { get; set; }
    public int Iteration { get; set; }
    public int SeedHi { get; set; }
    public int SeedLo { get; set; }
    public double[] Split { get; set; }
    public int TrafficSeedHi { get; set; }
    public int TrafficSeedLo { get; set; }
    public double[] TrafficSplit { get; set; }
    public int FullOnVariant { get; set; }
    public ExperimentApplication[] Applications { get; set; }
    public ExperimentVariant[] Variants { get; set; }
    public CustomFieldValue[] CustomFieldValues { get; set; }
    public bool AudienceStrict { get; set; }
    public string Audience { get; set; }
    // The experiment's ordered assignment rules, a JSON string such as
    // {"rules":[{"type":"assign","variant":1,"conditions":{...},"environments":[]}]}. A value
    // that is not a string is read as null, so it does not fail the whole context.
    [JsonConverter(typeof(AssignmentRulesConverter))]
    public string AssignmentRules { get; set; }
    // The ids of the holdouts (ContextData.Holdouts) covering this experiment. Null or empty means
    // it is not covered. Entries that are not integers are skipped, so they do not fail the context.
    [JsonConverter(typeof(HoldoutIdsConverter))]
    public int[] HoldoutIds { get; set; }
    // Set only on ContextData.Holdouts entries ("full", "all_full_on"); informational, the arm
    // count comes from the split. A value that is not a string is read as null.
    [JsonConverter(typeof(LenientStringConverter))]
    public string HoldoutType { get; set; }

    private string DebugView =>
        $"ContextExperiment{{id={Id}, name={Name}, unitType={UnitType}, iteration={Iteration}, seedHi={SeedHi}, seedLo={SeedLo}, split={Split.ToArrayString()}, trafficSeedHi={TrafficSeedHi}, trafficSeedLo={TrafficSeedLo}, trafficSplit={TrafficSplit.ToArrayString()}, fullOnVariant={FullOnVariant}, applications={Applications.ToArrayString()}, variants={Variants.ToArrayString()}, audienceStrict={AudienceStrict}, audience={Audience}, assignmentRules={AssignmentRules}{HoldoutDebugView}}}";

    private string HoldoutDebugView =>
        (HoldoutIds == null ? "" : $", holdoutIds={HoldoutIds.ToArrayString()}") +
        (HoldoutType == null ? "" : $", holdoutType={HoldoutType}");

    public override string ToString()
    {
        return DebugView;
    }


    #region Equality members

    protected bool Equals(Experiment other)
    {
        return Id == other.Id &&
               Name == other.Name &&
               UnitType == other.UnitType &&
               Iteration == other.Iteration &&
               SeedHi == other.SeedHi &&
               SeedLo == other.SeedLo &&
               TrafficSeedHi == other.TrafficSeedHi &&
               TrafficSeedLo == other.TrafficSeedLo &&
               FullOnVariant == other.FullOnVariant &&
               AudienceStrict == other.AudienceStrict &&
               Audience == other.Audience &&
               AssignmentRules == other.AssignmentRules &&
               HoldoutType == other.HoldoutType &&
               ArrayEquality.Equals(HoldoutIds, other.HoldoutIds) &&
               ArrayEquality.Equals(Split, other.Split) &&
               ArrayEquality.Equals(TrafficSplit, other.TrafficSplit) &&
               ArrayEquality.Equals(Applications, other.Applications) &&
               ArrayEquality.Equals(Variants, other.Variants);
    }

    public override bool Equals(object obj)
    {
        if (ReferenceEquals(null, obj)) return false;
        if (ReferenceEquals(this, obj)) return true;
        if (obj.GetType() != GetType()) return false;
        return Equals((Experiment)obj);
    }

    public override int GetHashCode()
    {
        unchecked
        {
            var hashCode = Id;
            hashCode = (hashCode * 397) ^ (Name?.GetHashCode() ?? 0);
            hashCode = (hashCode * 397) ^ (UnitType?.GetHashCode() ?? 0);
            hashCode = (hashCode * 397) ^ Iteration;
            hashCode = (hashCode * 397) ^ SeedHi;
            hashCode = (hashCode * 397) ^ SeedLo;
            hashCode = (hashCode * 397) ^ (Split?.GetHashCode() ?? 0);
            hashCode = (hashCode * 397) ^ TrafficSeedHi;
            hashCode = (hashCode * 397) ^ TrafficSeedLo;
            hashCode = (hashCode * 397) ^ (TrafficSplit?.GetHashCode() ?? 0);
            hashCode = (hashCode * 397) ^ FullOnVariant;
            hashCode = (hashCode * 397) ^ (Applications?.GetHashCode() ?? 0);
            hashCode = (hashCode * 397) ^ (Variants?.GetHashCode() ?? 0);
            hashCode = (hashCode * 397) ^ AudienceStrict.GetHashCode();
            hashCode = (hashCode * 397) ^ (Audience?.GetHashCode() ?? 0);
            hashCode = (hashCode * 397) ^ (AssignmentRules?.GetHashCode() ?? 0);
            // Mixed in only when present, so experiments without holdout fields hash as before.
            if (HoldoutIds != null)
            {
                hashCode = (hashCode * 397) ^ HoldoutIds.Length;
                foreach (var id in HoldoutIds) hashCode = (hashCode * 397) ^ id;
            }
            if (HoldoutType != null) hashCode = (hashCode * 397) ^ HoldoutType.GetHashCode();
            return hashCode;
        }
    }

    #endregion
}