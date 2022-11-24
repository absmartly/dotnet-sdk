using System.Diagnostics;

namespace ABSmartly.Models;

[DebuggerDisplay("{DebugView},nq")]
public class ExperimentVariant
{
    public string Name { get; set; }
    public string Config { get; set; }

    private string DebugView => $"ExperimentVariant{{name={Name}, config={Config}}}";

    public override string ToString()
    {
        return DebugView;
    }


    #region Equality members

    protected bool Equals(ExperimentVariant other)
    {
        return Name == other.Name &&
               Config == other.Config;
    }

    public override bool Equals(object obj)
    {
        if (ReferenceEquals(null, obj)) return false;
        if (ReferenceEquals(this, obj)) return true;
        if (obj.GetType() != GetType()) return false;
        return Equals((ExperimentVariant)obj);
    }

    public override int GetHashCode()
    {
        unchecked
        {
            return ((Name?.GetHashCode() ?? 0) * 397) ^ (Config?.GetHashCode() ?? 0);
        }
    }

    #endregion
}