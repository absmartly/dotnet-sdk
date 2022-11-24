using System.Diagnostics;

namespace ABSmartly.Models;

[DebuggerDisplay("{DebugView},nq")]
public class Unit
{
    public string Type { get; set; }
    public string Uid { get; set; }

    private string DebugView => $"Unit{{type={Type}, uid={Uid}}}";

    public override string ToString()
    {
        return DebugView;
    }

    #region Equality members

    protected bool Equals(Unit other)
    {
        return Type == other.Type &&
               Uid == other.Uid;
    }

    public override bool Equals(object obj)
    {
        if (ReferenceEquals(null, obj)) return false;
        if (ReferenceEquals(this, obj)) return true;
        if (obj.GetType() != GetType()) return false;
        return Equals((Unit)obj);
    }

    public override int GetHashCode()
    {
        unchecked
        {
            return ((Type?.GetHashCode() ?? 0) * 397) ^ (Uid?.GetHashCode() ?? 0);
        }
    }

    #endregion
}