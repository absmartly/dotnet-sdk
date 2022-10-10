using System;
using System.Collections.Generic;
using System.Threading;

namespace ABSmartly.Concurrency;

public class DictionaryLockableAdapter<TKey, TValue> : Dictionary<TKey, TValue>
{
    private readonly ReaderWriterLockSlim _lockSlim;

    public DictionaryLockableAdapter(ReaderWriterLockSlim lockSlim)
    {
        _lockSlim = lockSlim;
    }

    public DictionaryLockableAdapter(ReaderWriterLockSlim lockSlim, IDictionary<TKey, TValue> dictionary) :
        base(dictionary)
    {
        _lockSlim = lockSlim;
    }

    public DictionaryLockableAdapter(ReaderWriterLockSlim lockSlim, IDictionary<TKey, TValue> dictionary,
        IEqualityComparer<TKey> comparer) : base(dictionary, comparer)
    {
        _lockSlim = lockSlim;
    }

    public DictionaryLockableAdapter(ReaderWriterLockSlim lockSlim, IEqualityComparer<TKey> comparer) : base(comparer)
    {
        _lockSlim = lockSlim;
    }

    public DictionaryLockableAdapter(ReaderWriterLockSlim lockSlim, int capacity) : base(capacity)
    {
        _lockSlim = lockSlim;
    }

    private void EnsureLock()
    {
        if (_lockSlim == null)
            throw new InvalidOperationException(
                "Cannot perform synchronized operation because lock is missing. Use constructor with ReaderWriterLockSlim parameter.");
    }

    public TValue ConcurrentGetOrAdd(TKey key, Func<TValue> computeFn)
    {
        EnsureLock();

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

            var newValue = computeFn();
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
        EnsureLock();

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
        EnsureLock();

        try
        {
            _lockSlim.EnterWriteLock();

            Add(key, value);
            return this[key];
        }
        finally
        {
            _lockSlim.ExitWriteLock();
        }
    }
}