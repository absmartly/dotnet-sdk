using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ABSmartly;

/// <summary>
/// Extends <see cref="IContext"/> with the members added in 1.4.0.
/// </summary>
/// <remarks>
/// <see cref="IContext"/> is frozen at its 1.3.0 shape so that existing implementations
/// (decorators, test fakes) keep compiling and loading. New context members are added here
/// instead. <see cref="Context"/> implements this interface; the <c>CreateContext*</c> methods
/// keep returning <see cref="IContext"/> for binary compatibility, so cast the result
/// (<c>(IContextV2)sdk.CreateContext(config)</c>) or use <see cref="ABsmartly.Sdk.CreatePendingContext"/>,
/// which returns <see cref="IContextV2"/>.
/// </remarks>
public interface IContextV2 : IContext
{
    /// <summary>
    /// The error that made the initial data load fail, or null when the load succeeded
    /// (or failed without an exception).
    /// </summary>
    Exception ReadyError { get; }

    /// <summary>
    /// Waits for the initial data load to settle. Completes normally on a failed load too:
    /// check <see cref="IContext.IsFailed"/> and <see cref="ReadyError"/> afterwards.
    /// </summary>
    /// <returns>This context.</returns>
    Task<IContextV2> WaitUntilReadyAsync();

    /// <summary>
    /// Blocks until the initial data load settles. See <see cref="WaitUntilReadyAsync"/>.
    /// </summary>
    /// <returns>This context.</returns>
    IContextV2 WaitUntilReady();

    /// <summary>Same as <see cref="IContext.IsClosed"/>.</summary>
    bool IsFinalized { get; }

    /// <summary>Same as <see cref="IContext.IsClosing"/>.</summary>
    bool IsFinalizing { get; }

    /// <summary>Names of the experiments available in the context.</summary>
    string[] Experiments { get; }

    /// <summary>Copy of the units set on the context (type => UID).</summary>
    Dictionary<string, string> Units { get; }

    [Obsolete("Use the Units property instead.")]
    Dictionary<string, string> GetUnits();

    /// <summary>Last value set for the attribute, or null.</summary>
    object GetAttribute(string name);

    /// <summary>Copy of the attributes set on the context (name => last value).</summary>
    Dictionary<string, object> Attributes { get; }

    [Obsolete("Use the Attributes property instead.")]
    Dictionary<string, object> GetAttributes();

    /// <summary>Every variable key mapped to all experiments that define it.</summary>
    Dictionary<string, List<string>> VariableKeys { get; }

    [Obsolete("Use the VariableKeys property instead.")]
    Dictionary<string, List<string>> GetVariableExperimentKeys();

    /// <summary>Synchronously publishes pending events and closes the context.</summary>
    void Close();

    /// <summary>
    /// Publishes pending events and seals the context; units, attributes and experiments
    /// stay readable afterwards.
    /// </summary>
    Task FinalizeAsync();
}
