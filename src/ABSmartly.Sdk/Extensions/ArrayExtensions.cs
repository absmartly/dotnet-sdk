﻿namespace ABSmartly.Extensions;

public static class ArrayExtensions
{
    public static string ToArrayString<T>(this T[] array)
    {
        return array is null ? string.Empty : string.Join(",", array);
    }
}