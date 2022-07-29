using System.Threading;
using ABSmartly.Utils.Extensions;

namespace ABSmartly.Internal.Hashing;

public class Hashing
{
    private static readonly ThreadLocal<byte[]> threadBuffer = new(() => new byte[512]);

    public static byte[] HashUnit(char[] unit)
    {
        var n = unit.Length;
        var bufferLength = n << 1;

        var buffer = threadBuffer.Value;
        if (buffer.Length < bufferLength)
        {
            var bit = 32 - (bufferLength - 1).GetNumberOfLeadingZeros();
            buffer = new byte[1 << bit];
            threadBuffer.Value = buffer;
        }

        var encoded = Buffers.EncodeUTF8(buffer, 0, unit);
        return MD5.DigestBase64UrlNoPadding(buffer, 0, encoded);
    }

}