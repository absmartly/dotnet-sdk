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

    // Set by a context with switchback experiments installed: DefaultContextEventSerializer then
    // writes attribute values as the cluster lookup read them (a whole floating-point number as
    // 3.0), so the Collector re-resolves the same clusters. Not serialized; the values are untouched.
    internal bool SwitchbackWireAttributes { get; set; }

    // Set by a context on each request of a flush split into several (switchback exposures served
    // before a linked unit was set): a request that could be retried is then raised as a failure,
    // so it and the later requests stay queued.
    internal bool SplitRequest { get; set; }

    // Set by ABSmartlyService when a publish failed in a way a retry can fix: a transport error or
    // timeout, or HTTP 408, 429 or 5xx. Not for an event that could not be serialized, or another
    // 4xx, which would fail again.
    internal bool RetryableFailure { get; set; }

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