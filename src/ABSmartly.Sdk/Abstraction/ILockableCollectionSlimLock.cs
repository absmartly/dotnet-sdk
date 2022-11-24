namespace ABSmartly;

public interface ILockableCollectionSlimLock
{
    void EnterReadLock();
    void ExitReadLock();
    void EnterWriteLock();
    void ExitWriteLock();
}