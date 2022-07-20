using System;
using System.Threading;

namespace ABSmartly.Internal.Hashing;

public abstract class Hashing
{
    protected Hashing()
    {
        
    }
    // Todo: ???
    private static readonly Func<byte[]> valueFactory = () => new byte[512];
    private static ThreadLocal<byte[]> threadBuffer = new(() => valueFactory());

    public static byte[] HashUnit(char[] unit)
    {
        var n = unit.Length;
        var bufferLength = n << 1;

        var buffer = threadBuffer.Value;
        if (buffer.Length < bufferLength)
        {
            //var bit = 32 - Integer.numberOfLeadingZeros(bufferLen - 1);
            //buffer = new byte[1 << bit];
            ////threadBuffer.set(buffer);
            //threadBuffer.Value = buffer;
        }

        var encoded = Buffers.EncodeUTF8(buffer, 0, unit);
        return MD5.DigestBase64UrlNoPadding(buffer, 0, encoded);
    }

}