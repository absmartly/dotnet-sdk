using System.Collections.Generic;
using System.Threading.Tasks;
using ABSmartly.Models;

namespace ABSmartly;

public interface IContext
{
    /// <summary>
    /// Shows number of pending events that require synchronization
    /// </summary>
    int PendingCount { get; }
    
    /// <summary>
    /// Return true when context is initialized and ready, false otherwise.<br/>
    /// Context is ready even if it's initialization with data from server is failed.
    /// </summary>
    /// <returns>Boolean value indicating context 'ready' status.</returns>
    bool IsReady();
    
    /// <summary>
    /// Returns true when context initialization with data from server failed, false otherwise.
    /// </summary>
    /// <returns>Boolean value indicating context 'ready' status.</returns>
    bool IsFailed();
    
    /// <summary>
    /// Returns true when context all sync is performed and context is closed, false otherwise.
    /// </summary>
    /// <returns>Boolean value indicating that context is closed and no longer available for operations.</returns>
    bool IsClosed();
    
    /// <summary>
    /// Returns true when context is performing closing and doing any outstanding sync and close operations, false otherwise.
    /// </summary>
    /// <returns>Boolean value indicating context is performing closing and no longer available for operations.</returns>
    bool IsClosing();
    
    /// <summary>
    /// Get names of available experiments for the context. 
    /// </summary>
    /// <returns>Array with names of context's experiments.</returns>
    string[] GetExperiments();
    
    /// <summary>
    /// Returns <see cref="ContextData"/> object that represents current data state of the context.
    /// </summary>
    /// <returns>Current data in the context</returns>
    ContextData GetContextData();
    
    /// <summary>
    /// Sets attribute for the context.
    /// </summary>
    /// <param name="name">Name of the attribute</param>
    /// <param name="value">Value of the attribute</param>
    void SetAttribute(string name, object value);
    
    /// <summary>
    /// Sets multiple attributes for the context.
    /// </summary>
    /// <param name="attributes">Dictionary with attributes (name => value) to set for the context</param>
    void SetAttributes(Dictionary<string, object> attributes);
    
    /// <summary>
    /// Sets custom assignment for an experiment.
    /// </summary>
    /// <param name="experimentName">Experiment name</param>
    /// <param name="variant">Custom variant to set for the experiment</param>
    void SetCustomAssignment(string experimentName, int variant);
    
    /// <summary>
    /// Gets custom variant assignment for the experiment, if any.
    /// </summary>
    /// <param name="experimentName">Experiment name</param>
    /// <returns>Custom assigned variant number, null if there's no custom assignment for this experiment.</returns>
    int? GetCustomAssignment(string experimentName);
    
    /// <summary>
    /// Sets multiple custom assignments for experiments.
    /// </summary>
    /// <param name="customAssignments">Dictionary with custom assignments (experiment name => variant)</param>
    void SetCustomAssignments(Dictionary<string, int> customAssignments);
    
    /// <summary>
    /// Sets override for an experiment.
    /// </summary>
    /// <param name="experimentName">Experiment name</param>
    /// <param name="variant">Override variant to set for the experiment</param>
    void SetOverride(string experimentName, int variant);
    
    /// <summary>
    /// Gets override variant assignment for the experiment, if any.
    /// </summary>
    /// <param name="experimentName">Experiment name</param>
    /// <returns>Override variant number, null if there's no override for this experiment.</returns>
    int? GetOverride(string experimentName);
    
    /// <summary>
    /// Sets multiple overrides for experiments.
    /// </summary>
    /// <param name="overrides">Dictionary with override variants (experiment name => variant)</param>
    void SetOverrides(Dictionary<string, int> overrides);
    
    /// <summary>
    /// Get treatment for specified experiment for current context.<br/>
    /// This operation causes exposure tracking.
    /// </summary>
    /// <param name="experimentName">Experiment name.</param>
    /// <returns>Treatment (group) for current context in the specified experiment.</returns>
    int GetTreatment(string experimentName);
    
    /// <summary>
    /// Peek treatment for specified experiment for current context.<br/>
    /// This operation does not cause exposure tracking.
    /// </summary>
    /// <param name="experimentName">Experiment name.</param>
    /// <returns>Treatment (group) for current context in the specified experiment.</returns>
    int PeekTreatment(string experimentName);
    
    /// <summary>
    /// Sets unit on context.
    /// </summary>
    /// <param name="unitType">Unit type</param>
    /// <param name="uid">Unit UID</param>
    void SetUnit(string unitType, string uid);
    
    /// <summary>
    /// Sets multiple units on context.
    /// </summary>
    /// <param name="units">Dictionary with units to set (type => UID)</param>
    void SetUnits(Dictionary<string, string> units);
    
    /// <summary>
    /// Get mapping of every variable key in this context to appropriate experiment.
    /// </summary>
    /// <returns>Dictionary with (variable key => experiment name) mapping.</returns>
    Dictionary<string, string> GetVariableKeys();
    
    /// <summary>
    /// Gets variant assignment for current context and returns variable value if available,
    /// or default value if it's not.<br/>
    /// This operation causes exposure tracking.
    /// </summary>
    /// <param name="key">Variable key</param>
    /// <param name="defaultValue">Default value to return in case variable is missing</param>
    /// <returns>Variable value or default value specified.</returns>
    object GetVariableValue(string key, object defaultValue);
    
    /// <summary>
    /// Peeks variant assignment for current context and returns variable value if available,
    /// or default value if it's not.<br/>
    /// This operation does not cause exposure tracking.
    /// </summary>
    /// <param name="key">Variable key</param>
    /// <param name="defaultValue">Default value to return in case variable is missing</param>
    /// <returns>Variable value or default value specified.</returns>
    object PeekVariableValue(string key, object defaultValue);
    
    /// <summary>
    /// Synchronously publish tracked events in the context, that have not yet been synchronized. 
    /// </summary>
    void Publish();
    
    /// <summary>
    /// Asynchronously publish tracked events in the context, that have not yet been synchronized.
    /// </summary>
    Task PublishAsync();
    
    /// <summary>
    /// Synchronously refreshes context data from servers. 
    /// </summary>
    void Refresh();
    
    /// <summary>
    /// Asynchronously refreshes context data from servers.
    /// </summary>
    /// <returns></returns>
    Task RefreshAsync();
    
    /// <summary>
    /// Tracks achievement into the context and initiates possible publish event by timeout.
    /// </summary>
    /// <param name="goalName">Achievement name</param>
    /// <param name="properties">Additional achievement information</param>
    void Track(string goalName, Dictionary<string, object> properties);
}