namespace ABSmartly.Utils.Extensions;

public static class ArrayExtensions
{
    public static string ToArrayString<T>(this T[] array)
    {
        var text = string.Empty;

        foreach (var item in array)
        {
            text += item.ToString();
        }

        return text;
    }
}