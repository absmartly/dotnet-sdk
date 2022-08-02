using System;
using System.Security.Cryptography;
using Murmur;

namespace ABSmartly.Internal.Hashing;

/// <summary>
/// Wrapper around the MurMur package
/// https://github.com/darrenkopp/murmurhash-net
/// </summary>
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


    public static uint HashToUInt32(byte[] buffer)
    {
        var hashedBytes = Hash(buffer);
        var hashedValue = BitConverter.ToUInt32(hashedBytes, 0);
        return hashedValue;
    }

    public static uint HashToUInt32(byte[] buffer, int offset, int count)
    {
        var hashedBytes = Hash(buffer, offset, count);
        var hashedValue = BitConverter.ToUInt32(hashedBytes, 0);
        return hashedValue;
    }

    public static uint HashToUInt32(byte[] buffer, uint seed)
    {
        var hashedBytes = Hash(buffer, seed);
        var hashedValue = BitConverter.ToUInt32(hashedBytes, 0);
        return hashedValue;
    }

    public static uint HashToUInt32(byte[] buffer, int offset, int count, uint seed)
    {
        var hashedBytes = Hash(buffer, offset, count, seed);
        var hashedValue = BitConverter.ToUInt32(hashedBytes, 0);
        return hashedValue;
    }
}
