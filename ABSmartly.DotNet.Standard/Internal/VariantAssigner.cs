using System.Threading;
using ABSmartly.Internal.Hashing;

namespace ABSmartly.Internal;

public class VariantAssigner
{
    private readonly int unitHash_;

    private static double normalizer = 1.0 / 0xffffffffL;
    // Todo: pass valuefactory
    private static readonly ThreadLocal<byte[]> threadBuffer = new ThreadLocal<byte[]>();


    //private static ThreadLocal<byte[]> threadBuffer = new ThreadLocal<byte[]>() {
    //    //@Override
    //    public byte[] initialValue() {
    //        return new byte[12];
    //    }
    //};


    public VariantAssigner(byte[] unitHash) {
        unitHash_ = Murmur3_32.Digest(unitHash, 0);
    }

    public int assign(double[] split, int seedHi, int seedLo) {
        double prob = probability(seedHi, seedLo);
        return chooseVariant(split, prob);
    }

    public static int chooseVariant(double[] split, double prob) {
        double cumSum = 0.0;
        for (int i = 0; i < split.Length; ++i) {
            cumSum += split[i];
            if (prob < cumSum) {
                return i;
            }
        }

        return split.Length - 1;
    }

    private double probability(int seedHi, int seedLo) {
        byte[] buffer = threadBuffer.Value;

        Buffers.PutUInt32(buffer, 0, seedLo);
        Buffers.PutUInt32(buffer, 4, seedHi);
        Buffers.PutUInt32(buffer, 8, unitHash_);

        int hash = Murmur3_32.Digest(buffer, 0);
        return (hash & 0xffffffffL) * normalizer;
    }


}