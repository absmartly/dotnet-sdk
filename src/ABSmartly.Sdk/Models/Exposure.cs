using System.Diagnostics;

namespace ABSmartly.Models;

[DebuggerDisplay("{DebugView},nq")]
public sealed class Exposure
{
    public int Id { get; set; }
    public string Name { get; set; }
    public string Unit { get; set; }
    public int Variant { get; set; }
    public long ExposedAt { get; set; }
    public bool Assigned { get; set; }
    public bool Eligible { get; set; }
    public bool Overridden { get; set; }
    public bool FullOn { get; set; }
    public bool Custom { get; set; }
    public bool AudienceMismatch { get; set; }
    // True when a matching custom assignment rule selected the variant. The collector
    // excludes this exposure from normal experiment analysis, as it does for overrides.
    public bool RuleOverride { get; set; }

    // For a switchback exposure, the number of context units (in the order they were set) present
    // when its cluster was served; 0 otherwise. Not serialized and not part of equality: the context
    // publishes such an exposure with that unit set, so the Collector replays the same cluster.
    internal int ServingUnitCount { get; set; }

    // For such an exposure, its experiment's linked unit types (the switchbackClusters keys): only a
    // linked unit set after serving can change the Collector's replay.
    internal string[] LinkedUnitTypes { get; set; }

    private string DebugView =>
        $"Exposure{{id={Id}, name={Name}, unit={Unit}, variant={Variant}, exposedAt={ExposedAt}, assigned={Assigned}, eligible={Eligible}, overridden={Overridden}, fullOn={FullOn}, custom={Custom}, audienceMismatch={AudienceMismatch}, ruleOverride={RuleOverride}}}";

    public override string ToString()
    {
        return DebugView;
    }

    #region Equality members

    private bool Equals(Exposure other)
    {
        return Id == other.Id &&
               Name == other.Name &&
               Unit == other.Unit &&
               Variant == other.Variant &&
               ExposedAt == other.ExposedAt &&
               Assigned == other.Assigned &&
               Eligible == other.Eligible &&
               Overridden == other.Overridden &&
               FullOn == other.FullOn &&
               Custom == other.Custom &&
               AudienceMismatch == other.AudienceMismatch &&
               RuleOverride == other.RuleOverride;
    }

    public override bool Equals(object obj)
    {
        if (ReferenceEquals(null, obj)) return false;
        if (ReferenceEquals(this, obj)) return true;
        if (obj.GetType() != GetType()) return false;
        return Equals((Exposure)obj);
    }

    public override int GetHashCode()
    {
        unchecked
        {
            var hashCode = Id;
            hashCode = (hashCode * 397) ^ (Name?.GetHashCode() ?? 0);
            hashCode = (hashCode * 397) ^ (Unit?.GetHashCode() ?? 0);
            hashCode = (hashCode * 397) ^ Variant;
            hashCode = (hashCode * 397) ^ ExposedAt.GetHashCode();
            hashCode = (hashCode * 397) ^ Assigned.GetHashCode();
            hashCode = (hashCode * 397) ^ Eligible.GetHashCode();
            hashCode = (hashCode * 397) ^ Overridden.GetHashCode();
            hashCode = (hashCode * 397) ^ FullOn.GetHashCode();
            hashCode = (hashCode * 397) ^ Custom.GetHashCode();
            hashCode = (hashCode * 397) ^ AudienceMismatch.GetHashCode();
            hashCode = (hashCode * 397) ^ RuleOverride.GetHashCode();
            return hashCode;
        }
    }

    #endregion
}