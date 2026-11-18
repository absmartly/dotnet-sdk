using System;
using System.Globalization;
using ABSmartly.Internal.Hashing;

namespace ABSmartly.Internal;

// Balanced-block switchback assignment, ported from java-sdk SwitchbackAssigner (the platform's
// com.absmartly.lib.analytics.switchback.SwitchbackAssignment, Guava murmur3_32_fixed, seed 0):
//   key       = utf8(clusterUnit + "\u001f" + cycleIndex)
//   baseHash  = murmur3_32(le64(seed) ++ le32(murmur3_32(key)))
//   stepHash  = murmur3_32(le32(baseHash) ++ le32(i)),  i = armCount-1 .. 1
//   j         = min(i, floor(u32(stepHash) / 0xffffffff * (i + 1)))
//   variant   = permutation[floorMod(periodIndex, armCount)], cycle = floorDiv(periodIndex, armCount)
// Any change here breaks parity with the platform golden vectors and the analysis-side schedule.
internal static class SwitchbackAssigner
{
    private const double Normalizer = 1.0 / 0xffffffffL;
    private const char Separator = '\u001f';

    public static int Assign(string clusterUnit, long periodIndex, long seed, int armCount)
    {
        RequireValidArmCount(armCount);
        var cycleIndex = FloorDiv(periodIndex, armCount);
        // floorMod; the product may wrap near long.MinValue but the difference is exact mod 2^64.
        var cycleSlot = (int)unchecked(periodIndex - cycleIndex * armCount);
        return FisherYatesPermutation(armCount, BaseHash(clusterUnit, cycleIndex, seed))[cycleSlot];
    }

    public static int BaseHash(string clusterUnit, long cycleIndex, long seed)
    {
        var key = clusterUnit + Separator + cycleIndex.ToString(CultureInfo.InvariantCulture);
        var keyBytes = new byte[key.Length * 3];
        var keyLength = EncodeUtf8(keyBytes, key);
        var keyHash = (int)MurMur3.HashToUInt32(keyBytes, 0, keyLength);

        var buffer = new byte[12];
        PutUInt32(buffer, 0, unchecked((int)seed));
        PutUInt32(buffer, 4, unchecked((int)(seed >> 32)));
        PutUInt32(buffer, 8, keyHash);
        return (int)MurMur3.HashToUInt32(buffer);
    }

    public static int[] FisherYatesPermutation(int armCount, int baseHash)
    {
        RequireValidArmCount(armCount);

        var permutation = new int[armCount];
        for (var i = 0; i < armCount; ++i) permutation[i] = i;

        var buffer = new byte[8];
        PutUInt32(buffer, 0, baseHash);
        for (var i = armCount - 1; i > 0; --i)
        {
            PutUInt32(buffer, 4, i);
            var stepHash = MurMur3.HashToUInt32(buffer);
            var probability = stepHash * Normalizer;
            // probability == 1.0 would map to i + 1
            var j = Math.Min(i, (int)Math.Floor(probability * (i + 1)));

            (permutation[i], permutation[j]) = (permutation[j], permutation[i]);
        }

        return permutation;
    }

    // C# division truncates toward zero; the divisor is always positive here.
    public static long FloorDiv(long dividend, long divisor)
    {
        var q = dividend / divisor;
        return dividend % divisor != 0 && (dividend ^ divisor) < 0 ? q - 1 : q;
    }

    // UTF-8 as java-sdk Buffers.encodeUTF8, which differs from Encoding.UTF8 only for an unpaired
    // surrogate: it is encoded as its own three bytes rather than replaced by U+FFFD.
    private static int EncodeUtf8(byte[] buffer, string value)
    {
        var offset = 0;
        for (var i = 0; i < value.Length; ++i)
        {
            var c = value[i];
            if (c < 0x80)
            {
                buffer[offset++] = (byte)c;
            }
            else if (c < 0x800)
            {
                buffer[offset++] = (byte)(0xc0 | (c >> 6));
                buffer[offset++] = (byte)(0x80 | (c & 0x3f));
            }
            else if (char.IsHighSurrogate(c) && i + 1 < value.Length && char.IsLowSurrogate(value[i + 1]))
            {
                var codePoint = char.ConvertToUtf32(c, value[++i]);
                buffer[offset++] = (byte)(0xf0 | (codePoint >> 18));
                buffer[offset++] = (byte)(0x80 | ((codePoint >> 12) & 0x3f));
                buffer[offset++] = (byte)(0x80 | ((codePoint >> 6) & 0x3f));
                buffer[offset++] = (byte)(0x80 | (codePoint & 0x3f));
            }
            else
            {
                buffer[offset++] = (byte)(0xe0 | (c >> 12));
                buffer[offset++] = (byte)(0x80 | ((c >> 6) & 0x3f));
                buffer[offset++] = (byte)(0x80 | (c & 0x3f));
            }
        }

        return offset;
    }

    private static void PutUInt32(byte[] buffer, int offset, int x)
    {
        buffer[offset] = (byte)(x & 0xff);
        buffer[offset + 1] = (byte)((x >> 8) & 0xff);
        buffer[offset + 2] = (byte)((x >> 16) & 0xff);
        buffer[offset + 3] = (byte)((x >> 24) & 0xff);
    }

    private static void RequireValidArmCount(int armCount)
    {
        if (armCount < 2)
            throw new ArgumentException("armCount must be >= 2, got " + armCount.ToString(CultureInfo.InvariantCulture),
                nameof(armCount));
    }
}
