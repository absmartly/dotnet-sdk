using System.Text;

namespace ABSmartly.DotNet.Standard.UnitTests.InternalTests.HashingTests;

[TestFixture]
public class Murmur3Tests
{
    #region [TestCases]

    [TestCase("", (uint)0x00000000, (uint)0x00000000)]
    [TestCase(" ", (uint)0x00000000, (uint)0x7ef49b98)]
    [TestCase("t", (uint)0x00000000, (uint)0xca87df4d)]
    [TestCase("te", (uint)0x00000000, (uint)0xedb8ee1b)]
    [TestCase("tes", (uint)0x00000000, (uint)0x0bb90e5a)]
    [TestCase("test", (uint)0x00000000, (uint)0xba6bd213)]
    [TestCase("testy", (uint)0x00000000, (uint)0x44af8342)]
    [TestCase("testy1", (uint)0x00000000, (uint)0x8a1a243a)]
    [TestCase("testy12", (uint)0x00000000, (uint)0x845461b9)]
    [TestCase("testy123", (uint)0x00000000, (uint)0x47628ac4)]
    [TestCase("special characters açb↓c", (uint)0x00000000, (uint)0xbe83b140)]
    [TestCase("The quick brown fox jumps over the lazy dog", (uint)0x00000000, (uint)0x2e4ff723)]
    [TestCase("", (uint)0xdeadbeef, (uint)0x0de5c6a9)]
    [TestCase(" ", (uint)0xdeadbeef, (uint)0x25acce43)]
    [TestCase("t", (uint)0xdeadbeef, (uint)0x3b15dcf8)]
    [TestCase("te", (uint)0xdeadbeef, (uint)0xac981332)]
    [TestCase("tes", (uint)0xdeadbeef, (uint)0xc1c78dda)]
    [TestCase("test", 0xdeadbeef, 0xaa22d41a)]
    [TestCase("testy", 0xdeadbeef, 0x84f5f623)]
    [TestCase("testy1", 0xdeadbeef, (uint)0x09ed28e9)]
    [TestCase("testy12", 0xdeadbeef, (uint)0x22467835)]
    [TestCase("testy123", 0xdeadbeef, 0xd633060d)]
    [TestCase("special characters açb↓c", 0xdeadbeef, 0xf7fdd8a2)]
    [TestCase("The quick brown fox jumps over the lazy dog", 0xdeadbeef, (uint)0x3a7b3f4d)]
    [TestCase("", (uint)0x00000001, (uint)0x514e28b7)]
    [TestCase(" ", (uint)0x00000001, (uint)0x4f0f7132)]
    [TestCase("t", (uint)0x00000001, (uint)0x5db1831e)]
    [TestCase("te", (uint)0x00000001, 0xd248bb2e)]
    [TestCase("tes", (uint)0x00000001, 0xd432eb74)]
    [TestCase("test", (uint)0x00000001, 0x99c02ae2)]
    [TestCase("testy", (uint)0x00000001, 0xc5b2dc1e)]
    [TestCase("testy1", (uint)0x00000001, (uint)0x33925ceb)]
    [TestCase("testy12", (uint)0x00000001, 0xd92c9f23)]
    [TestCase("testy123", (uint)0x00000001, (uint)0x3bc1712d)]
    [TestCase("special characters açb↓c", (uint)0x00000001, (uint)0x293327b5)]
    [TestCase("The quick brown fox jumps over the lazy dog", (uint)0x00000001, (uint)0x78e69e27)]    

    #endregion
    public void Digest_WithSeed_Returns_ExpectedValue(string actualString, uint seedHex, uint expectedHex)
    {
        var actualBytes = Encoding.UTF8.GetBytes(actualString);

        var actualHashedBytes = MurMur3.Hash(actualBytes, seedHex);
        var actualHex = BitConverter.ToUInt32(actualHashedBytes);

        Assert.That(actualHex, Is.EqualTo(expectedHex));
    }

    #region [TestCases]

    [TestCase("", (uint)0x00000000, (uint)0x00000000)]
    [TestCase(" ", (uint)0x00000000, (uint)0x7ef49b98)]
    [TestCase("t", (uint)0x00000000, (uint)0xca87df4d)]
    [TestCase("te", (uint)0x00000000, (uint)0xedb8ee1b)]
    [TestCase("tes", (uint)0x00000000, (uint)0x0bb90e5a)]
    [TestCase("test", (uint)0x00000000, (uint)0xba6bd213)]
    [TestCase("testy", (uint)0x00000000, (uint)0x44af8342)]
    [TestCase("testy1", (uint)0x00000000, (uint)0x8a1a243a)]
    [TestCase("testy12", (uint)0x00000000, (uint)0x845461b9)]
    [TestCase("testy123", (uint)0x00000000, (uint)0x47628ac4)]
    [TestCase("special characters açb↓c", (uint)0x00000000, (uint)0xbe83b140)]
    [TestCase("The quick brown fox jumps over the lazy dog", (uint)0x00000000, (uint)0x2e4ff723)]
    [TestCase("", (uint)0xdeadbeef, (uint)0x0de5c6a9)]
    [TestCase(" ", (uint)0xdeadbeef, (uint)0x25acce43)]
    [TestCase("t", (uint)0xdeadbeef, (uint)0x3b15dcf8)]
    [TestCase("te", (uint)0xdeadbeef, (uint)0xac981332)]
    [TestCase("tes", (uint)0xdeadbeef, (uint)0xc1c78dda)]
    [TestCase("test", 0xdeadbeef, 0xaa22d41a)]
    [TestCase("testy", 0xdeadbeef, 0x84f5f623)]
    [TestCase("testy1", 0xdeadbeef, (uint)0x09ed28e9)]
    [TestCase("testy12", 0xdeadbeef, (uint)0x22467835)]
    [TestCase("testy123", 0xdeadbeef, 0xd633060d)]
    [TestCase("special characters açb↓c", 0xdeadbeef, 0xf7fdd8a2)]
    [TestCase("The quick brown fox jumps over the lazy dog", 0xdeadbeef, (uint)0x3a7b3f4d)]
    [TestCase("", (uint)0x00000001, (uint)0x514e28b7)]
    [TestCase(" ", (uint)0x00000001, (uint)0x4f0f7132)]
    [TestCase("t", (uint)0x00000001, (uint)0x5db1831e)]
    [TestCase("te", (uint)0x00000001, 0xd248bb2e)]
    [TestCase("tes", (uint)0x00000001, 0xd432eb74)]
    [TestCase("test", (uint)0x00000001, 0x99c02ae2)]
    [TestCase("testy", (uint)0x00000001, 0xc5b2dc1e)]
    [TestCase("testy1", (uint)0x00000001, (uint)0x33925ceb)]
    [TestCase("testy12", (uint)0x00000001, 0xd92c9f23)]
    [TestCase("testy123", (uint)0x00000001, (uint)0x3bc1712d)]
    [TestCase("special characters açb↓c", (uint)0x00000001, (uint)0x293327b5)]
    [TestCase("The quick brown fox jumps over the lazy dog", (uint)0x00000001, (uint)0x78e69e27)]    

    #endregion
    public void Digest_WithSeedAndOffset_Returns_ExpectedValue(string actualString, uint seedHex, uint expectedHex)
    {
        var actualBytesOffset = Encoding.UTF8.GetBytes("123" + actualString + "321");

        var actualHashedBytes = MurMur3.Hash(actualBytesOffset, "123".Length, actualBytesOffset.Length - "123321".Length, seedHex);
        var actualHex = BitConverter.ToUInt32(actualHashedBytes);

        Assert.That(actualHex, Is.EqualTo(expectedHex));
    }
}