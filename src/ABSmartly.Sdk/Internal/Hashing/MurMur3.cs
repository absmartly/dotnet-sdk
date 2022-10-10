using System;
using Murmur;

namespace ABSmartly.Internal.Hashing;

/// <summary>
/// Wrapper around the MurMur package
/// https://github.com/darrenkopp/murmurhash-net
/// </summary>
public static class MurMur3
{
    public static uint HashToUInt32(byte[] buffer)
    {
        using var murmur3 = MurmurHash.Create32(0);
        var hashedBytes = murmur3.ComputeHash(buffer);
        var hashedValue = BitConverter.ToUInt32(hashedBytes, 0);
        return hashedValue;
    }
}