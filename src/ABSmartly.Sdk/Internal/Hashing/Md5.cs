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
        var bytes = Utf8Bytes(unit);

#if NET5_0_OR_GREATER
        var hashBytes = DotNetMD5.HashData(bytes);
#else
        byte[] hashBytes;

        lock (DotNetMd5)
        {
            hashBytes = DotNetMd5.ComputeHash(bytes);
        }
#endif

        return Base64UrlNoPadding(hashBytes);
    }

    // The UTF-8 encoding used for unit hashing.
    public static byte[] Utf8Bytes(string value)
    {
        return Encoding.UTF8.GetBytes(value);
    }

    // URL-safe base64 without padding, the encoding of unit hashes.
    public static string Base64UrlNoPadding(byte[] bytes)
    {
        return Convert.ToBase64String(bytes).Replace('+', '-').Replace('/', '_').Replace("=", "");
    }

    public static byte[] HashToUtf8Bytes(string unit)
    {
        var hashedString = Hash(unit);
        var hashedBytes = Encoding.UTF8.GetBytes(hashedString);
        return hashedBytes;
    }
}