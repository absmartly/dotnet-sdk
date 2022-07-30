namespace ABSmartly.DotNet.Standard.UnitTests.InternalTests;

[TestFixture]
public class BuffersTests
{
    [Test]
    public void PutUInt32()
    {
        const int bytesLen = 9;
        for (var i = 0; i < bytesLen - 3; ++i) 
        {
            var bytes = new byte[bytesLen];

            var expected = (int)((i + 1) * 0xe6546b64);
            Buffers.PutUInt32(bytes, i, expected);
            var actual = Buffers.GetUInt32(bytes, i);

            Assert.That(actual, Is.EqualTo(expected));

            for (var b = 0; b < bytesLen; ++b) 
            {
                if (b < i || b >= (i + 4)) 
                {
                    Assert.That(bytes[b], Is.EqualTo(0));
                }
            }
        }
    }

    [Test]
    public void GetUInt32()
    {
        var bytes = new byte[]{97, 226, 134, 147, 98, 196, 0};

        for (var i = 0; i < bytes.Length - 3; ++i) 
        {
            Assert.That(Buffers.GetUInt32(bytes, i), 
                Is.EqualTo((bytes[i] & 0xff) | 
                           ((bytes[i + 1] & 0xff) << 8) | 
                           ((bytes[i + 2] & 0xff) << 16) | 
                           ((bytes[i + 3] & 0xff) << 24)));
        }
    }

    [Test]
    public void GetUInt24()
    {
        var bytes = new byte[]{97, 226, 134, 147, 98, 0};

        for (var i = 0; i < bytes.Length - 2; ++i) 
        {
            Assert.That(Buffers.GetUInt24(bytes, i), 
                Is.EqualTo((bytes[i] & 0xff) | 
                           ((bytes[i + 1] & 0xff) << 8) | 
                           ((bytes[i + 2] & 0xff) << 16)));
        }
    }

    [Test]
    public void GetUInt16()
    {
        var bytes = new byte[]{97, 226, 134, 147, 98, 0};

        for (var i = 0; i < bytes.Length - 1; ++i)
        {
            Assert.That(Buffers.GetUInt16(bytes, i), 
                Is.EqualTo((bytes[i] & 0xff) | 
                           ((bytes[i + 1] & 0xff) << 8)));
        }
    }

    [Test]
    public void GetUInt8()
    {
        var bytes = new byte[]{97, 226, 134, 147, 98, 0};
        for (var i = 0; i < bytes.Length; ++i) 
        {
            Assert.That(Buffers.GetUInt8(bytes, i), Is.EqualTo(bytes[i] & 0xff));
        }
    }

    [TestCase("", new byte[] { } )]
    [TestCase(" ", new byte[] { 32 } )]
    public void EncodeUTF8(string value, byte[] expectedBytes)
    {
        var actual = new byte[expectedBytes.Length];
        var encodeLength = Buffers.EncodeUTF8(actual, 0, value.ToCharArray());

        Assert.That(actual, Is.EqualTo(expectedBytes));
        Assert.That(encodeLength, Is.EqualTo(expectedBytes.Length));
    }

    [TestCase("", new byte[] { } )]
    [TestCase(" ", new byte[] { 32 } )]
    public void EncodeUTF8_WithOffset(string value, byte[] expectedBytes)
    {
        var actualOffset = new byte[3 + expectedBytes.Length];
        var encodeLengthOffset = Buffers.EncodeUTF8(actualOffset, 3, value.ToCharArray());

        //assertArrayEquals(expected, Arrays.copyOfRange(actualOffset, 3, 3 + encodeLengthOffset));

        //Array ar;
        //actualOffset.CopyTo(ar, );

        //Assert.That(actualOffset, Is.EqualTo(expectedBytes));
        //Assert.That(actualOffset, Is.EqualTo(copyOfRange(expectedBytes, actualOffset, 3 + encodeLengthOffset)));
        //Assert.That(actualOffset, Is.EqualTo(expectedBytes.Skip(3).Take(3 + encodeLengthOffset).ToArray()));
        //Assert.That(actualOffset, Is.EqualTo(expectedBytes[actualOffset..3]));

        //var expectedOffsetBytes = CopyOfRange(actualOffset, 3, 3 + encodeLengthOffset);
        var expectedOffsetBytes = actualOffset[3..(3 + encodeLengthOffset)]; //CopyOfRange(actualOffset, 3, 3 + encodeLengthOffset);
        //var expectedOffsetBytes = actualOffset.Skip(3).Take(3 + encodeLengthOffset).ToArray();

        Assert.AreEqual(expectedOffsetBytes, CopyOfRange(actualOffset, 3, 3 + encodeLengthOffset));

        //Assert.That(actualOffset, Is.EqualTo(expectedOffsetBytes));


        Assert.That(encodeLengthOffset, Is.EqualTo(expectedBytes.Length));
    }


    private byte[] CopyOfRange (byte[] src, int start, int end) {
        var len = end - start;
        var dest = new byte[len];
        // note i is always from 0
        for (var i = 0; i < len; i++)
        {
            dest[i] = src[start + i]; // so 0..n = 0+x..n+x
        }
        return dest;
    }
}