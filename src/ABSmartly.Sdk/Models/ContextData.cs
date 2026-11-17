using System.Diagnostics;
using System.Text.Json.Serialization;
using ABSmartly.Services.Json;
using ABSmartly.EqualityComparison;
using ABSmartly.Extensions;

namespace ABSmartly.Models;

[DebuggerDisplay("{DebugView},nq")]
public class ContextData
{
    public ContextData()
    {
    }

    public ContextData(Experiment[] experiments)
    {
        Experiments = experiments;
    }

    public ContextData(Experiment[] experiments, Experiment[] holdouts)
    {
        Experiments = experiments;
        Holdouts = holdouts;
    }

    public Experiment[] Experiments { get; set; }

    // The holdouts that experiments reference through Experiment.HoldoutIds. Null when the payload
    // has none. An entry without a split is ignored, and a malformed one is skipped.
    [JsonConverter(typeof(HoldoutsConverter))]
    public Experiment[] Holdouts { get; set; }

    private string DebugView => Holdouts == null
        ? $"ContextData{{experiments={Experiments.ToArrayString()}}}"
        : $"ContextData{{experiments={Experiments.ToArrayString()}, holdouts={Holdouts.ToArrayString()}}}";

    public override string ToString()
    {
        return DebugView;
    }


    #region Equality members

    protected bool Equals(ContextData other)
    {
        return ArrayEquality.Equals(Experiments, other.Experiments) &&
               ArrayEquality.Equals(Holdouts, other.Holdouts);
    }

    public override bool Equals(object obj)
    {
        if (ReferenceEquals(null, obj)) return false;
        if (ReferenceEquals(this, obj)) return true;
        if (obj.GetType() != GetType()) return false;
        return Equals((ContextData)obj);
    }

    public override int GetHashCode()
    {
        var hashCode = Experiments?.GetHashCode() ?? 0;
        if (Holdouts == null) return hashCode;

        // Mixed in only when present, so data without holdouts hashes as before. By content,
        // consistent with Equals.
        unchecked
        {
            hashCode = hashCode * 397 ^ Holdouts.Length;
            foreach (var holdout in Holdouts)
                hashCode = hashCode * 397 ^ (holdout == null ? 0 : holdout.Id * 31 + holdout.Iteration);
            return hashCode;
        }
    }

    #endregion
}