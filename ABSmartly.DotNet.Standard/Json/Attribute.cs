namespace ABSmartly.Json;

public class Attribute
{
    public string Name { get; set; }
    public object Value { get; set; }
    public long SetAt { get; set; }

    public Attribute()
    {
        
    }

    public Attribute(string name, object value, long setAt)
    {
        Name = name;
        Value = value;
        SetAt = setAt;
    }


    protected bool Equals(Attribute other)
    {
        return Name == other.Name && Equals(Value, other.Value) && SetAt == other.SetAt;
    }

    public override bool Equals(object obj)
    {
        if (ReferenceEquals(null, obj)) return false;
        if (ReferenceEquals(this, obj)) return true;
        if (obj.GetType() != this.GetType()) return false;
        return Equals((Attribute)obj);
    }

    public override int GetHashCode()
    {
        unchecked
        {
            var hashCode = (Name != null ? Name.GetHashCode() : 0);
            hashCode = (hashCode * 397) ^ (Value != null ? Value.GetHashCode() : 0);
            hashCode = (hashCode * 397) ^ SetAt.GetHashCode();
            return hashCode;
        }
    }

    public override string ToString()
    {
        return "Attribute{" +
               "name='" + Name + '\'' +
               ", value=" + Value +
               ", setAt=" + SetAt +
               '}';
    }
}