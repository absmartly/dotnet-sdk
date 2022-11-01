﻿using ABSmartly.Extensions;

namespace ABSmartly.Models;

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
    public bool AudienceStrict { get; set; }
    public string Audience { get; set; }


    #region Overrides - Equality / Hash / ToString

    protected bool Equals(Experiment other)
    {
        return Id == other.Id && Name == other.Name && UnitType == other.UnitType && Iteration == other.Iteration &&
               SeedHi == other.SeedHi && SeedLo == other.SeedLo && Equals(Split, other.Split) &&
               TrafficSeedHi == other.TrafficSeedHi && TrafficSeedLo == other.TrafficSeedLo &&
               Equals(TrafficSplit, other.TrafficSplit) && FullOnVariant == other.FullOnVariant &&
               Equals(Applications, other.Applications) && Equals(Variants, other.Variants) &&
               AudienceStrict == other.AudienceStrict && Audience == other.Audience;
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
            return hashCode;
        }
    }

    public override string ToString()
    {
        return "ContextExperiment{" +
               "id=" + Id +
               ", name='" + Name + '\'' +
               ", unitType='" + UnitType + '\'' +
               ", iteration=" + Iteration +
               ", seedHi=" + SeedHi +
               ", seedLo=" + SeedLo +
               ", split=" + Split.ToArrayString() +
               ", trafficSeedHi=" + TrafficSeedHi +
               ", trafficSeedLo=" + TrafficSeedLo +
               ", trafficSplit=" + TrafficSplit.ToArrayString() +
               ", fullOnVariant=" + FullOnVariant +
               ", applications=" + Applications.ToArrayString() +
               ", variants=" + Variants.ToArrayString() +
               ", audienceStrict=" + AudienceStrict +
               ", audience='" + Audience + '\'' +
               '}';
    }

    #endregion
}