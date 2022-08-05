using System.Collections.ObjectModel;
// ReSharper disable RedundantExplicitArrayCreation

namespace ABSmartly.DotNet.Standard.UnitTests.TestUtils;

public class TestCases
{
    public static object[] ObjectArrayOfBoolWithNull = 
    {
        new object[] { true },
        new object[] { false },
        new object[] { null },
        new object[] { true, true },
        new object[] { true, false },
        new object[] { false, true },
        new object[] { false, false },
        new object[] { true, null },
        new object[] { false, null },
        new object[] { true, false, true },
        new object[] { false, true, false },
        new object[] { null, true, false },
        new object[] { null, true, null },
        new object[] { false, null, false },
    };

    public static IEnumerable<object> RandomNotIListValues()
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
        yield return new List<int> { 2 };
        yield return new Collection<bool> { false };
    }

    public static IEnumerable<object> RandomOneElementIListWithoutArrayValues()
    {
        yield return new List<string> { "test" };
        yield return new List<int> { 2 };
        yield return new Collection<bool> { false };
    }

    public static IEnumerable<object> RandomTwoOrMoreElementIListValues()
    {
        yield return new object[] { 1, false, "random" };
        yield return new bool[] { true, false };
        yield return new string[] { "test 1", "test 2" };
        yield return new List<int> { 2, 2 };
        yield return new Collection<bool> { false, true, false };
    }

    public static IEnumerable<object> NonStringOrIListOrIDictionary()
    {
        yield return 1;
        yield return (byte)0;
        yield return 3.14;
        yield return false;
        yield return 'c';
        yield return new Exception("test");
    }

    public static IEnumerable<object> StringsWithEmpty()
    {
        yield return "";
        yield return "1";
        yield return "a";
        yield return "c";
        yield return "sm";
        yield return "test";
    }

    public static IEnumerable<object[]> StringObject()
    {
        yield return new object[] { "test123", "t" };
        yield return new object[] { "test123", "st" };
        yield return new object[] { "test123", "12" };
        yield return new object[] { "test123", 12 };
        yield return new object[] { "test123", 't' };
        yield return new object[] { "test123", '1' };
        yield return new object[] { "test123", "bla" };
        yield return new object[] { "test123", "56" };
        yield return new object[] { "test123", 56 };

    }
}