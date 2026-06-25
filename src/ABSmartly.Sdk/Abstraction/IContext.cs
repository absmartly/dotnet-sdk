using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ABSmartly.Models;

namespace ABSmartly;

public interface IContext
{
    int PendingCount { get; }

    bool IsReady();

    bool IsFailed();

#if !NETSTANDARD2_0
    Exception ReadyError => null;
#else
    Exception ReadyError { get; }
#endif

    bool IsClosed();

    bool IsClosing();

#if !NETSTANDARD2_0
    bool IsFinalized => IsClosed();
    bool IsFinalizing => IsClosing();
#else
    bool IsFinalized { get; }
    bool IsFinalizing { get; }
#endif

#if !NETSTANDARD2_0
    string[] Experiments => Array.Empty<string>();

    [Obsolete("Use the Experiments property instead.")]
    string[] GetExperiments() => Experiments;
#else
    string[] Experiments { get; }

    [Obsolete("Use the Experiments property instead.")]
    string[] GetExperiments();
#endif

    ContextData GetContextData();

    void SetAttribute(string name, object value);

    void SetAttributes(Dictionary<string, object> attributes);

    void SetCustomAssignment(string experimentName, int variant);

    int? GetCustomAssignment(string experimentName);

    void SetCustomAssignments(Dictionary<string, int> customAssignments);

    void SetOverride(string experimentName, int variant);

    int? GetOverride(string experimentName);

    void SetOverrides(Dictionary<string, int> overrides);

    int GetTreatment(string experimentName);

    int PeekTreatment(string experimentName);

    void SetUnit(string unitType, string uid);

    void SetUnits(Dictionary<string, string> units);

#if !NETSTANDARD2_0
    Dictionary<string, string> Units => new Dictionary<string, string>();

    [Obsolete("Use the Units property instead.")]
    Dictionary<string, string> GetUnits() => Units;
#else
    Dictionary<string, string> Units { get; }

    [Obsolete("Use the Units property instead.")]
    Dictionary<string, string> GetUnits();
#endif

    object GetAttribute(string name);

#if !NETSTANDARD2_0
    Dictionary<string, object> Attributes => new Dictionary<string, object>();

    [Obsolete("Use the Attributes property instead.")]
    Dictionary<string, object> GetAttributes() => Attributes;
#else
    Dictionary<string, object> Attributes { get; }

    [Obsolete("Use the Attributes property instead.")]
    Dictionary<string, object> GetAttributes();
#endif

#if !NETSTANDARD2_0
    [Obsolete("Use the VariableKeys property instead.")]
    Dictionary<string, string> GetVariableKeys()
    {
        var full = VariableKeys;
        var result = new Dictionary<string, string>(full.Count);
        foreach (var kv in full)
            if (kv.Value.Count > 0) result[kv.Key] = kv.Value[0];
        return result;
    }

    Dictionary<string, List<string>> VariableKeys => new Dictionary<string, List<string>>();

    [Obsolete("Use the VariableKeys property instead.")]
    Dictionary<string, List<string>> GetVariableExperimentKeys() => VariableKeys;
#else
    [Obsolete("Use the VariableKeys property instead.")]
    Dictionary<string, string> GetVariableKeys();

    Dictionary<string, List<string>> VariableKeys { get; }

    [Obsolete("Use the VariableKeys property instead.")]
    Dictionary<string, List<string>> GetVariableExperimentKeys();
#endif

    object GetVariableValue(string key, object defaultValue);

    object PeekVariableValue(string key, object defaultValue);

    void Publish();

    Task PublishAsync();

    void Refresh();

    Task RefreshAsync();

    void Track(string goalName, Dictionary<string, object> properties);

#if !NETSTANDARD2_0
    void Close() { }
#else
    void Close();
#endif
}
