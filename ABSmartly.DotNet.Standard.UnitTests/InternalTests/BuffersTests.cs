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
}