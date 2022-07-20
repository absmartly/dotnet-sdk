using System.Linq;

namespace ABSmartly.Internal;

public abstract class Buffers
{
    public static void PutUInt32(byte[] buf, int offset, int x) 
    {
        buf[offset] = (byte) (x & 0xff);
        buf[offset + 1] = (byte) ((x >> 8) & 0xff);
        buf[offset + 2] = (byte) ((x >> 16) & 0xff);
        buf[offset + 3] = (byte) ((x >> 24) & 0xff);
    }

    public static int GetUInt32(byte[] buf, int offset) 
    {
        return (buf[offset] & 0xff) | ((buf[offset + 1] & 0xff) << 8) | ((buf[offset + 2] & 0xff) << 16)
               | ((buf[offset + 3] & 0xff) << 24);
    }

    public static int GetUInt24(byte[] buf, int offset) 
    {
        return (buf[offset] & 0xff) | ((buf[offset + 1] & 0xff) << 8) | ((buf[offset + 2] & 0xff) << 16);
    }

    public static int GetUInt16(byte[] buf, int offset) 
    {
        return (buf[offset] & 0xff) | ((buf[offset + 1] & 0xff) << 8);
    }

    public static int GetUInt8(byte[] buf, int offset) 
    {
        return (buf[offset] & 0xff);
    }

    public static int EncodeUTF8(byte[] buf, int offset, char[] value) 
    {
        var n = value.Length;

        var out2 = offset;
        for (var i = 0; i < n; ++i) 
        {
            var c = value.ElementAt(i);
            if (c < 0x80) {
                buf[out2++] = (byte) c;
            } else if (c < 0x800) {
                buf[out2++] = (byte) ((c >> 6) | 192);
                buf[out2++] = (byte) ((c & 63) | 128);
            } else {
                buf[out2++] = (byte) ((c >> 12) | 224);
                buf[out2++] = (byte) (((c >> 6) & 63) | 128);
                buf[out2++] = (byte) ((c & 63) | 128);
            }
        }
        return out2 - offset;
    }
}