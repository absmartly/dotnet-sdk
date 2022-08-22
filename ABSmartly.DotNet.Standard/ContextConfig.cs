using System.Collections.Generic;

namespace ABSmartlySdk;

public class ContextConfig
{
    #region Lifecycle

    public ContextConfig()
    {
        Units = new Dictionary<string, string>();
        Attributes = new Dictionary<string, object>();
        Overrides = new Dictionary<string, int>();
        CustomAssigmnents = new Dictionary<string, int>();

        // Assign default values
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

	public Dictionary<string, string> GetUnits() 
    {
		return Units;
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

	//public object GetAttribute(string name)
 //   {
 //       if (!Attributes.ContainsKey(name))
 //           return null;

 //       return Attributes[name];
 //   }

	//public Dictionary<string, object> GetAttributes() 
 //   {
	//	return Attributes;
	//}    

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

    //public Dictionary<string, int> GetOverrides() 
    //   {
    //	return Overrides;
    //}    

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

	//public Dictionary<string, int> GetCustomAssignments() 
 //   {
	//	return CustomAssigmnents;
	//}    

    #endregion

    #region PublishDelay

	//public ContextConfig SetPublishDelay(long delay) 
 //   {
	//	PublishDelay = delay;
	//	return this;
	//}

	//public long GetPublishDelay() 
 //   {
	//	return PublishDelay;
	//}

    #endregion

    #region RefreshInterval

	//public ContextConfig SetRefreshInterval(long interval) 
 //   {
	//	RefreshInterval = interval;
	//	return this;
	//}

	//public long GetRefreshInterval() 
 //   {
	//	return RefreshInterval;
	//}    

    #endregion
}