namespace ABSmartly.Sdk.Tests;

public static class T
{
    public static List<object> ListOf(params object?[] values) => new(values!);

    public static Dictionary<string, object> MapOf(string k1, object v1)=> new()
    {
        [k1] = v1
    };
    
    public static Dictionary<string, object> MapOf(string k1, object v1, string k2, object v2) => new()
    {
        [k1] = v1,
        [k2] = v2
    };
    
    public static Dictionary<string, object> MapOf(string k1, object v1, string k2, object v2, string k3, object v3) => new()
    {
        [k1] = v1,
        [k2] = v2,
        [k3] = v3
    };
    
    public static Dictionary<string, object> MapOf(string k1, object v1, string k2, object v2, string k3, object v3, string k4, object v4) => new()
    {
        [k1] = v1,
        [k2] = v2,
        [k3] = v3,
        [k4] = v4
    };
    
    public static Dictionary<string, object> MapOf(string k1, object v1, string k2, object v2, string k3, object v3, string k4, object v4, string k5, object v5) => new()
    {
        [k1] = v1,
        [k2] = v2,
        [k3] = v3,
        [k4] = v4,
        [k5] = v5
    };
    
    public static Dictionary<string, object> MapOf(string k1, object v1, string k2, object v2, string k3, object v3, string k4, object v4, string k5, object v5, string k6, object v6) => new()
    {
        [k1] = v1,
        [k2] = v2,
        [k3] = v3,
        [k4] = v4,
        [k5] = v5,
        [k6] = v6
    };
    
    public static Dictionary<string, object> MapOf(string k1, object v1, string k2, object v2, string k3, object v3, string k4, object v4, string k5, object v5, string k6, object v6, string k7, object v7) => new()
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