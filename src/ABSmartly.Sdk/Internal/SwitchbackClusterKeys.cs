using System.Globalization;
using System.Numerics;
using System.Text.Json;

namespace ABSmartly.Internal;

// Port of the Collector's SwitchbackClusterKeys.normalize (via java-sdk): the cluster key an
// attribute value yields, or null when it yields none. A non-empty string is the key as is; a whole
// number of an integer type (or an integer JSON number) is its base-10 text. Null, empty strings,
// booleans, every floating-point number (3.0 included), objects and arrays yield no key. The key is
// passed to SwitchbackAssigner unhashed.
internal static class SwitchbackClusterKeys
{
    public static string Normalize(object value)
    {
        switch (value)
        {
            case string s:
                return s.Length == 0 ? null : s;
            case sbyte or byte or short or ushort or int or uint or long:
                return System.Convert.ToInt64(value, CultureInfo.InvariantCulture).ToString(CultureInfo.InvariantCulture);
            case ulong u:
                return u.ToString(CultureInfo.InvariantCulture);
            // As java-sdk; reached only by a caller-built value: an attribute is read through its JSON
            // encoding, which writes a BigInteger as an object (no key) and a big integer token as
            // a JsonElement (keyed below).
            case BigInteger b:
                return b.ToString(CultureInfo.InvariantCulture);
            case JsonElement element:
                return Normalize(element);
            default:
                return null;
        }
    }

    private static string Normalize(JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.String:
                var s = element.GetString();
                return string.IsNullOrEmpty(s) ? null : s;
            case JsonValueKind.Number:
                // An integer token is a whole number; a fraction or exponent makes it a float, as
                // Jackson decodes it, even when the value is whole (3.0, 1e2).
                var raw = element.GetRawText();
                if (raw.IndexOfAny(FloatMarkers) >= 0) return null;
                return BigInteger.Parse(raw, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture)
                    .ToString(CultureInfo.InvariantCulture);
            default:
                return null;
        }
    }

    private static readonly char[] FloatMarkers = { '.', 'e', 'E' };
}
