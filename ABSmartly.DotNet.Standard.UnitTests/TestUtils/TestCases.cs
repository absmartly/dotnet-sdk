using System.Collections.ObjectModel;
using Microsoft.VisualBasic;

namespace ABSmartly.DotNet.Standard.UnitTests.TestUtils;

public class TestCases
{
    public static object?[] ObjectArrayOfBoolWithNull = 
    {
        new object?[] { true },
        new object?[] { false },
        new object?[] { null },
        new object?[] { true, true },
        new object?[] { true, false },
        new object?[] { false, true },
        new object?[] { false, false },
        new object?[] { true, null },
        new object?[] { false, null },
        new object?[] { true, false, true },
        new object?[] { false, true, false },
        new object?[] { null, true, false },
        new object?[] { null, true, null },
        new object?[] { false, null, false },
    };

    public static IEnumerable<object?> RandomNotIListValues()
    {
        yield return null;
        yield return "";
        yield return "random text";
        yield return true;
        yield return false;
        yield return -10;
        yield return 0;
        yield return 10;
        yield return (float)5;
        yield return (double)5;
        yield return (decimal)5;
    }

    //public static IEnumerable<object> RandomIListValues()
    //{
    //    yield return new bool[] { };
    //    yield return new string[] { };
    //    yield return new List<int>();
    //    yield return new Collection<bool>();

    //    yield return new bool[] { true };
    //    yield return new string[] { "test" };
    //    yield return new List<int>() { 2 };
    //    yield return new Collection<bool>() { false };

    //    yield return new object?[] { 1, false, "random" };
    //    yield return new bool[] { true, false };
    //    yield return new string[] { "test 1", "test 2" };
    //    yield return new List<int>() { 2, 2 };
    //    yield return new Collection<bool>() { false, true, false };
    //}

    public static IEnumerable<object> RandomEmptyIListValues()
    {
        yield return new bool[] { };
        yield return new string[] { };
        yield return new List<int>();
        yield return new Collection<bool>();
    }

    public static IEnumerable<object> RandomOneElementIListValues()
    {
        yield return new bool[] { true };
        yield return new string[] { "test" };
        yield return new List<int>() { 2 };
        yield return new Collection<bool>() { false };
    }

    public static IEnumerable<object> RandomOneElementIListWithoutArrayValues()
    {
        yield return new List<string>() { "test" };
        yield return new List<int>() { 2 };
        yield return new Collection<bool>() { false };
    }

    public static IEnumerable<object> RandomTwoOrMoreElementIListValues()
    {
        yield return new object?[] { 1, false, "random" };
        yield return new bool[] { true, false };
        yield return new string[] { "test 1", "test 2" };
        yield return new List<int>() { 2, 2 };
        yield return new Collection<bool>() { false, true, false };
    }

    //public static IEnumerable<object> 
}