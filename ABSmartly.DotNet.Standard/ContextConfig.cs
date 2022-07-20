using System.Collections.Generic;

namespace ABSmartly;

public class ContextConfig
{
    private Dictionary<string, string> _units;
    private Dictionary<string, object> _attributes;
    private Dictionary<string, int> _overrides;
    private Dictionary<string, int> _cassigmnents;

    private IContextEventLogger _eventLogger;

    private long _publishDelay;
    private long _refreshInterval;

    public ContextConfig()
    {
        _publishDelay = 100;
        _refreshInterval = 0;
    }

    public static ContextConfig Create()
    {
        return new ContextConfig();
    }



    public ContextConfig SetUnit(string unitType, string uid) 
    {
		_units ??= new Dictionary<string, string>();

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
		if (_units.ContainsKey(unitType))
			return _units[unitType];

        return string.Empty;
    }

	public Dictionary<string, string> GetUnits() 
    {
		return _units;
	}

	public ContextConfig SetAttribute(string name, object value) 
    {
		_attributes ??= new Dictionary<string, object>();
		_attributes.Add(name, value);
		return this;
	}

	public ContextConfig SetAttributes(Dictionary<string, object> attributes) 
    {
		_attributes ??= new Dictionary<string, object>( /*attributes.Count()*/);

        foreach (var kvp in attributes)
        {
            _attributes.Add(kvp.Key, kvp.Value);
        }

		return this;
	}

	public object GetAttribute(string name) 
    {
		if (_attributes.ContainsKey(name))
			return _attributes[name];

        return null;
    }

	public Dictionary<string, object> GetAttributes() 
    {
		return _attributes;
	}

	public ContextConfig SetOverride(string experimentName, int variant) 
    {
		_overrides ??= new Dictionary<string, int>();
		_overrides.Add(experimentName, variant);
		return this;
	}

	public ContextConfig SetOverrides(Dictionary<string, int> overrides) 
    {
		_overrides ??= new Dictionary<string, int>( /*overrides.size()*/);

        foreach (var kvp in overrides)
        {
            _overrides.Add(kvp.Key, kvp.Value);
        }

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

	public ContextConfig SetCustomAssignment(string experimentName, int variant) 
    {
		_cassigmnents ??= new Dictionary<string, int>();
		_cassigmnents.Add(experimentName, variant);
		return this;
	}

	public ContextConfig SetCustomAssignments(Dictionary<string, int> customAssignments) 
    {
		_cassigmnents ??= new Dictionary<string, int>( /*customAssignments.size()*/);

        foreach (var kvp in customAssignments)
        {
            _cassigmnents.Add(kvp.Key, kvp.Value);
        }

		return this;
	}

	public object GetCustomAssignment(string experimentName)
    {
        if (_cassigmnents.ContainsKey(experimentName))
            return _cassigmnents[experimentName];

        return null;
    }

	public Dictionary<string, int> GetCustomAssignments() 
    {
		return _cassigmnents;
	}

	public IContextEventLogger GetEventLogger() 
    {
		return _eventLogger;
	}

	public ContextConfig SetEventLogger(IContextEventLogger eventLogger) 
    {
		_eventLogger = eventLogger;
		return this;
	}

	public ContextConfig SetPublishDelay(long delayMs) 
    {
		_publishDelay = delayMs;
		return this;
	}

	public long GetPublishDelay() 
    {
		return _publishDelay;
	}

	public ContextConfig SetRefreshInterval(long intervalMs) 
    {
		_refreshInterval = intervalMs;
		return this;
	}

	public long GetRefreshInterval() 
    {
		return _refreshInterval;
	}
}