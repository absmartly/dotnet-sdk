using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;

namespace ABSmartly.Internal;

public class Concurrency
{
    static public V ComputeIfAbsentRW<K, V>(ReaderWriterLockSlim rwlock, Dictionary<K, V> map, K key, Func<K, V> computer) 
    {
        try 
        {
            rwlock.EnterReadLock();
            V value = map[key];
            if (value != null) 
            {
                return value;
            }
        } 
        catch (Exception e)
        {
            Debug.WriteLine(e);
        }
        finally 
        {
            rwlock.ExitReadLock();
        }

        try 
        {
            rwlock.EnterWriteLock();
            V value = map[key]; // double check
            if (value != null) 
            {
                return value;
            }

            V newValue = computer.Invoke(key);
            map.Add(key, newValue);
            return newValue;
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

    static public V GetRW<K, V>(ReaderWriterLockSlim rwlock, Dictionary<K, V> map, K key) {

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

    static public V PutRW<K, V>(ReaderWriterLockSlim rwlock, Dictionary<K, V> map, K key, V value) 
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

    static public void AddRW<V>(ReaderWriterLockSlim rwlock, List<V> list, V value) {
  
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