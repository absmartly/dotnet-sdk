using System.Threading;

namespace ABSmartly.Concurrency;

internal class LockableCollectionSlimLock : ILockableCollectionSlimLock
{
    private readonly ReaderWriterLockSlim _lockSlim;

    public LockableCollectionSlimLock(ReaderWriterLockSlim lockSlim)
    {
        _lockSlim = lockSlim;
    }

    public void EnterReadLock()
    {
        _lockSlim.EnterReadLock();
    }

    public void ExitReadLock()
    {
        _lockSlim.ExitReadLock();
    }

    public void EnterWriteLock()
    {
        _lockSlim.EnterWriteLock();
    }

    public void ExitWriteLock()
    {
        _lockSlim.ExitWriteLock();
    }
}