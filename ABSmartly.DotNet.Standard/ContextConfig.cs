using System;
using System.Collections.Generic;

namespace ABSmartly;

public class ContextConfig
{
    private Dictionary<string, string> units_;
    private Dictionary<string, object> attributes_;
    private Dictionary<string, int> overrides_;
    private Dictionary<string, int> cassigmnents_;

    private ContextEventLogger eventLogger_;

    private long publishDelay_ = 100;
    private long refreshInterval_ = 0;

    public ContextConfig()
    {
        
    }

    public static ContextConfig Create()
    {
        return new ContextConfig();
    }



    public ContextConfig setUnit(String unitType, String uid) {
		if (units_ == null) {
			units_ = new Dictionary<string, string>();
		}

		units_.Add(unitType, uid);
		return this;
	}

	public ContextConfig setUnits(Dictionary<String, String> units) {
        foreach (var kvp in units)
        {
            setUnit(kvp.Key, kvp.Value);
        }

		return this;
	}

	public String getUnit(String unitType) {
		if (units_.ContainsKey(unitType))
			return units_[unitType];

        return string.Empty;
    }

	public Dictionary<String, String> getUnits() {
		return units_;
	}

	public ContextConfig setAttribute(String name, Object value) {
		if (attributes_ == null) {
			attributes_ = new Dictionary<String, Object>();
		}
		attributes_.Add(name, value);
		return this;
	}

	public ContextConfig setAttributes(Dictionary<String, Object> attributes) {
		if (attributes_ == null) {
			attributes_ = new Dictionary<String, Object>(/*attributes.Count()*/);
		}

        foreach (var kvp in attributes)
        {
            attributes_.Add(kvp.Key, kvp.Value);
        }

		return this;
	}

	public Object getAttribute(String name) {
		if (attributes_.ContainsKey(name))
			return attributes_[name];

        return null;
    }

	public Dictionary<String, Object> getAttributes() {
		return this.attributes_;
	}

	public ContextConfig setOverride(String experimentName, int variant) {
		if (overrides_ == null) {
			overrides_ = new Dictionary<String, int>();
		}
		overrides_.Add(experimentName, variant);
		return this;
	}

	public ContextConfig setOverrides(Dictionary<String, int> overrides) {
		if (overrides_ == null) {
			overrides_ = new Dictionary<String, int>(/*overrides.size()*/);
		}

        foreach (var kvp in overrides)
        {
            overrides_.Add(kvp.Key, kvp.Value);
        }

		return this;
	}

	public Object getOverride(String experimentName)
    {
        if (overrides_.ContainsKey(experimentName))
            return overrides_[experimentName];

        return null;
    }

	public Dictionary<String, int> getOverrides() {
		return this.overrides_;
	}

	public ContextConfig setCustomAssignment(String experimentName, int variant) {
		if (cassigmnents_ == null) {
			cassigmnents_ = new Dictionary<string, int>();
		}
		cassigmnents_.Add(experimentName, variant);
		return this;
	}

	public ContextConfig setCustomAssignments(Dictionary<String, int> customAssignments) {
		if (cassigmnents_ == null) {
			cassigmnents_ = new Dictionary<String, int>(/*customAssignments.size()*/);
		}

        foreach (var kvp in customAssignments)
        {
            cassigmnents_.Add(kvp.Key, kvp.Value);
        }

		return this;
	}

	public Object getCustomAssignment(String experimentName)
    {
        if (cassigmnents_.ContainsKey(experimentName))
            return cassigmnents_[experimentName];

        return null;
    }

	public Dictionary<String, int> getCustomAssignments() {
		return this.cassigmnents_;
	}

	public ContextEventLogger getEventLogger() {
		return this.eventLogger_;
	}

	public ContextConfig setEventLogger(ContextEventLogger eventLogger) {
		this.eventLogger_ = eventLogger;
		return this;
	}

	public ContextConfig setPublishDelay(long delayMs) {
		this.publishDelay_ = delayMs;
		return this;
	}

	public long getPublishDelay() {
		return this.publishDelay_;
	}

	public ContextConfig setRefreshInterval(long intervalMs) {
		this.refreshInterval_ = intervalMs;
		return this;
	}

	public long getRefreshInterval() {
		return this.refreshInterval_;
	}
}