using System;
using System.Collections.Generic;

namespace ABSmartly;

public class ContextConfig
{
    public ContextConfig()
    {
        Units = new Dictionary<string, string>();
        Attributes = new Dictionary<string, object>();
        Overrides = new Dictionary<string, int?>();
        CustomAssignments = new Dictionary<string, int?>();

        PublishDelay = TimeSpan.FromMilliseconds(100);
        RefreshInterval = TimeSpan.FromMilliseconds(0);
    }

    public Dictionary<string, string> Units { get; }
    public Dictionary<string, object> Attributes { get; }
    public Dictionary<string, int?> Overrides { get; }
    public Dictionary<string, int?> CustomAssignments { get; }

    public TimeSpan PublishDelay { get; set; }
    public TimeSpan RefreshInterval { get; set; }

    public IContextEventLogger ContextEventLogger { get; set; }

    public ContextConfig SetUnit(string unitType, string uid)
    {
        Units.Add(unitType, uid);
        return this;
    }

    public ContextConfig SetUnits(Dictionary<string, string> units)
    {
        foreach (var kvp in units) SetUnit(kvp.Key, kvp.Value);
        return this;
    }

    public string GetUnit(string unitType) => 
        Units.TryGetValue(unitType, out var u) ? u : null;

    public ContextConfig SetAttribute(string name, object value)
    {
        Attributes.Add(name, value);
        return this;
    }

    public ContextConfig SetAttributes(Dictionary<string, object> attributes)
    {
        foreach (var kvp in attributes) Attributes.Add(kvp.Key, kvp.Value);

        return this;
    }

    public object GetAttribute(string name) => 
        Attributes.TryGetValue(name, out var v) ? v : null;
    
    public ContextConfig SetOverride(string experimentName, int variant)
    {
        Overrides.Add(experimentName, variant);
        return this;
    }

    public ContextConfig SetOverrides(Dictionary<string, int> overrides)
    {
        foreach (var kvp in overrides) Overrides.Add(kvp.Key, kvp.Value);

        return this;
    }

    public object GetOverride(string experimentName) => 
        Overrides.TryGetValue(experimentName, out var e) ? e : null;

    public ContextConfig SetCustomAssignment(string experimentName, int variant)
    {
        CustomAssignments.Add(experimentName, variant);
        return this;
    }

    public ContextConfig SetCustomAssignments(Dictionary<string, int> customAssignments)
    {
        foreach (var kvp in customAssignments) CustomAssignments.Add(kvp.Key, kvp.Value);

        return this;
    }

    public object GetCustomAssignment(string experimentName) =>
        CustomAssignments.TryGetValue(experimentName, out var c) ? c : null;
}