using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace ABSmartly.Extensions;

public static class ConcurrencyExtensions
{
    public static ConfiguredTaskAwaitable ConfigureUnboundContinuation(this Task task)
    {
        return task.ConfigureAwait(false);
    }

    public static ConfiguredTaskAwaitable<T> ConfigureUnboundContinuation<T>(this Task<T> task)
    {
        return task.ConfigureAwait(false);
    }
}