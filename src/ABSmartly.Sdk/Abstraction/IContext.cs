using System.Collections.Generic;
using System.Threading.Tasks;
using ABSmartly.Models;

namespace ABSmartly;

public interface IContext
{
    int PendingCount { get; }
    bool IsReady();
    bool IsFailed();
    bool IsClosed();
    bool IsClosing();
    string[] GetExperiments();
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
    Dictionary<string, string> GetVariableKeys();
    object GetVariableValue(string key, object defaultValue);
    object PeekVariableValue(string key, object defaultValue);
    void Publish();
    Task PublishAsync();
    void Refresh();
    Task RefreshAsync();
    void Track(string goalName, Dictionary<string, object> properties);
}