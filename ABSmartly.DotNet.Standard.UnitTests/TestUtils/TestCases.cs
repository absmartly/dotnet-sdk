namespace ABSmartly.DotNet.Standard.UnitTests.TestUtils;

public class TestCases
{
    //public static List<object> ObjectListOfBoolWithNull = new()
    //{
    //    new List<object> { true },
    //    new List<object> { false },
    //    new List<object> { null },
    //    new List<object> { true, true },
    //    new List<object> { true, false },
    //    new List<object> { false, true },
    //    new List<object> { false, false },
    //    new List<object> { true, null },
    //    new List<object> { false, null },
    //    new List<object> { true, false, true },
    //    new List<object> { false, true, false },
    //    new List<object> { false, true, null },
    //    new List<object> { false, null, false },
    //};

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
}