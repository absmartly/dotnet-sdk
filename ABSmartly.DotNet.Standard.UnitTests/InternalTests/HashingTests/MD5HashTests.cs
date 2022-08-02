using System.Text;

namespace ABSmartly.DotNet.Standard.UnitTests.InternalTests.HashingTests;

[TestFixture]
public class MD5HashTests
{
    [TestCase("4a42766ca6313d26f49985e799ff4f3790fb86efa0fce46edb3ea8fbf1ea3408", "H2jvj6o9YcAgNdhKqEbtWw")]
    [TestCase("bleh@absmarty.com", "DRgslOje35bZMmpaohQjkA")]
    [TestCase("açb↓c", "LxcqH5VC15rXfWfA_smreg")]
    [TestCase("123456778999", "K4uy4bTeCy34W97lmceVRg")]
    public void Hash_InputString_Returns_ExpectedHashedString(string actualString, string expectedHash)
    {
        var actualStringHash = MD5Hash.Hash(actualString);

        Assert.That(actualStringHash, Is.EqualTo(expectedHash));
    }

    [Test]
    public void Hash_LargeInputString_Returns_ExpectedHashedString()
    {
        const string actualString = "4a42766ca6313d26f49985e799ff4f3790fb86efa0fce46edb3ea8fbf1ea3408";
        var sb = new StringBuilder();
        var count = (2048 + actualString.Length - 1) / actualString.Length;

        for (var i = 0; i < count; i++)
        {
            sb.Append(actualString);
        }

        var actualStringHash = MD5Hash.Hash(sb.ToString());

        Assert.That(actualStringHash, Is.EqualTo("Rxnq-eM9eE1SEoMnkEMOIw"));
    }


}