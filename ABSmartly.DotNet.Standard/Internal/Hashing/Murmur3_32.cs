using ABSmartly.Temp;

namespace ABSmartly.Internal.Hashing;

public class Murmur3_32
{
    public static int Digest(byte[] key, int seed) {
        return Digest(key, 0, key.Length, seed);
    }

    public static int Digest(byte[] key, int offset, int length, int seed) {
        int n = offset + (length & ~3);

        int hash = seed;
        int i = offset;
        for (; i < n; i += 4) {
            int chunk = Buffers.GetUInt32(key, i);
            hash ^= scramble32(chunk);
            hash = Integer.RotateLeft(hash, 13);
            hash = (int)((hash * 5) + 0xe6546b64);
        }

        switch (length & 3) {
            case 3:
                hash ^= scramble32(Buffers.GetUInt24(key, i));
                break;
            case 2:
                hash ^= scramble32(Buffers.GetUInt16(key, i));
                break;
            case 1:
                hash ^= scramble32(Buffers.GetUInt8(key, i));
                break;
            case 0:
            default:
                break;
        }

        hash ^= length;
        hash = fmix32(hash);
        return hash;
    }


    private static int fmix32(int h) {
        h ^= ((int)(uint)h >> 16);
        h = (int)(h * 0x85ebca6b);
        h ^= ((int)(uint)h >> 13);
        h = (int)(h * 0xc2b2ae35);
        h ^= ((int)(uint)h >> 16);

        return h;
    }

    private static int scramble32(int block) {
        return Integer.RotateLeft((int)(block * 0xcc9e2d51), 15) * 0x1b873593;
    }
}