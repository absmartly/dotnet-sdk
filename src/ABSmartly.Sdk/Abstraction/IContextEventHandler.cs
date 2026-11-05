using System;
using System.Threading.Tasks;
using ABSmartly.Models;

namespace ABSmartly;

/// <summary>
/// Obsolete: Use <see cref="IContextPublisher"/> instead.
/// </summary>
/// <remarks>
/// Kept with its 1.3.0 shape: <see cref="PublishAsync"/> is declared here, so existing
/// implementations (including explicit <c>IContextEventHandler.PublishAsync</c> ones) keep
/// compiling and loading. <see cref="IContextPublisher"/> derives from this interface.
/// </remarks>
[Obsolete("Use IContextPublisher instead.")]
public interface IContextEventHandler
{
    Task PublishAsync(IContext context, PublishEvent publishEvent);
}
