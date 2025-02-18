using System;
using System.Text;
using DotNetMD5 = System.Security.Cryptography.MD5;

namespace ABSmartly.Internal.Hashing;

public static class Md5
{

#if !NET5_0_OR_GREATER
    private static readonly DotNetMD5 DotNetMd5;

    static Md5()
    {
        DotNetMd5 = DotNetMD5.Create();
    }
#endif

    public static string Hash(string unit)
    {
        var bytes = Encoding.UTF8.GetBytes(unit);

#if NET5_0_OR_GREATER
        var hashBytes = DotNetMD5.HashData(bytes);
#else
        byte[] hashBytes;

        lock (DotNetMd5)
        {
            hashBytes = DotNetMd5.ComputeHash(bytes);
        }
#endif

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