namespace ABSmartly.Json;

public class ExperimentApplication
{
    public ExperimentApplication()
    {

    }

    public ExperimentApplication(string name) 
    {
        Name = name;
    }


    public string Name { get; set; }

    

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



    ////@Override
    //public  boolean equals(object o) {
    //    if (this == o)
    //        return true;
    //    if (o == null || getClass() != o.getClass())
    //        return false;
    //    ExperimentApplication that = (ExperimentApplication) o;
    //    return Objects.equals(name, that.name);
    //}

    ////@Override
    //public int hashCode() {
    //    return Objects.hash(name);
    //}

    ////@Override
    //public string toString() {
    //    return "ExperimentApplication{" +
    //           "name='" + name + '\'' +
    //           '}';
    //}
}