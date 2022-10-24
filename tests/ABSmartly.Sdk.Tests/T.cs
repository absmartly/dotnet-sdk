namespace ABSmartly.Sdk.Tests;

public static class T
{
    public static List<TItem> ListOf<TItem>(params TItem?[] values) => new(values);

    public static Dictionary<TKey, TValue> MapOf<TKey, TValue>(TKey k1, TValue v1) where TKey : notnull => new()
    {
        [k1] = v1
    };
    
    public static Dictionary<TKey, TValue> MapOf<TKey, TValue>(TKey k1, TValue v1, TKey k2, TValue v2) where TKey : notnull => new()
    {
        [k1] = v1,
        [k2] = v2
    };
    
    public static Dictionary<TKey, TValue> MapOf<TKey, TValue>(TKey k1, TValue v1, TKey k2, TValue v2, TKey k3, TValue v3) where TKey : notnull => new()
    {
        [k1] = v1,
        [k2] = v2,
        [k3] = v3
    };
    
    public static Dictionary<TKey, TValue> MapOf<TKey, TValue>(TKey k1, TValue v1, TKey k2, TValue v2, TKey k3, TValue v3, TKey k4, TValue v4) where TKey : notnull => new()
    {
        [k1] = v1,
        [k2] = v2,
        [k3] = v3,
        [k4] = v4
    };
    
    public static Dictionary<TKey, TValue> MapOf<TKey, TValue>(TKey k1, TValue v1, TKey k2, TValue v2, TKey k3, TValue v3, TKey k4, TValue v4, TKey k5, TValue v5) where TKey : notnull => new()
    {
        [k1] = v1,
        [k2] = v2,
        [k3] = v3,
        [k4] = v4,
        [k5] = v5
    };
    
    public static Dictionary<TKey, TValue> MapOf<TKey, TValue>(TKey k1, TValue v1, TKey k2, TValue v2, TKey k3, TValue v3, TKey k4, TValue v4, TKey k5, TValue v5, TKey k6, TValue v6) where TKey : notnull => new()
    {
        [k1] = v1,
        [k2] = v2,
        [k3] = v3,
        [k4] = v4,
        [k5] = v5,
        [k6] = v6
    };
    
    public static Dictionary<TKey, TValue> MapOf<TKey, TValue>(TKey k1, TValue v1, TKey k2, TValue v2, TKey k3, TValue v3, TKey k4, TValue v4, TKey k5, TValue v5, TKey k6, TValue v6, TKey k7, TValue v7) where TKey : notnull => new()
    {
        [k1] = v1,
        [k2] = v2,
        [k3] = v3,
        [k4] = v4,
        [k5] = v5,
        [k6] = v6,
        [k7] = v7
    };
}