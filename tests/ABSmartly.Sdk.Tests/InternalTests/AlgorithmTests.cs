using ABSmartlySdk.Internal;
using Moq;

namespace ABSmartly.DotNet.Standard.UnitTests.InternalTests;

[TestFixture]
public class AlgorithmTests
{
    [TestCase(new[] { 1, 2, 3, 4, 5 }, new int[] { }, new[] { 1, 4, 9, 16, 25 })]
    public void MapSetToArray_SquareFunc_EmptyArray_Returns_ExpectedResult(int[] arrayset, int[] array, int[] expected)
    {
        var mock = new Mock<Func<int, int>>();
        mock.Setup(p => p(It.IsAny<int>()))
            .Returns(Square);

        var set = arrayset.ToHashSet();

        var actual = Algorithm.MapSetToArray(set, array, mock.Object);

        Assert.That(actual, Is.EqualTo(expected));
    }

    [TestCase(new[] { 1, 2, 3, 4, 5 }, new[] { 0, 0, 0, 0, 0 }, new[] { 1, 4, 9, 16, 25 })]
    public void MapSetToArray_SquareFunc_SameSizeZeroArray_Returns_ExpectedResult(int[] arrayset, int[] array, int[] expected)
    {
        var mock = new Mock<Func<int, int>>();
        mock.Setup(p => p(It.IsAny<int>()))
            .Returns(Square);

        var set = arrayset.ToHashSet();

        var actual = Algorithm.MapSetToArray(set, array, mock.Object);

        Assert.That(actual, Is.EqualTo(expected));
    }

    [TestCase(new[] { 1, 2, 3, 4, 5 }, new[] { 0, 0, 0, 0, 0, 0, 0 }, new[] { 1, 4, 9, 16, 25, 0, 0 })]
    public void MapSetToArray_SquareFunc_LargerSizeZeroArray_Returns_ExpectedResult(int[] arrayset, int[] array, int[] expected)
    {
        var mock = new Mock<Func<int, int>>();
        mock.Setup(p => p(It.IsAny<int>()))
            .Returns(Square);

        var set = arrayset.ToHashSet();

        var actual = Algorithm.MapSetToArray(set, array, mock.Object);

        Assert.That(actual, Is.EqualTo(expected));
    }

    #region Helper

    private static int Square(int a)
    {
        return a * a;
    }    

    #endregion
}