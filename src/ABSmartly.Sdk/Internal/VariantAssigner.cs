using System;
using System.Threading;
using ABSmartly.Internal.Hashing;

namespace ABSmartly.Internal;

internal class VariantAssigner
{
    private readonly int _unitHash;

    private const double Normalizer = 1.0 / 0xffffffffL;
    private static readonly ThreadLocal<byte[]> ThreadBuffer = new(() => new byte[12]);

    public VariantAssigner(byte[] unitHash)
    {
        _unitHash = (int)MurMur3.HashToUInt32(unitHash);
    }

    public int Assign(double[] split, int seedHi, int seedLo)
    {
        var probability = Probability(seedHi, seedLo);
        return ChooseVariant(split, probability);
    }

    public static int ChooseVariant(double[] split, double probability)
    {
        var cumSum = 0.0;
        for (var i = 0; i < split.Length; ++i)
        {
            cumSum += split[i];
            if (probability < cumSum) return i;
        }

        return split.Length - 1;
    }

    private double Probability(int seedHi, int seedLo)
    {
        var buffer = ThreadBuffer.Value;

        PutUInt32(buffer, 0, seedLo);
        PutUInt32(buffer, 4, seedHi);
        PutUInt32(buffer, 8, _unitHash);

        var hash = MurMur3.HashToUInt32(buffer);
        return (hash & 0xffffffffL) * Normalizer;
    }

    private static void PutUInt32(byte[] buf, int offset, int x)
    {
        buf[offset] = (byte)(x & 0xff);
        buf[offset + 1] = (byte)((x >> 8) & 0xff);
        buf[offset + 2] = (byte)((x >> 16) & 0xff);
        buf[offset + 3] = (byte)((x >> 24) & 0xff);
    }
}