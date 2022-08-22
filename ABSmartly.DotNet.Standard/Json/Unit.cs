namespace ABSmartlySdk.Json;

//@JsonInclude(JsonInclude.Include.NON_NULL)
//@JsonIgnoreProperties(ignoreUnknown = true)
public class Unit
{
    public Unit(string type, string uid)
    {
        Type = type;
        Uid = uid;
    }

    public string Type { get; set; }
    public string Uid { get; set; }

    #region Overrides - Equality / Hash / ToString

    protected bool Equals(Unit other)
    {
        return Type == other.Type && Uid == other.Uid;
    }

    public override bool Equals(object obj)
    {
        if (ReferenceEquals(null, obj)) return false;
        if (ReferenceEquals(this, obj)) return true;
        if (obj.GetType() != this.GetType()) return false;
        return Equals((Unit)obj);
    }

    public override int GetHashCode()
    {
        unchecked
        {
            return ((Type != null ? Type.GetHashCode() : 0) * 397) ^ (Uid != null ? Uid.GetHashCode() : 0);
        }
    }

    public override string ToString()
    {
        return "Unit{" +
               "type='" + Type + '\'' +
               ", uid=" + Uid +
               '}';
    }

    #endregion
}