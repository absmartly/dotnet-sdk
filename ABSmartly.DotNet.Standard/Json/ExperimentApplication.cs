namespace ABSmartly.Json;

//@JsonInclude(JsonInclude.Include.NON_NULL)
//@JsonIgnoreProperties(ignoreUnknown = true)
public class ExperimentApplication
{
    public ExperimentApplication(string name) 
    {
        Name = name;
    }

    public string Name { get; set; }

    #region Overrides - Equality / Hash / ToString

    protected bool Equals(ExperimentApplication other)
    {
        return Name == other.Name;
    }

    public override bool Equals(object obj)
    {
        if (ReferenceEquals(null, obj)) return false;
        if (ReferenceEquals(this, obj)) return true;
        if (obj.GetType() != this.GetType()) return false;
        return Equals((ExperimentApplication)obj);
    }

    public override int GetHashCode()
    {
        return (Name != null ? Name.GetHashCode() : 0);
    }

    public override string ToString()
    {
        return "ExperimentApplication{" +
               "name='" + Name + '\'' +
               '}';
    }

    #endregion
}