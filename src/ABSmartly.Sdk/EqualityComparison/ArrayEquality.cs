using System;

namespace ABSmartly.EqualityComparison;

public static class ArrayEquality
{
    public static bool Equals(Array x, Array y)
    {
        if (ReferenceEquals(x, y)) return true;
        if (ReferenceEquals(x, null)) return false;
        if (ReferenceEquals(y, null)) return false;

        if (x.Length != y.Length) return false;

        for (var i = 0; i < x.Length; i++)
        {
            var v1 = x.GetValue(i);
            var v2 = y.GetValue(i);

            if (!Equals(v1, v2)) return false;
        }

        return true;
    }
}