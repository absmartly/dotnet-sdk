using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace ABSmartly.EqualityComparison;

public class DictionaryComparer : IEqualityComparer<IDictionary<string, object>>, IEqualityComparer
{
    private readonly IComparer<string> _keysComparer;
    private readonly Func<object, IEqualityComparer> _valueComparerSelector;

    public DictionaryComparer(Func<object, IEqualityComparer> valueComparerSelector,
        IComparer<string> keysComparer = null)
    {
        _valueComparerSelector = valueComparerSelector;
        _keysComparer = keysComparer ?? Comparer<string>.Default;
    }

#pragma warning disable CS0108, CS0114
    public bool Equals(object x, object y)
#pragma warning restore CS0108, CS0114
    {
        if (ReferenceEquals(x, y)) return true;
        if (ReferenceEquals(x, null)) return false;
        if (ReferenceEquals(y, null)) return false;

        if (x.GetType() != y.GetType()) return false;

        return Equals((Dictionary<string, object>)x, (Dictionary<string, object>)y);
    }

    public int GetHashCode(object obj)
    {
        throw new NotImplementedException();
    }

    public bool Equals(IDictionary<string, object> x, IDictionary<string, object> y)
    {
        if (ReferenceEquals(x, y)) return true;
        if (ReferenceEquals(x, null)) return false;
        if (ReferenceEquals(y, null)) return false;

        if (x.Count != y.Count) return false;

        var xSorted = x.OrderBy(kv => kv.Key, _keysComparer);
        var ySorted = y.OrderBy(kv => kv.Key, _keysComparer);
        var matched = xSorted.Zip(ySorted, (kv1, kv2) => (kv1, kv2));

        foreach (var (kv1, kv2) in matched)
        {
            if (_keysComparer.Compare(kv1.Key, kv2.Key) != 0)
                return false;

            if (!_valueComparerSelector(kv1.Value).Equals(kv1.Value, kv2.Value))
                return false;
        }

        return true;
    }

    public int GetHashCode(IDictionary<string, object> obj)
    {
        throw new NotImplementedException();
    }
}