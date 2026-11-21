using System.Diagnostics;

namespace ABSmartly.Models;

[DebuggerDisplay("{DebugView},nq")]
public class Attribute
{
    public string Name { get; set; }
    private object _value;
    public object Value
    {
        get => _value;
        set
        {
            _value = value;
            WireValue = null;
        }
    }
    public long SetAt { get; set; }

    // A switchback context's wire encoding of a structured Value (a boxed JsonElement, so it is
    // published whole to a serializer on another thread), taken when it was set or first read by a
    // cluster lookup, so a caller that later mutates the object it passed cannot change the cluster
    // an earlier exposure replays from. Not compared, not public: Value is untouched.
    internal object WireValue { get; set; }

    private string DebugView => $"Attribute{{name={Name}, value={Value}, setAt={SetAt}}}";

    public override string ToString()
    {
        return DebugView;
    }

    #region Equality members

    protected bool Equals(Attribute other)
    {
        return Name == other.Name &&
               Equals(Value, other.Value) &&
               SetAt == other.SetAt;
    }

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
}