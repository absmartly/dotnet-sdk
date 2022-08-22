using System;
using System.Diagnostics;

namespace ABSmartlySdk.Utils.Converters;

public class ByteConverter
{
    public static byte[] Convert(byte[] bytes, int offset, int length)
    {
        try
        {
            var customBytes = new byte[length];

            var b = 0;
            for (var i = offset; i < length; i++)
            {
                customBytes[b] = bytes[i];
                b++;
            }

            return customBytes;
        }
        catch (Exception e)
        {
            Debug.WriteLine(e);
            return null;
        }
    }
}