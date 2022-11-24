using System.Collections.Generic;
using System.Diagnostics;
using ABSmartly.EqualityComparison;

namespace ABSmartly.Models;

[DebuggerDisplay("{DebugView},nq")]
public class GoalAchievement
{
    public string Name { get; set; }
    public long AchievedAt { get; set; }
    public IDictionary<string, object> Properties { get; set; }

    #region Equality members

    protected bool Equals(GoalAchievement other) =>
        Name == other.Name && 
        AchievedAt == other.AchievedAt &&
        new DictionaryComparer(EqualityComparerSelectors.Default).Equals(Properties, other.Properties);

    public override bool Equals(object obj)
    {
        if (ReferenceEquals(null, obj)) return false;
        if (ReferenceEquals(this, obj)) return true;
        if (obj.GetType() != GetType()) return false;
        return Equals((GoalAchievement)obj);
    }

    public override int GetHashCode()
    {
        unchecked
        {
            var hashCode = Name != null ? Name.GetHashCode() : 0;
            hashCode = (hashCode * 397) ^ AchievedAt.GetHashCode();
            hashCode = (hashCode * 397) ^ (Properties?.GetHashCode() ?? 0);
            return hashCode;
        }
    }

    #endregion

    private string DebugView => $"GoalAchievement{{name={Name}, achievedAt={AchievedAt}, properties={Properties}}}";
    public override string ToString() => DebugView;
}