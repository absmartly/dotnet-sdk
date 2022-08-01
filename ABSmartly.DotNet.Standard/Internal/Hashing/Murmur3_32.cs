using ABSmartly.Temp;

namespace ABSmartly.Internal.Hashing;

public class Murmur3_32
{
    public static uint Digest(byte[] key, uint seed) 
    {
        return Digest(key, 0, key.Length, seed);
    }

    public static uint Digest(byte[] key, int offset, int length, uint seed) 
    {
        uint n = (uint)(offset + (length & ~3));

        uint hash = seed;
        var i = offset;
        for (; i < n; i += 4) {
            uint chunk = Buffers.GetUInt32(key, i);
            hash ^= scramble32(chunk);
            hash = (uint)Integer.RotateLeft((int)hash, 13);
            hash = ((hash * 5) + 0xe6546b64);
        }

        switch (length & 3) {
            case 3:
                hash ^= scramble32(Buffers.GetUInt24(key, (int)i));
                break;
            case 2:
                hash ^= scramble32(Buffers.GetUInt16(key, (int)i));
                break;
            case 1:
                hash ^= scramble32(Buffers.GetUInt8(key, (int)i));
                break;
            case 0:
            default:
                break;
        }

        hash ^= (uint)length;
        hash = fmix32(hash);
        return hash;
    }


    private static uint fmix32(uint h) 
    {
        h ^= (h >> 16);
        h = (h * 0x85ebca6b);
        h ^= ((uint)h >> 13);
        h = (h * 0xc2b2ae35);
        h ^= ((uint)h >> 16);

        return h;
    }

    private static uint scramble32(uint block) 
    {
        var r = Integer.RotateLeft((int)(block * 0xcc9e2d51), 15) * 0x1b873593;
        return (uint)r;
    }
}