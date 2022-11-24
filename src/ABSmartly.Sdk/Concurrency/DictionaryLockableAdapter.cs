using System;
using System.Collections.Generic;

namespace ABSmartly.Concurrency;

public class DictionaryLockableAdapter<TKey, TValue> : Dictionary<TKey, TValue>
{
    private readonly ILockableCollectionSlimLock _lockSlim;

    public DictionaryLockableAdapter(ILockableCollectionSlimLock lockSlim)
    {
        _lockSlim = lockSlim ?? throw new ArgumentNullException(nameof(lockSlim), "Lock is required");
    }

    public DictionaryLockableAdapter(ILockableCollectionSlimLock lockSlim, IDictionary<TKey, TValue> dictionary) :
        base(dictionary)
    {
        _lockSlim = lockSlim ?? throw new ArgumentNullException(nameof(lockSlim), "Lock is required");
    }

    public DictionaryLockableAdapter(ILockableCollectionSlimLock lockSlim, IDictionary<TKey, TValue> dictionary,
        IEqualityComparer<TKey> comparer) : base(dictionary, comparer)
    {
        _lockSlim = lockSlim ?? throw new ArgumentNullException(nameof(lockSlim), "Lock is required");
    }

    public DictionaryLockableAdapter(ILockableCollectionSlimLock lockSlim, IEqualityComparer<TKey> comparer) :
        base(comparer)
    {
        _lockSlim = lockSlim ?? throw new ArgumentNullException(nameof(lockSlim), "Lock is required");
    }

    public DictionaryLockableAdapter(ILockableCollectionSlimLock lockSlim, int capacity) : base(capacity)
    {
        _lockSlim = lockSlim ?? throw new ArgumentNullException(nameof(lockSlim), "Lock is required");
    }

    public TValue ConcurrentGetOrAdd(TKey key, Func<TKey, TValue> computeFn)
    {
        try
        {
            _lockSlim.EnterReadLock();

            if (ContainsKey(key))
                return this[key];
        }
        finally
        {
            _lockSlim.ExitReadLock();
        }

        try
        {
            _lockSlim.EnterWriteLock();

            if (ContainsKey(key))
                return this[key];

            var newValue = computeFn(key);
            Add(key, newValue);
            return newValue;
        }
        finally
        {
            _lockSlim.ExitWriteLock();
        }
    }

    public TValue ConcurrentGetValueOrDefault(TKey key)
    {
        try
        {
            _lockSlim.EnterReadLock();

            return ContainsKey(key) ? this[key] : default;
        }
        finally
        {
            _lockSlim.ExitReadLock();
        }
    }

    public TValue ConcurrentSet(TKey key, TValue value)
    {
        try
        {
            _lockSlim.EnterWriteLock();

            this[key] = value;
            return this[key];
        }
        finally
        {
            _lockSlim.ExitWriteLock();
        }
    }
}