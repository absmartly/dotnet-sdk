namespace ABSmartlySdk.Utils.Extensions;

public static class ArrayExtensions
{
    public static string ToArrayString<T>(this T[] array)
    {
        var text = string.Empty;

        if (array is null)
            return text;

        foreach (var item in array)
        {
            text += item.ToString();
            text += ",";
        }

        text = text.TrimEnd(',');

        return text;
    }
}