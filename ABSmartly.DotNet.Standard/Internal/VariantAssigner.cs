using System.Threading;
using ABSmartly.Internal.Hashing;

namespace ABSmartly.Internal;

public class VariantAssigner
{
    private readonly uint _unitHash;

    private static readonly double normalizer = 1.0 / 0xffffffffL;
    private static readonly ThreadLocal<byte[]> threadBuffer = new(() => new byte[12]);

    public VariantAssigner(byte[] unitHash) 
    {
        _unitHash = MurMur3.HashToUInt32(unitHash);
    }

    public int Assign(double[] split, uint seedHi, uint seedLo) 
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
            if (probability < cumSum) 
            {
                return i;
            }
        }

        return split.Length - 1;
    }

    private double Probability(uint seedHi, uint seedLo) 
    {
        var buffer = threadBuffer.Value;

        Buffers.PutUInt32(buffer, 0, seedLo);
        Buffers.PutUInt32(buffer, 4, seedHi);
        Buffers.PutUInt32(buffer, 8, _unitHash);

        var hash = MurMur3.HashToUInt32(buffer);
        return (hash & 0xffffffffL) * normalizer;
    }
}