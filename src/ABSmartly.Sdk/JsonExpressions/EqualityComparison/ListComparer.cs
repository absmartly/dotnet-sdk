﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace ABSmartly.JsonExpressions.EqualityComparison;

public class ListComparer: IEqualityComparer<List<object>>, IEqualityComparer
{
    private readonly Func<object, IEqualityComparer> _valueComparerSelector;

    public ListComparer(Func<object, IEqualityComparer> valueComparerSelector)
    {
        _valueComparerSelector = valueComparerSelector;
    }
    
    public bool Equals(List<object> x, List<object> y)
    {
        if (ReferenceEquals(x, y)) return true;
        if (ReferenceEquals(x, null)) return false;
        if (ReferenceEquals(y, null)) return false;
        
        if (x.Count != y.Count) return false;

        var matched = x.Zip(y, (i1, i2) => (kv1: i1, kv2: i2));

        foreach (var (i1, i2) in matched)
        {
            if (!_valueComparerSelector(i1).Equals(i1, i2))
                return false;
        }
        
        return true;
    }

    bool IEqualityComparer.Equals(object x, object y)
    {
        if (ReferenceEquals(x, y)) return true;
        if (ReferenceEquals(x, null)) return false;
        if (ReferenceEquals(y, null)) return false;

        if (x.GetType() != y.GetType()) return false;
        
        return Equals((List<object>)x, (List<object>)y);
    }
    
    public int GetHashCode(List<object> obj)
    {
        throw new NotImplementedException();
    }

    public int GetHashCode(object obj)
    {
        throw new NotImplementedException();
    }
}