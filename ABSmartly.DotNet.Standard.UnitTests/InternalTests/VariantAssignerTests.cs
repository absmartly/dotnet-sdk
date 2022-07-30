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



    [TestCase(123456789, new[] { 1, 0, 1, 1, 1, 0, 0, 2, 1, 2, 2, 2, 0, 0 })]
    [TestCase("bleh@absmartly.com", new[] { 0, 1, 0, 0, 0, 0, 1, 0, 2, 0, 0, 0, 1, 1 })]
    [TestCase("e791e240fcd3df7d238cfc285f475e8152fcc0ec", new[] { 1, 0, 1, 1, 0, 0, 0, 2, 0, 2, 1, 0, 0, 1 })]
    public void AssignmentsMatch(object unitUID, int[] expectedVariants)
    {
        var splits = new List<List<double>>()
        {
            new() { 0.5, 0.5 }, new() { 0.5, 0.5 }, new() { 0.5, 0.5 }, 
            new() { 0.5, 0.5 }, new() { 0.5, 0.5 }, 
            new() { 0.5, 0.5 }, new() { 0.5, 0.5 },
            new() { 0.33, 0.33, 0.34 }, new() { 0.33, 0.33, 0.34 }, 
            new() { 0.33, 0.33, 0.34 }, new() { 0.33, 0.33, 0.34 }, 
            new() { 0.33, 0.33, 0.34 }, new() { 0.33, 0.33, 0.34 },
            new() { 0.33, 0.33, 0.34 }
        };

        var seeds = new List<List<int>>()
        {
            new() { 0x00000000, 0x00000000 },
            new() { 0x00000000, 0x00000001 },
            //new() { 0x8015406f, 0x7ef49b98 },
            //new() { 0x3b2e7d90, 0xca87df4d },
            //new() { 0x52c1f657, 0xd248bb2e },
            //new() { 0x865a84d0, 0xaa22d41a },
            //new() { 0x27d1dc86, 0x845461b9 },
            new() { 0x00000000, 0x00000000 },
            new() { 0x00000000, 0x00000001 },
            //new() { 0x8015406f, 0x7ef49b98 },
            //new() { 0x3b2e7d90, 0xca87df4d },
            //new() { 0x52c1f657, 0xd248bb2e },
            //new() { 0x865a84d0, 0xaa22d41a },
            //new() { 0x27d1dc86, 0x845461b9 },
        };

        var unitHash = Hashing.HashUnit(unitUID.ToString()?.ToCharArray());
        var assigner = new VariantAssigner(unitHash);

        for (var i = 0; i < seeds.Count; i++)
        {
            var frags = seeds[i];
            var split = splits[i].ToArray();
            var variant = assigner.Assign(split, frags[0], frags[1]);
            Assert.That(variant, Is.EqualTo(expectedVariants[i]));
        }
    }


    //static Stream<? extends Arguments> testAssignmentsMatchArgs() 
    //{
    //    return Stream.of(
    //        Arguments.of(123456789, listOf(1, 0, 1, 1, 1, 0, 0, 2, 1, 2, 2, 2, 0, 0)),
    //        Arguments.of("bleh@absmartly.com", listOf(0, 1, 0, 0, 0, 0, 1, 0, 2, 0, 0, 0, 1, 1)),
    //        Arguments.of("e791e240fcd3df7d238cfc285f475e8152fcc0ec",
    //            listOf(1, 0, 1, 1, 0, 0, 0, 2, 0, 2, 1, 0, 0, 1)));
    //}
}