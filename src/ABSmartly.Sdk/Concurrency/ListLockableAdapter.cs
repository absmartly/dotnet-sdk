using System;
using System.Collections.Generic;

namespace ABSmartly.Concurrency;

public class ListLockableAdapter<T> : List<T>
{
    private readonly ILockableCollectionSlimLock _lockSlim;

    public ListLockableAdapter(ILockableCollectionSlimLock lockSlim)
    {
        _lockSlim = lockSlim ?? throw new ArgumentNullException(nameof(lockSlim), "Lock is required");
    }

    public ListLockableAdapter(ILockableCollectionSlimLock lockSlim, IEnumerable<T> collection) : base(collection)
    {
        _lockSlim = lockSlim ?? throw new ArgumentNullException(nameof(lockSlim), "Lock is required");
    }

    public void ConcurrentAdd(T value)
    {
        try
        {
            _lockSlim.EnterWriteLock();
            Add(value);
        }
        finally
        {
            _lockSlim.ExitWriteLock();
        }
    }
}