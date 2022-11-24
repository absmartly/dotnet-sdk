using System;
using Murmur;

namespace ABSmartly.Internal.Hashing;

/// <summary>
/// Wrapper around the MurMur package
/// https://github.com/darrenkopp/murmurhash-net
/// </summary>
public static class MurMur3
{
    public static uint HashToUInt32(byte[] buffer, uint seed = 0) => HashToUInt32(buffer, 0, buffer.Length, seed);

    public static uint HashToUInt32(byte[] buffer, int offset, int len, uint seed = 0)
    {
        var span = new Span<byte>(buffer, offset, len);
        using var murmur3 = MurmurHash.Create32(seed);
        var hashedBytes = murmur3.ComputeHash(span.ToArray());
        var hashedValue = BitConverter.ToUInt32(hashedBytes, 0);
        return hashedValue;
    }
}