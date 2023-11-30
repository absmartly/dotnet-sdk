using System.Diagnostics;

namespace ABSmartly.Models;

[DebuggerDisplay("{DebugView},nq")]
public class CustomFieldValue
{
    public string Name { get; set; }
    public string Type { get; set; }
    public string Value { get; set; }

    private string DebugView => $"ExperimentVariant{{name={Name}, type={Type}, value={Value}}}";

    public override string ToString()
    {
        return DebugView;
    }


    #region Equality members

    protected bool Equals(CustomFieldValue other)
    {
        return Name == other.Name &&
               Type == other.Type&&
               Value == other.Value;
    }

    public override bool Equals(object obj)
    {
        if (ReferenceEquals(null, obj)) return false;
        if (ReferenceEquals(this, obj)) return true;
        if (obj.GetType() != GetType()) return false;
        return Equals((CustomFieldValue)obj);
    }

    public override int GetHashCode()
    {
        unchecked
        {
            return ((Name?.GetHashCode() ?? 0) * 397) ^ (Type?.GetHashCode() ?? 0)  ^ (Value?.GetHashCode() ?? 0);
        }
    }

    #endregion
}