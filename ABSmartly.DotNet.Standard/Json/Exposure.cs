namespace ABSmartly.Json;

//@JsonInclude(JsonInclude.Include.ALWAYS)
//@JsonIgnoreProperties(ignoreUnknown = true)
public class Exposure
{
    public Exposure()
    {
        
    }

    public Exposure(int id, string name, string unit, int variant, long exposedAt, bool assigned, bool eligible,
        bool overridden, bool fullOn, bool custom, bool audienceMismatch)
    {
        Id = id;
        Name = name;
        Unit = unit;
        Variant = variant;
        ExposedAt = exposedAt;
        Assigned = assigned;
        Eligible = eligible;
        Overridden = overridden;
        FullOn = fullOn;
        Custom = custom;
        AudienceMismatch = audienceMismatch;
    }

    public int Id { get; set; }
    public string Name { get; set; }
    public string Unit { get; set; }
    public int Variant { get; set; }
    public long ExposedAt { get; set; }
    public bool Assigned { get; set; }
    public bool Eligible { get; set; }
    public bool Overridden { get; set; }
    public bool FullOn { get; set; }
    public bool Custom { get; set; }
    public bool AudienceMismatch { get; set; }



    protected bool Equals(Exposure other)
    {
        return Id == other.Id && Name == other.Name && Unit == other.Unit && Variant == other.Variant &&
               ExposedAt == other.ExposedAt && Assigned == other.Assigned && Eligible == other.Eligible &&
               Overridden == other.Overridden && FullOn == other.FullOn && Custom == other.Custom &&
               AudienceMismatch == other.AudienceMismatch;
    }

    public override bool Equals(object obj)
    {
        if (ReferenceEquals(null, obj)) return false;
        if (ReferenceEquals(this, obj)) return true;
        if (obj.GetType() != this.GetType()) return false;
        return Equals((Exposure)obj);
    }

    public override int GetHashCode()
    {
        unchecked
        {
            var hashCode = Id;
            hashCode = (hashCode * 397) ^ (Name != null ? Name.GetHashCode() : 0);
            hashCode = (hashCode * 397) ^ (Unit != null ? Unit.GetHashCode() : 0);
            hashCode = (hashCode * 397) ^ Variant;
            hashCode = (hashCode * 397) ^ ExposedAt.GetHashCode();
            hashCode = (hashCode * 397) ^ Assigned.GetHashCode();
            hashCode = (hashCode * 397) ^ Eligible.GetHashCode();
            hashCode = (hashCode * 397) ^ Overridden.GetHashCode();
            hashCode = (hashCode * 397) ^ FullOn.GetHashCode();
            hashCode = (hashCode * 397) ^ Custom.GetHashCode();
            hashCode = (hashCode * 397) ^ AudienceMismatch.GetHashCode();
            return hashCode;
        }
    }

    public override string ToString()
    {
        return "Exposure{" +
               "id=" + Id +
               ", name='" + Name + '\'' +
               ", unit='" + Unit + '\'' +
               ", variant=" + Variant +
               ", exposedAt=" + ExposedAt +
               ", assigned=" + Assigned +
               ", eligible=" + Eligible +
               ", overridden=" + Overridden +
               ", fullOn=" + FullOn +
               ", custom=" + Custom +
               ", audienceMismatch=" + AudienceMismatch +
               '}';
    }
}