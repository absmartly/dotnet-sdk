using System.Text;

namespace ABSmartly.DotNet.Standard.UnitTests.InternalTests.HashingTests;

[TestFixture]
public class MD5HashTests
{
    [TestCase("", "1B2M2Y8AsgTpgAmY7PhCfg")]
    [TestCase(" ", "chXunH2dwinSkhpA6JnsXw")]
    [TestCase("t", "41jvpIn1gGLxDdcxa2Vkng")]
    [TestCase("te", "Vp73JkK-D63XEdakaNaO4Q")]
    [TestCase("tes", "KLZi2IO212_Zbk3cXpungA")]
    [TestCase("test", "CY9rzUYh03PK3k6DJie09g")]
    [TestCase("testy", "K5I_V6RgP8c6sYKz-TVn8g")]
    [TestCase("testy1", "8fT8xGipOhPkZ2DncKU-1A")]
    [TestCase("testy12", "YqRAtOz000gIu61ErEH18A")]
    [TestCase("testy123", "pfV2H07L6WvdqlY0zHuYIw")]
    [TestCase("special characters açb↓c", "4PIrO7lKtTxOcj2eMYlG7A")]
    [TestCase("The quick brown fox jumps over the lazy dog", "nhB9nTcrtoJr2B01QqQZ1g")]
    [TestCase("The quick brown fox jumps over the lazy dog and eats a pie", "iM-8ECRrLUQzixl436y96A")]
    [TestCase("Lorem ipsum dolor sit amet, consectetur adipiscing elit, sed do eiusmod tempor incididunt ut labore et dolore magna aliqua. Ut enim ad minim veniam, quis nostrud exercitation ullamco laboris nisi ut aliquip ex ea commodo consequat. Duis aute irure dolor in reprehenderit in voluptate velit esse cillum dolore eu fugiat nulla pariatur. Excepteur sint occaecat cupidatat non proident, sunt in culpa qui officia deserunt mollit anim id est laborum.",
    "24m7XOq4f5wPzCqzbBicLA")]

    [TestCase("4a42766ca6313d26f49985e799ff4f3790fb86efa0fce46edb3ea8fbf1ea3408", "H2jvj6o9YcAgNdhKqEbtWw")]
    [TestCase("bleh@absmarty.com", "DRgslOje35bZMmpaohQjkA")]
    [TestCase("açb↓c", "LxcqH5VC15rXfWfA_smreg")]
    [TestCase("123456778999", "K4uy4bTeCy34W97lmceVRg")]
    public void Hash_InputString_Returns_ExpectedHashedString(string actualString, string expectedHash)
    {
        var actualStringHash = MD5.Hash(actualString);

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

        var actualStringHash = MD5.Hash(sb.ToString());

        Assert.That(actualStringHash, Is.EqualTo("Rxnq-eM9eE1SEoMnkEMOIw"));
    }


}