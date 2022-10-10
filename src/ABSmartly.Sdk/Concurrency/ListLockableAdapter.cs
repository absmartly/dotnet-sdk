using System;
using System.Collections.Generic;
using System.Threading;

namespace ABSmartly.Concurrency;

public class ListLockableAdapter<T> : List<T>
{
    private readonly ReaderWriterLockSlim _lockSlim;

    public ListLockableAdapter(ReaderWriterLockSlim lockSlim)
    {
        _lockSlim = lockSlim;
    }

    public ListLockableAdapter(ReaderWriterLockSlim lockSlim, IEnumerable<T> collection) : base(collection)
    {
        _lockSlim = lockSlim;
    }

    private void EnsureLock()
    {
        if (_lockSlim == null)
            throw new InvalidOperationException(
                "Cannot perform synchronized operation because lock is missing. Use constructor with ReaderWriterLockSlim parameter.");
    }

    public void ConcurrentAdd(T value)
    {
        EnsureLock();

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