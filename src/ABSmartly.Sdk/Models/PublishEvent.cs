using System.Diagnostics;
using ABSmartly.Extensions;
using ABSmartlySdk.Json;

namespace ABSmartly.Models;

[DebuggerDisplay("{StringLook}")]
public class PublishEvent
{
    public bool Hashed { get; set; }
    public Unit[] Units { get; set; }
    public long PublishedAt { get; set; }
    public Exposure[] Exposures { get; set; }
    public GoalAchievement[] Goals { get; set; }
    public Attribute[] Attributes { get; set; }

    #region Overrides - Equality / Hash / ToString

    protected bool Equals(PublishEvent other)
    {
        return Hashed == other.Hashed && Equals(Units, other.Units) && PublishedAt == other.PublishedAt &&
               Equals(Exposures, other.Exposures) && Equals(Goals, other.Goals) && Equals(Attributes, other.Attributes);
    }

    public override bool Equals(object obj)
    {
        if (ReferenceEquals(null, obj)) return false;
        if (ReferenceEquals(this, obj)) return true;
        if (obj.GetType() != GetType()) return false;
        return Equals((PublishEvent)obj);
    }

    public override int GetHashCode()
    {
        unchecked
        {
            var hashCode = Hashed.GetHashCode();
            hashCode = (hashCode * 397) ^ (Units != null ? Units.GetHashCode() : 0);
            hashCode = (hashCode * 397) ^ PublishedAt.GetHashCode();
            hashCode = (hashCode * 397) ^ (Exposures != null ? Exposures.GetHashCode() : 0);
            hashCode = (hashCode * 397) ^ (Goals != null ? Goals.GetHashCode() : 0);
            hashCode = (hashCode * 397) ^ (Attributes != null ? Attributes.GetHashCode() : 0);
            return hashCode;
        }
    }

    public override string ToString()
    {
        return StringLook;
    }

    private string StringLook =>
        "PublishEvent{" +
        "hashedUnits=" + Hashed +
        ", units=" + Units.ToArrayString() +
        ", publishedAt=" + PublishedAt +
        ", exposures=" + Exposures.ToArrayString() +
        ", goals=" + Goals.ToArrayString() +
        ", attributes=" + Attributes.ToArrayString() +
        '}';

    #endregion
}