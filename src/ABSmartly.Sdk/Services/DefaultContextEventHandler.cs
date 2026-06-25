using System;
using System.Threading.Tasks;
using ABSmartly.Models;

namespace ABSmartly.Services;

/// <summary>
/// Obsolete: Use <see cref="DefaultContextPublisher"/> instead.
/// </summary>
[Obsolete("Use DefaultContextPublisher instead.")]
public class DefaultContextEventHandler : DefaultContextPublisher, IContextEventHandler
{
    public DefaultContextEventHandler(IABSmartlyServiceClient client) : base(client)
    {
    }
}
