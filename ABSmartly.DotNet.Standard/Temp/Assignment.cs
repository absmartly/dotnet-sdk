using System.Collections.Generic;

namespace ABSmartly.Temp;

internal class Assignment
{
    public int Id { get; set; }
    public int Iteration { get; set; }
    public int FullOnVariant { get; set; }
    public string Name { get; set; }
    public string UnitType { get; set; }
    public double[] TrafficSplit { get; set; }
    public int Variant { get; set; }
    public bool Assigned { get; set; }
    public bool Overridden { get; set; }
    public bool Eligible { get; set; }
    public bool FullOn { get; set; }
    public bool Custom { get; set; }

    public bool AudienceMismatch { get; set; }

    public Dictionary<string, object> Variables = new();

    public bool Exposed { get; set; }
    //final AtomicBoolean exposed = new AtomicBoolean(false);
}