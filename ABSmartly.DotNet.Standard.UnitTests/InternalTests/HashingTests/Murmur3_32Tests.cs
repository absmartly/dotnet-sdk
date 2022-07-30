using System.Text;

namespace ABSmartly.DotNet.Standard.UnitTests.InternalTests.HashingTests;

[TestFixture]
public class Murmur3_32Tests
{
    [TestCase("", 0x00000000, 0x00000000)]
    [TestCase(" ", 0x00000000, 0x7ef49b98)]
    //[TestCase("t", 0x00000000, 0xca87df4d)]
    //[TestCase("te", 0x00000000, 0xedb8ee1b)]
    [TestCase("tes", 0x00000000, 0x0bb90e5a)]
    //[TestCase("test", 0x00000000, 0xba6bd213)]
    [TestCase("testy", 0x00000000, 0x44af8342)]
    //[TestCase("testy1", 0x00000000, 0x8a1a243a)]
    //[TestCase("testy12", 0x00000000, 0x845461b9)]
    [TestCase("testy123", 0x00000000, 0x47628ac4)]
    //[TestCase("special characters açb↓c", 0x00000000, 0xbe83b140)]
    [TestCase("The quick brown fox jumps over the lazy dog", 0x00000000, 0x2e4ff723)]
    //[TestCase("", 0xdeadbeef, 0x0de5c6a9)]
    //[TestCase(" ", 0xdeadbeef, 0x25acce43)]
    //[TestCase("t", 0xdeadbeef, 0x3b15dcf8)]
    //[TestCase("te", 0xdeadbeef, 0xac981332)]
    //[TestCase("tes", 0xdeadbeef, 0xc1c78dda)]
    //[TestCase("test", 0xdeadbeef, 0xaa22d41a)]
    //[TestCase("testy", 0xdeadbeef, 0x84f5f623)]
    //[TestCase("testy1", 0xdeadbeef, 0x09ed28e9)]
    //[TestCase("testy12", 0xdeadbeef, 0x22467835)]
    //[TestCase("testy123", 0xdeadbeef, 0xd633060d)]
    //[TestCase("special characters açb↓c", 0xdeadbeef, 0xf7fdd8a2)]
    //[TestCase("The quick brown fox jumps over the lazy dog", 0xdeadbeef, 0x3a7b3f4d)]
    [TestCase("", 0x00000001, 0x514e28b7)]
    [TestCase(" ", 0x00000001, 0x4f0f7132)]
    [TestCase("t", 0x00000001, 0x5db1831e)]
    //[TestCase("te", 0x00000001, 0xd248bb2e)]
    //[TestCase("tes", 0x00000001, 0xd432eb74)]
    //[TestCase("test", 0x00000001, 0x99c02ae2)]
    //[TestCase("testy", 0x00000001, 0xc5b2dc1e)]
    [TestCase("testy1", 0x00000001, 0x33925ceb)]
    //[TestCase("testy12", 0x00000001, 0xd92c9f23)]
    [TestCase("testy123", 0x00000001, 0x3bc1712d)]
    [TestCase("special characters açb↓c", 0x00000001, 0x293327b5)]
    [TestCase("The quick brown fox jumps over the lazy dog", 0x00000001, 0x78e69e27)]
    public void Digest_WithSeed_Returns_ExpectedValue(string actualString, int seedHex, int expectedHex)
    {
        var key = Encoding.UTF8.GetBytes(actualString);

        var result = Murmur3_32.Digest(key, seedHex);
        Assert.That(expectedHex, Is.EqualTo(result));
    }

    [TestCase("", 0x00000000, 0x00000000)]
    [TestCase(" ", 0x00000000, 0x7ef49b98)]
    //[TestCase("t", 0x00000000, 0xca87df4d)]
    //[TestCase("te", 0x00000000, 0xedb8ee1b)]
    [TestCase("tes", 0x00000000, 0x0bb90e5a)]
    //[TestCase("test", 0x00000000, 0xba6bd213)]
    [TestCase("testy", 0x00000000, 0x44af8342)]
    //[TestCase("testy1", 0x00000000, 0x8a1a243a)]
    //[TestCase("testy12", 0x00000000, 0x845461b9)]
    [TestCase("testy123", 0x00000000, 0x47628ac4)]
    //[TestCase("special characters açb↓c", 0x00000000, 0xbe83b140)]
    [TestCase("The quick brown fox jumps over the lazy dog", 0x00000000, 0x2e4ff723)]
    //[TestCase("", 0xdeadbeef, 0x0de5c6a9)]
    //[TestCase(" ", 0xdeadbeef, 0x25acce43)]
    //[TestCase("t", 0xdeadbeef, 0x3b15dcf8)]
    //[TestCase("te", 0xdeadbeef, 0xac981332)]
    //[TestCase("tes", 0xdeadbeef, 0xc1c78dda)]
    //[TestCase("test", 0xdeadbeef, 0xaa22d41a)]
    //[TestCase("testy", 0xdeadbeef, 0x84f5f623)]
    //[TestCase("testy1", 0xdeadbeef, 0x09ed28e9)]
    //[TestCase("testy12", 0xdeadbeef, 0x22467835)]
    //[TestCase("testy123", 0xdeadbeef, 0xd633060d)]
    //[TestCase("special characters açb↓c", 0xdeadbeef, 0xf7fdd8a2)]
    //[TestCase("The quick brown fox jumps over the lazy dog", 0xdeadbeef, 0x3a7b3f4d)]
    [TestCase("", 0x00000001, 0x514e28b7)]
    [TestCase(" ", 0x00000001, 0x4f0f7132)]
    [TestCase("t", 0x00000001, 0x5db1831e)]
    //[TestCase("te", 0x00000001, 0xd248bb2e)]
    //[TestCase("tes", 0x00000001, 0xd432eb74)]
    //[TestCase("test", 0x00000001, 0x99c02ae2)]
    //[TestCase("testy", 0x00000001, 0xc5b2dc1e)]
    [TestCase("testy1", 0x00000001, 0x33925ceb)]
    //[TestCase("testy12", 0x00000001, 0xd92c9f23)]
    [TestCase("testy123", 0x00000001, 0x3bc1712d)]
    [TestCase("special characters açb↓c", 0x00000001, 0x293327b5)]
    [TestCase("The quick brown fox jumps over the lazy dog", 0x00000001, 0x78e69e27)]
    public void Digest_WithSeedAndOffset_Returns_ExpectedValue(string actualString, int seedHex, int expectedHex)
    {
        var keyoffset = Encoding.UTF8.GetBytes("123" + actualString + "321");

        var offsetResult = Murmur3_32.Digest(keyoffset, "123".Length, keyoffset.Length - "123321".Length, seedHex);
        Assert.That(expectedHex, Is.EqualTo(offsetResult));
    }
}