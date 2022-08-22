using DotNetMD5 = System.Security.Cryptography.MD5;

namespace ABSmartlySdk.Internal.Hashing;

internal static class MD5Wrapper
{
    private static readonly DotNetMD5 md5;

    static MD5Wrapper()
    {
        md5 = DotNetMD5.Create();
    }

    internal static byte[] ComputeHash(byte[] key)
    {
        return ComputeHash(key, 0, (uint)key.Length);
    }

    internal static byte[] ComputeHash(byte[] key, int offset, uint len)
    {
        var result = md5.ComputeHash(key, offset, (int)len);
        return result;
    }
}