using System.Diagnostics;

namespace ABSmartly.Models;

[DebuggerDisplay("{DebugView},nq")]
public class Attribute
{
    public string Name { get; set; }
    public object Value { get; set; }
    public long SetAt { get; set; }

    #region Equality members

    protected bool Equals(Attribute other) => 
        Name == other.Name && 
        Equals(Value, other.Value) && 
        SetAt == other.SetAt;

    public override bool Equals(object obj)
    {
        if (ReferenceEquals(null, obj)) return false;
        if (ReferenceEquals(this, obj)) return true;
        if (obj.GetType() != GetType()) return false;
        return Equals((Attribute)obj);
    }

    public override int GetHashCode()
    {
        unchecked
        {
            var hashCode = Name?.GetHashCode() ?? 0;
            hashCode = (hashCode * 397) ^ (Value?.GetHashCode() ?? 0);
            hashCode = (hashCode * 397) ^ SetAt.GetHashCode();
            return hashCode;
        }
    }

    #endregion
    
    private string DebugView => $"Attribute{{name={Name}, value={Value}, setAt={SetAt}}}";
    public override string ToString() => DebugView;
}