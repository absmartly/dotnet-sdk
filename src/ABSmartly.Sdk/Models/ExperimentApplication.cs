using System.Diagnostics;

namespace ABSmartly.Models;

[DebuggerDisplay("{DebugView},nq")]
public class ExperimentApplication
{
    public string Name { get; set; }

    private string DebugView => $"ExperimentApplication{{name={Name}}}";

    public override string ToString()
    {
        return DebugView;
    }

    #region Equality members

    protected bool Equals(ExperimentApplication other)
    {
        return Name == other.Name;
    }

    public override bool Equals(object obj)
    {
        if (ReferenceEquals(null, obj)) return false;
        if (ReferenceEquals(this, obj)) return true;
        if (obj.GetType() != GetType()) return false;
        return Equals((ExperimentApplication)obj);
    }

    public override int GetHashCode()
    {
        return Name?.GetHashCode() ?? 0;
    }

    #endregion
}