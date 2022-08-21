using System;
using System.Collections.Generic;
using ABSmartly.Utils;

namespace ABSmartly.Internal;

public class Concurrency
{
    public static V ComputeIfAbsentRW<K, V>(ABLock rwlock, IDictionary<K, V> dictionary, K key, Func<K, V> computer) 
    {
        try 
        {
            rwlock.EnterReadLock();

            if (dictionary.ContainsKey(key))
                return dictionary[key];
        }
        finally 
        {
            rwlock.ExitReadLock();
        }

        try
        {
            rwlock.EnterWriteLock();

            // double check
            if (dictionary.ContainsKey(key))
                return dictionary[key];

            V newValue = computer.Invoke(key);
            dictionary.Add(key, newValue);
            return newValue;
        }
        finally 
        {
            rwlock.ExitWriteLock();
        }
    }

    public static V GetRW<K, V>(ABLock rwlock, IDictionary<K, V> dictionary, K key) 
    {

        try
        {
            rwlock.EnterReadLock();

            if (!dictionary.ContainsKey(key))
                return default;

            return dictionary[key];
        }
        finally 
        {
            rwlock.ExitReadLock();
        }
    }

    public static V PutRW<K, V>(ABLock rwlock, IDictionary<K, V> dictionary, K key, V value) 
    {
        try 
        {
            rwlock.EnterWriteLock();

            dictionary.Add(key, value);
            return dictionary[key];
        }
        finally 
        {
            rwlock.ExitWriteLock();
        }
    }

    public static void AddRW<V>(ABLock rwlock, IList<V> list, V value) {
  
        try 
        {
            rwlock.EnterWriteLock();
            list.Add(value);
        }
        finally 
        {
            rwlock.ExitWriteLock();
        }
    }
}