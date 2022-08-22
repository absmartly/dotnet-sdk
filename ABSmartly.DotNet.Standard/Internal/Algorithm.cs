using System;
using System.Collections.Generic;

namespace ABSmartlySdk.Internal;

public class Algorithm
{
    public static R[] MapSetToArray<T, R>(HashSet<T> set, R[] array, Func<T, R> mapper) 
    {
        var size = set.Count;

        if (array.Length < size)
            array = new R[size];
        
        if (array.Length > size)
            array[size] = default(R);

        var index = 0;
        foreach (var value in set)
            array[index++] = mapper.Invoke(value);

        return array;
    }
}