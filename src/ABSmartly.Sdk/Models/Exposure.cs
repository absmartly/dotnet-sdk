namespace ABSmartlySdk.Json;

//@JsonInclude(JsonInclude.Include.ALWAYS)
//@JsonIgnoreProperties(ignoreUnknown = true)
public sealed record Exposure(int Id, string Name, string Unit, int Variant, long ExposedAt, bool Assigned,
    bool Eligible,
    bool Overridden, bool FullOn, bool Custom, bool AudienceMismatch)
{
    public int Id { get; } = Id;
    public string Name { get; } = Name;
    public string Unit { get; } = Unit;
    public int Variant { get; } = Variant;
    public long ExposedAt { get; } = ExposedAt;
    public bool Assigned { get; } = Assigned;
    public bool Eligible { get; } = Eligible;
    public bool Overridden { get; } = Overridden;
    public bool FullOn { get; } = FullOn;
    public bool Custom { get; } = Custom;
    public bool AudienceMismatch { get; set; } = AudienceMismatch;


    #region Overrides - Equality / Hash / ToString

    public bool Equals(Exposure other)
    {
        return Id == other.Id && Name == other.Name && Unit == other.Unit && Variant == other.Variant &&
               ExposedAt == other.ExposedAt && Assigned == other.Assigned && Eligible == other.Eligible &&
               Overridden == other.Overridden && FullOn == other.FullOn && Custom == other.Custom &&
               AudienceMismatch == other.AudienceMismatch;
    }

    #endregion

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