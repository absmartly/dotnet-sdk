using System.Diagnostics;
using ABSmartly.EqualityComparison;
using ABSmartly.Extensions;

namespace ABSmartly.Models;

[DebuggerDisplay("{DebugView},nq")]
public class PublishEvent
{
    public bool Hashed { get; set; }
    public Unit[] Units { get; set; }
    public long PublishedAt { get; set; }
    public Exposure[] Exposures { get; set; }
    public GoalAchievement[] Goals { get; set; }
    public Attribute[] Attributes { get; set; }

    private string DebugView =>
        $"PublishEvent{{hashedUnits={Hashed}, units={Units.ToArrayString()}, publishedAt={PublishedAt}, exposures={Exposures.ToArrayString()}, goals={Goals.ToArrayString()}, attributes={Attributes.ToArrayString()}}}";

    public override string ToString()
    {
        return DebugView;
    }

    #region Equality members

    protected bool Equals(PublishEvent other)
    {
        return Hashed == other.Hashed &&
               PublishedAt == other.PublishedAt &&
               ArrayEquality.Equals(Units, other.Units) &&
               ArrayEquality.Equals(Exposures, other.Exposures) &&
               ArrayEquality.Equals(Goals, other.Goals) &&
               ArrayEquality.Equals(Attributes, other.Attributes);
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

    #endregion
}