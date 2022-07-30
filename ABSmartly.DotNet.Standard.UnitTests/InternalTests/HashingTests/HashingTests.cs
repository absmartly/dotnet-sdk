using System.Text;

namespace ABSmartly.DotNet.Standard.UnitTests.InternalTests.HashingTests;

[TestFixture]
public class HashingTests
{
    [TestCase("4a42766ca6313d26f49985e799ff4f3790fb86efa0fce46edb3ea8fbf1ea3408", "H2jvj6o9YcAgNdhKqEbtWw")]
    [TestCase("bleh@absmarty.com", "DRgslOje35bZMmpaohQjkA")]
    [TestCase("açb↓c", "LxcqH5VC15rXfWfA_smreg")]
    [TestCase("123456778999", "K4uy4bTeCy34W97lmceVRg")]
    public void TestHashUnit(string actualText, string expectedHash)
    {
        var hash = Hashing.HashUnit(actualText.ToCharArray());
        Assert.That(hash, Is.EqualTo(expectedHash));
    }

    [TestCase]
    public void TestHashUnit_Large()
    {
        var actual = "4a42766ca6313d26f49985e799ff4f3790fb86efa0fce46edb3ea8fbf1ea3408";
        var sb = new StringBuilder();
        var count = (2048 + actual.Length - 1) / actual.Length;

        for (var i = 0; i < count; i++)
        {
            sb.Append(actual);
        }

        var hash = Hashing.HashUnit(sb.ToString().ToCharArray());

        Assert.That(hash, Is.EqualTo("Rxnq-eM9eE1SEoMnkEMOIw"));
    }


}