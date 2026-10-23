using System;
using System.Threading.Tasks;
using ABSmartly.Models;

namespace ABSmartly;

/// <summary>
/// Obsolete: Use <see cref="IContextPublisher"/> instead.
/// </summary>
[Obsolete("Use IContextPublisher instead.")]
public interface IContextEventHandler : IContextPublisher
{
}
