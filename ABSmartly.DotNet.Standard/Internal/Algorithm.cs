using System;
using System.Collections.Generic;

namespace ABSmartly.Internal;

public class Algorithm
{
    public static R[] MapSetToArray<T, R>(HashSet<T> set, R[] array, Func<T, R> mapper) 
    {
        var size = set.Count;

        if (array.Length < size) 
        {
            //array = (R[]) java.lang.reflect.Array.newInstance(array.getClass().getComponentType(), size);
            array = new R[size];
        }

        if (array.Length > size) 
        {
            //array[size] = null;
            array[size] = default(R);
        }

        int index = 0;


        foreach (var value in set) {
            array[index++] = mapper.Invoke(value);
        }
        return array;
    }
}