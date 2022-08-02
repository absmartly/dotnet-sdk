using System;
using System.Text;

namespace ABSmartly.Internal.Hashing;

// Todo: completely refactored, simplified
// private static readonly ThreadLocal<byte[]> threadBuffer = new(() => new byte[512]); => removed
public class MD5
{
    public static string Hash(string unit)
    {
        var bytes = Encoding.UTF8.GetBytes(unit);
        var rawHashedBytes = MD5Wrapper.ComputeHash(bytes);
        var rawHashedString = Convert.ToBase64String(rawHashedBytes);
        var hashedString = rawHashedString.Replace('+', '-').Replace('/', '_').Replace("=", "");
        return hashedString;
    }

    public static byte[] HashToByte(string unit)
    {
        var hashedString = Hash(unit);
        //var hashedBytes = Convert.FromBase64String(hashedString);
        var hashedBytes = Encoding.UTF8.GetBytes(hashedString);
        return hashedBytes;
    }
}
