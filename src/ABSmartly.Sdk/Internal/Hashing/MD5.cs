using System;
using System.Text;
using DotNetMD5 = System.Security.Cryptography.MD5;

namespace ABSmartly.Internal.Hashing;

public static class Md5
{
    private static readonly DotNetMD5 DotNetMd5;

    static Md5()
    {
        DotNetMd5 = DotNetMD5.Create();
    }

    public static string Hash(string unit)
    {
        var bytes = Encoding.UTF8.GetBytes(unit);
        var len = (uint)bytes.Length;
        var hashBytes = DotNetMd5.ComputeHash(bytes, 0, (int)len);
        var rawHashedString = Convert.ToBase64String(hashBytes);
        var hashedString = rawHashedString.Replace('+', '-').Replace('/', '_').Replace("=", "");
        return hashedString;
    }

    public static byte[] HashToUtf8Bytes(string unit)
    {
        var hashedString = Hash(unit);
        var hashedBytes = Encoding.UTF8.GetBytes(hashedString);
        return hashedBytes;
    }
}