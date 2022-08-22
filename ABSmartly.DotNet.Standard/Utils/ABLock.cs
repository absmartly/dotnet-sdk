using System;
using System.Threading;

namespace ABSmartlySdk.Utils;

public class ABLock : IDisposable
{
    private readonly ReaderWriterLockSlim _lockSlim;

    public ABLock()
    {
        _lockSlim = new ReaderWriterLockSlim();
    }
    public ABLock(ReaderWriterLockSlim lockSlim)
    {
        _lockSlim = lockSlim;
    }

    public virtual void EnterWriteLock()
    {
        _lockSlim.EnterWriteLock();
    }

    public virtual void EnterReadLock()
    {
        _lockSlim.EnterReadLock();
    }

    public virtual void ExitWriteLock()
    {
        _lockSlim.ExitWriteLock();
    }

    public virtual void ExitReadLock()
    {
        _lockSlim.ExitReadLock();
    }

    public void Dispose()
    {
        _lockSlim?.Dispose();
    }
}