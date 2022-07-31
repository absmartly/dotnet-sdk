﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using ABSmartly.Utils;

namespace ABSmartly.Internal;

public class Concurrency
{
    public static V ComputeIfAbsentRW<K, V>(ABLock rwlock, IDictionary<K, V> map, K key, Func<K, V> computer) 
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

    public static V GetRW<K, V>(ABLock rwlock, IDictionary<K, V> map, K key) 
    {

        try
        {
            rwlock.EnterReadLock();

            return map.ContainsKey(key) ? map[key] : default(V);
        }
        finally 
        {
            rwlock.ExitReadLock();
        }
    }

    public static V PutRW<K, V>(ABLock rwlock, IDictionary<K, V> map, K key, V value) 
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

    public static void AddRW<V>(ABLock rwlock, List<V> list, V value) {
  
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