﻿namespace ABSmartly.DotNet.Standard.UnitTests.TestUtils;

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
}