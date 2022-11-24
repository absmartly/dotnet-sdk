using System.Diagnostics;
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

    public Experiment[] Experiments { get; set; }

    private string DebugView => $"ContextData{{experiments={Experiments.ToArrayString()}}}";

    public override string ToString()
    {
        return DebugView;
    }


    #region Equality members

    protected bool Equals(ContextData other)
    {
        return ArrayEquality.Equals(Experiments, other.Experiments);
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
        return Experiments?.GetHashCode() ?? 0;
    }

    #endregion
}