using System.Collections.Generic;

namespace ABSmartlySdk;

public class ContextConfig
{
    #region Lifecycle

    public ContextConfig()
    {
        // Assign default values

        Units = new Dictionary<string, string>();
        Attributes = new Dictionary<string, object>();
        Overrides = new Dictionary<string, int>();
        CustomAssigmnents = new Dictionary<string, int>();

        PublishDelay = 100;
        RefreshInterval = 0;
    }

    #endregion

    public Dictionary<string, string> Units { get; }
    public Dictionary<string, object> Attributes { get; }
    public Dictionary<string, int> Overrides { get; }
    public Dictionary<string, int> CustomAssigmnents { get; }

    public long PublishDelay { get; set; }
    public long RefreshInterval { get; set; }

    #region Units

    public ContextConfig SetUnit(string unitType, string uid) 
    {
        Units.Add(unitType, uid);
		return this;
	}

	public ContextConfig SetUnits(Dictionary<string, string> units) 
    {
        foreach (var kvp in units)
        {
            SetUnit(kvp.Key, kvp.Value);
        }

		return this;
	}

	public string GetUnit(string unitType)
    {
        if (!Units.ContainsKey(unitType))
            return string.Empty;

        return Units[unitType];
    }    

    #endregion

    #region Attributes

	public ContextConfig SetAttribute(string name, object value) 
    {
        Attributes.Add(name, value);
		return this;
	}

	public ContextConfig SetAttributes(Dictionary<string, object> attributes) 
    {
        foreach (var kvp in attributes)
            Attributes.Add(kvp.Key, kvp.Value);
        
		return this;
	}

    #endregion

    #region Overrides

	public ContextConfig SetOverride(string experimentName, int variant) 
    {
        Overrides.Add(experimentName, variant);
		return this;
	}

	public ContextConfig SetOverrides(Dictionary<string, int> overrides) 
    {
        foreach (var kvp in overrides)
            Overrides.Add(kvp.Key, kvp.Value);

        return this;
	}

    public object GetOverride(string experimentName)
    {
        if (!Overrides.ContainsKey(experimentName))
            return null;

        return Overrides[experimentName];
    }

    #endregion

    #region CustomAssignments

    public ContextConfig SetCustomAssignment(string experimentName, int variant) 
    {
        CustomAssigmnents.Add(experimentName, variant);
		return this;
	}

	public ContextConfig SetCustomAssignments(Dictionary<string, int> customAssignments) 
    {
        foreach (var kvp in customAssignments)
            CustomAssigmnents.Add(kvp.Key, kvp.Value);

        return this;
	}

	public object GetCustomAssignment(string experimentName)
    {
        if (!CustomAssigmnents.ContainsKey(experimentName))
            return null;

        return CustomAssigmnents[experimentName];
    }

    #endregion
}