using ABSmartly.Interfaces;
using System.Collections.Generic;
using ABSmartly.DefaultServiceImplementations;

namespace ABSmartly;

public class ContextConfig
{
    private readonly Dictionary<string, string> _units;
    private readonly Dictionary<string, object> _attributes;
    private readonly Dictionary<string, int> _overrides;
    private readonly Dictionary<string, int> _customAssigmnents;

    private long _publishDelay;
    private long _refreshInterval;

    public IContextEventLogger EventLogger { get; }

    #region Lifecycle

    public ContextConfig(IContextEventLogger contextEventLogger = null)
    {
        _units = new Dictionary<string, string>();
        _attributes = new Dictionary<string, object>();
        _overrides = new Dictionary<string, int>();
        _customAssigmnents = new Dictionary<string, int>();

        _publishDelay = 100;
        _refreshInterval = 0;

        EventLogger = contextEventLogger ?? new DefaultContextEventLogger();
    }

    //public static ContextConfig Create(IContextEventLogger contextEventLogger = null)
    //{
    //    return new ContextConfig(contextEventLogger);
    //}

    #endregion


    #region Units

    public ContextConfig SetUnit(string unitType, string uid) 
    {
        _units.Add(unitType, uid);
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
        return _units.ContainsKey(unitType) ? _units[unitType] : string.Empty;
    }    

	public Dictionary<string, string> GetUnits() 
    {
		return _units;
	}

    #endregion

    #region Attributes

	public ContextConfig SetAttribute(string name, object value) 
    {
        _attributes.Add(name, value);
		return this;
	}

	public ContextConfig SetAttributes(Dictionary<string, object> attributes) 
    {
        foreach (var kvp in attributes)
            _attributes.Add(kvp.Key, kvp.Value);
        

		return this;
	}

	public object GetAttribute(string name)
    {
        return _attributes.ContainsKey(name) ? _attributes[name] : null;
    }

	public Dictionary<string, object> GetAttributes() 
    {
		return _attributes;
	}    

    #endregion

    #region Overrides

	public ContextConfig SetOverride(string experimentName, int variant) 
    {
        _overrides.Add(experimentName, variant);
		return this;
	}

	public ContextConfig SetOverrides(Dictionary<string, int> overrides) 
    {
        foreach (var kvp in overrides)
            _overrides.Add(kvp.Key, kvp.Value);

        return this;
	}

	public object GetOverride(string experimentName)
    {
        if (_overrides.ContainsKey(experimentName))
            return _overrides[experimentName];

        return null;
    }

	public Dictionary<string, int> GetOverrides() 
    {
		return _overrides;
	}    

    #endregion

    #region CustomAssignments

	public ContextConfig SetCustomAssignment(string experimentName, int variant) 
    {
        _customAssigmnents.Add(experimentName, variant);
		return this;
	}

	public ContextConfig SetCustomAssignments(Dictionary<string, int> customAssignments) 
    {
        foreach (var kvp in customAssignments)
            _customAssigmnents.Add(kvp.Key, kvp.Value);

        return this;
	}

	public object GetCustomAssignment(string experimentName)
    {
        return _customAssigmnents.ContainsKey(experimentName) ? _customAssigmnents[experimentName] : null;
    }

	public Dictionary<string, int> GetCustomAssignments() 
    {
		return _customAssigmnents;
	}    

    #endregion

    #region PublishDelay

	public ContextConfig SetPublishDelay(long delay) 
    {
		_publishDelay = delay;
		return this;
	}

	public long GetPublishDelay() 
    {
		return _publishDelay;
	}

    #endregion

    #region RefreshInterval

	public ContextConfig SetRefreshInterval(long interval) 
    {
		_refreshInterval = interval;
		return this;
	}

	public long GetRefreshInterval() 
    {
		return _refreshInterval;
	}    

    #endregion
}