using System;
using System.Collections.Generic;
using System.Diagnostics;
using ABSmartly.Utils;

namespace ABSmartly.Internal;

public class Concurrency
{
    static public V ComputeIfAbsentRW<K, V>(ABLock rwlock, IDictionary<K, V> map, K key, Func<K, V> computer) 
    {
        try 
        {
            rwlock.EnterReadLock();

            if (map.ContainsKey(key))
                return map[key];
        }
        finally 
        {
            rwlock.ExitReadLock();
        }

        try
        {
            rwlock.EnterWriteLock();

            // double check
            if (map.ContainsKey(key))
                return map[key];

            V newValue = computer.Invoke(key);
            map.Add(key, newValue);
            return newValue;
        }
        finally 
        {
            rwlock.ExitWriteLock();
        }
    }

    static public V GetRW<K, V>(ABLock rwlock, Dictionary<K, V> map, K key) {

        try
        {
            rwlock.EnterReadLock();
            return map[key];
        }
        catch (Exception e)
        {
            Debug.WriteLine(e);
        }
        finally 
        {
            rwlock.ExitReadLock();
        }

        // Todo: ???
        return default;
    }

    static public V PutRW<K, V>(ABLock rwlock, Dictionary<K, V> map, K key, V value) 
    {
        try 
        {
            rwlock.EnterWriteLock();

            map.Add(key, value);
            // Todo: ???
            return map[key];

            //return map.add(key, value);
        }
        catch (Exception e)
        {
            Debug.WriteLine(e);
        }
        finally 
        {
            rwlock.ExitWriteLock();
        }

        // Todo: ???
        return default;
    }

    static public void AddRW<V>(ABLock rwlock, List<V> list, V value) {
  
        try 
        {
            rwlock.EnterWriteLock();
            list.Add(value);
        }
        catch (Exception e)
        {
            Debug.WriteLine(e);
        }
        finally 
        {
            rwlock.ExitWriteLock();
        }
    }
}