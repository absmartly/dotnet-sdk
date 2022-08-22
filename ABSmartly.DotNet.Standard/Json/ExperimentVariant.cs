namespace ABSmartlySdk.Json;

//@JsonInclude(JsonInclude.Include.NON_NULL)
//@JsonIgnoreProperties(ignoreUnknown = true)
public class ExperimentVariant
{
    public ExperimentVariant(string name, string config)
    {
        Name = name;
        Config = config;
    }

    public string Name { get; set; }

    public string Config { get; set; }


    #region Overrides - Equality / Hash / ToString

    protected bool Equals(ExperimentVariant other)
    {
        return Name == other.Name && Config == other.Config;
    }

    public override bool Equals(object obj)
    {
        if (ReferenceEquals(null, obj)) return false;
        if (ReferenceEquals(this, obj)) return true;
        if (obj.GetType() != this.GetType()) return false;
        return Equals((ExperimentVariant)obj);
    }

    public override int GetHashCode()
    {
        unchecked
        {
            return ((Name != null ? Name.GetHashCode() : 0) * 397) ^ (Config != null ? Config.GetHashCode() : 0);
        }
    }

    public override string ToString()
    {
        return "ExperimentVariant{" +
               "name='" + Name + '\'' +
               ", config='" + Config + '\'' +
               '}';
    }

    #endregion
}