using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
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
    [JsonConverter(typeof(LenientStringConverter))]
    public string AssignmentRules { get; set; }
    // The ids of the holdouts (ContextData.Holdouts) covering this experiment. Null or empty means
    // it is not covered. Entries that are not integers are skipped, so they do not fail the context.
    [JsonConverter(typeof(HoldoutIdsConverter))]
    public int[] HoldoutIds { get; set; }
    // Set only on ContextData.Holdouts entries ("full", "all_full_on"); informational, the arm
    // count comes from the split. A value that is not a string is read as null.
    [JsonConverter(typeof(LenientStringConverter))]
    public string HoldoutType { get; set; }

    // Switchback configuration (see SWITCHBACK.md in java-sdk). RandomizationType is "unit" or
    // "switchback"; null means unit. Instants and durations are epoch/duration milliseconds. Every
    // field is optional, and a wrongly typed value is read as null rather than failing the context.
    [JsonConverter(typeof(LenientStringConverter))]
    public string RandomizationType { get; set; }
    // Unit type name -> attribute path, in the served order (unit_type_id ascending). The order is
    // semantic: the first unit yielding a value serves the experiment, so it is kept as served (never
    // sorted) and Equals compares it. A path that is not a string is kept as a null entry, which makes
    // the map unreadable rather than dropping that source.
    [JsonConverter(typeof(SwitchbackClustersConverter))]
    public KeyValuePair<string, string>[] SwitchbackClusters { get; set; }
    // Allow-list of cluster key values; null or empty admits every value.
    [JsonConverter(typeof(LenientStringArrayConverter))]
    public string[] SwitchbackClusterValues { get; set; }
    [JsonConverter(typeof(LenientLongConverter))]
    public long? SwitchbackStartAt { get; set; }
    [JsonConverter(typeof(LenientLongConverter))]
    public long? SwitchbackPeriodMs { get; set; }
    // Carryover and burn windows are analysis-only and never read by assignment.
    [JsonConverter(typeof(LenientLongConverter))]
    public long? SwitchbackCarryoverDurationMs { get; set; }
    [JsonConverter(typeof(LenientLongConverter))]
    public long? SwitchbackBurnInDurationMs { get; set; }
    [JsonConverter(typeof(LenientLongConverter))]
    public long? SwitchbackBurnOutDurationMs { get; set; }
    [JsonConverter(typeof(LenientStringConverter))]
    public string SwitchbackDesignType { get; set; }
    [JsonConverter(typeof(LenientStringConverter))]
    public string SwitchbackEstimand { get; set; }

    private string DebugView =>
        $"ContextExperiment{{id={Id}, name={Name}, unitType={UnitType}, iteration={Iteration}, seedHi={SeedHi}, seedLo={SeedLo}, split={Split.ToArrayString()}, trafficSeedHi={TrafficSeedHi}, trafficSeedLo={TrafficSeedLo}, trafficSplit={TrafficSplit.ToArrayString()}, fullOnVariant={FullOnVariant}, applications={Applications.ToArrayString()}, variants={Variants.ToArrayString()}, audienceStrict={AudienceStrict}, audience={Audience}, assignmentRules={AssignmentRules}{HoldoutDebugView}{SwitchbackDebugView}}}";

    private string HoldoutDebugView =>
        (HoldoutIds == null ? "" : $", holdoutIds={HoldoutIds.ToArrayString()}") +
        (HoldoutType == null ? "" : $", holdoutType={HoldoutType}");

    private string SwitchbackDebugView =>
        RandomizationType == null && SwitchbackClusters == null && SwitchbackClusterValues == null &&
        SwitchbackStartAt == null && SwitchbackPeriodMs == null && SwitchbackCarryoverDurationMs == null &&
        SwitchbackBurnInDurationMs == null && SwitchbackBurnOutDurationMs == null && SwitchbackDesignType == null &&
        SwitchbackEstimand == null
            ? ""
            : $", randomizationType={RandomizationType}, switchbackClusters={{{string.Join(",", (SwitchbackClusters ?? Array.Empty<KeyValuePair<string, string>>()).Select(c => c.Key + "=" + c.Value))}}}, switchbackClusterValues={SwitchbackClusterValues.ToArrayString()}, switchbackStartAt={SwitchbackStartAt}, switchbackPeriodMs={SwitchbackPeriodMs}, switchbackCarryoverDurationMs={SwitchbackCarryoverDurationMs}, switchbackBurnInDurationMs={SwitchbackBurnInDurationMs}, switchbackBurnOutDurationMs={SwitchbackBurnOutDurationMs}, switchbackDesignType={SwitchbackDesignType}, switchbackEstimand={SwitchbackEstimand}";

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
               RandomizationType == other.RandomizationType &&
               ArrayEquality.Equals(SwitchbackClusters, other.SwitchbackClusters) &&
               ArrayEquality.Equals(SwitchbackClusterValues, other.SwitchbackClusterValues) &&
               SwitchbackStartAt == other.SwitchbackStartAt &&
               SwitchbackPeriodMs == other.SwitchbackPeriodMs &&
               SwitchbackCarryoverDurationMs == other.SwitchbackCarryoverDurationMs &&
               SwitchbackBurnInDurationMs == other.SwitchbackBurnInDurationMs &&
               SwitchbackBurnOutDurationMs == other.SwitchbackBurnOutDurationMs &&
               SwitchbackDesignType == other.SwitchbackDesignType &&
               SwitchbackEstimand == other.SwitchbackEstimand &&
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
            // Likewise for the switchback fields; the cluster map is hashed in its declared order.
            if (RandomizationType != null) hashCode = (hashCode * 397) ^ RandomizationType.GetHashCode();
            if (SwitchbackClusters != null)
            {
                hashCode = (hashCode * 397) ^ SwitchbackClusters.Length;
                foreach (var cluster in SwitchbackClusters)
                {
                    hashCode = (hashCode * 397) ^ (cluster.Key?.GetHashCode() ?? 0);
                    hashCode = (hashCode * 397) ^ (cluster.Value?.GetHashCode() ?? 0);
                }
            }
            if (SwitchbackClusterValues != null)
            {
                hashCode = (hashCode * 397) ^ SwitchbackClusterValues.Length;
                foreach (var value in SwitchbackClusterValues) hashCode = (hashCode * 397) ^ (value?.GetHashCode() ?? 0);
            }
            if (SwitchbackStartAt != null) hashCode = (hashCode * 397) ^ SwitchbackStartAt.Value.GetHashCode();
            if (SwitchbackPeriodMs != null) hashCode = (hashCode * 397) ^ SwitchbackPeriodMs.Value.GetHashCode();
            if (SwitchbackCarryoverDurationMs != null)
                hashCode = (hashCode * 397) ^ SwitchbackCarryoverDurationMs.Value.GetHashCode();
            if (SwitchbackBurnInDurationMs != null)
                hashCode = (hashCode * 397) ^ SwitchbackBurnInDurationMs.Value.GetHashCode();
            if (SwitchbackBurnOutDurationMs != null)
                hashCode = (hashCode * 397) ^ SwitchbackBurnOutDurationMs.Value.GetHashCode();
            if (SwitchbackDesignType != null) hashCode = (hashCode * 397) ^ SwitchbackDesignType.GetHashCode();
            if (SwitchbackEstimand != null) hashCode = (hashCode * 397) ^ SwitchbackEstimand.GetHashCode();
            return hashCode;
        }
    }

    #endregion
}