using System.Security.Cryptography;
using Murmur;

namespace ABSmartly.Internal.Hashing;

public static class MurMur3
{
    // Use a static instance when the seed is not used 
    private static readonly HashAlgorithm murmur;

    static MurMur3()
    {
        murmur = MurmurHash.Create32(managed: false, seed: 0);
    }

    public static byte[] Hash(byte[] buffer)
    {
        return murmur.ComputeHash(buffer);
    }

    public static byte[] Hash(byte[] buffer, int offset, int count)
    {
        return murmur.ComputeHash(buffer, offset, count);
    }

    public static byte[] Hash(byte[] buffer, uint seed)
    {
        var murmurHash = MurmurHash.Create32(managed: false, seed: seed);
        return murmurHash.ComputeHash(buffer);
    }

    public static byte[] Hash(byte[] buffer, int offset, int count, uint seed)
    {
        var murmurHash = MurmurHash.Create32(managed: false, seed: seed);
        return murmurHash.ComputeHash(buffer, offset, count);
    }
}
