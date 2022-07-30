namespace ABSmartly.DotNet.Standard.UnitTests.InternalTests;

[TestFixture]
public class VariantAssignerTests
{
    [TestCase(1, new[]{0.0, 1.0}, 0.0)]
    [TestCase(1, new[]{0.0, 1.0}, 0.5)]
    [TestCase(1, new[]{0.0, 1.0}, 1.0)]

    [TestCase(0, new[]{1.0, 0.0}, 0.0)]
    [TestCase(0, new[]{1.0, 0.0}, 0.5)]
    [TestCase(1, new[]{1.0, 0.0}, 1.0)]

    [TestCase(0, new[]{0.5, 0.5}, 0.0)]
    [TestCase(0, new[]{0.5, 0.5}, 0.25)]
    [TestCase(0, new[]{0.5, 0.5}, 0.49999999)]
    [TestCase(1, new[]{0.5, 0.5}, 0.5)]
    [TestCase(1, new[]{0.5, 0.5}, 0.50000001)]
    [TestCase(1, new[]{0.5, 0.5}, 0.75)]
    [TestCase(1, new[]{0.5, 0.5}, 1.0)]

    [TestCase(0, new[]{0.333, 0.333, 0.334}, 0.0)]
    [TestCase(0, new[]{0.333, 0.333, 0.334}, 0.25)]
    [TestCase(0, new[]{0.333, 0.333, 0.334}, 0.33299999)]
    [TestCase(1, new[]{0.333, 0.333, 0.334}, 0.333)]
    [TestCase(1, new[]{0.333, 0.333, 0.334}, 0.33300001)]
    [TestCase(1, new[]{0.333, 0.333, 0.334}, 0.5)]
    [TestCase(1, new[]{0.333, 0.333, 0.334}, 0.66599999)]
    [TestCase(2, new[]{0.333, 0.333, 0.334}, 0.666)]
    [TestCase(2, new[]{0.333, 0.333, 0.334}, 0.66600001)]
    [TestCase(2, new[]{0.333, 0.333, 0.334}, 0.75)]
    [TestCase(2, new[]{0.333, 0.333, 0.334}, 1.0)]
    [TestCase(1, new[]{0.0, 1.0}, 0.0)]
    [TestCase(1, new[]{0.0, 1.0}, 1.0)]
    public void ChooseVariant(int expected, double[] split, double probability)
    {
        Assert.That(VariantAssigner.ChooseVariant(split, probability), Is.EqualTo(expected));
    }
}