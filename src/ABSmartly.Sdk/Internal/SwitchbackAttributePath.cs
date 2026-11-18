using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using ABSmartly.Services.Json;

namespace ABSmartly.Internal;

// A switchback cluster attribute path, resolved exactly like the Collector's
// SwitchbackAttributePath (ported from java-sdk): "/" separates segments, a segment wrapped in ' or "
// is a literal key, and an unquoted segment that Guava Ints.tryParse accepts (ASCII digits, optional
// leading '-') indexes an array and nothing else. Every other segment reads an object key. A path is
// malformed, and its experiment unservable, when a quote is left unterminated or any segment key,
// quoted or not, contains a '.' (dotted paths are not a nesting syntax; nesting is written with '/').
internal sealed class SwitchbackAttributePath
{
    private readonly string[] _keys;
    // Parallel to _keys; null where the segment is an object key.
    private readonly int?[] _indices;

    private SwitchbackAttributePath(string[] keys, int?[] indices)
    {
        _keys = keys;
        _indices = indices;
    }

    // The attribute name the path starts at; a supported path always starts at a key segment.
    public string RootKey => _keys[0];

    // Null for an unsupported path: malformed (see above), empty, or an array index that is
    // negative, first, last, or directly after another index. Analysis cannot read those.
    public static SwitchbackAttributePath ParseSupported(string path)
    {
        if (path == null) return null;

        var parsed = Parse(path);
        if (parsed == null) return null;

        var n = parsed._keys.Length;
        if (n == 0) return null;

        for (var i = 0; i < n; ++i)
        {
            var index = parsed._indices[i];
            if (index != null && (index < 0 || i == 0 || i == n - 1 || parsed._indices[i - 1] != null))
                return null;
        }

        return parsed;
    }

    // The value at the path below RootKey in an SDK attribute value, read as the Collector reads
    // the published attribute: through its JSON encoding, with the publish serializer's naming
    // policies (so a dictionary key is read under the name it is published with). A string,
    // boolean, integer or floating-point root value is returned as is (a char as a string); anything
    // else is encoded and the result is a JsonElement. A double or float is encoded with a fraction
    // even when it is whole (3.0, where the serializer writes 3), so it stays a float wherever it
    // sits and never keys a cluster. Null when the path finds nothing, or the value has no JSON
    // encoding (a cycle, nesting deeper than the serializer's MaxDepth, or a type it rejects).
    public object ResolveAttribute(object rootValue)
    {
        if (rootValue == null) return null;

        if (rootValue is char c) rootValue = c.ToString();
        if (IsScalar(rootValue)) return _keys.Length == 1 ? rootValue : null;

        JsonElement element;
        try
        {
            element = JsonSerializer.SerializeToElement(rootValue, EncodingOptions.Value);
        }
        catch (Exception e) when (e is JsonException || e is NotSupportedException || e is InvalidOperationException ||
                                  e is ArgumentException)
        {
            return null;
        }

        for (var i = 1; i < _keys.Length; ++i)
        {
            if (!Step(ref element, _keys[i], _indices[i])) return null;
        }

        return element.ValueKind == JsonValueKind.Null ? null : element;
    }

    // Resolves the whole path, root segment included, in a decoded JSON object; for the shared
    // attribute-path cases, which are written as one JSON object of attributes.
    internal object Resolve(JsonElement attributes)
    {
        var element = attributes;
        for (var i = 0; i < _keys.Length; ++i)
        {
            if (!Step(ref element, _keys[i], _indices[i])) return null;
        }

        return element.ValueKind == JsonValueKind.Null ? null : element;
    }

    private static bool IsScalar(object value) =>
        value is string || value is bool ||
        value is sbyte || value is byte || value is short || value is ushort ||
        value is int || value is uint || value is long || value is ulong ||
        value is double || value is float;

    private static bool Step(ref JsonElement current, string key, int? index)
    {
        if (index != null)
        {
            if (current.ValueKind != JsonValueKind.Array || index.Value >= current.GetArrayLength()) return false;
            current = current[index.Value];
            return true;
        }

        if (current.ValueKind != JsonValueKind.Object) return false;

        // The last of duplicate keys wins, as a decoded JSON map.
        var found = false;
        foreach (var property in current.EnumerateObject())
        {
            if (!string.Equals(property.Name, key, StringComparison.Ordinal)) continue;
            current = property.Value;
            found = true;
        }

        return found;
    }

    // Null when the path is malformed. A '.' anywhere, quoted or not, is unreadable: the Collector's
    // SwitchbackAttributePath rejects any parsed segment key containing '.', so "geo.city",
    // "'geo.city'" and "\"geo.city\"" are alike unreadable cluster paths.
    private static SwitchbackAttributePath Parse(string path)
    {
        if (path.IndexOf('.') >= 0) return null;

        var keys = new List<string>();
        var indices = new List<int?>();
        var i = 0;
        while (i < path.Length)
        {
            if (path[i] == '/')
            {
                ++i;
                continue;
            }

            var first = path[i];
            if (first == '\'' || first == '"')
            {
                var end = path.IndexOf(first, i + 1);
                if (end < 0) return null;
                keys.Add(path.Substring(i + 1, end - i - 1));
                indices.Add(null);
                i = end + 1;
            }
            else
            {
                var end = path.IndexOf('/', i);
                var stop = end < 0 ? path.Length : end;
                var key = path.Substring(i, stop - i);
                keys.Add(key);
                indices.Add(TryParseInt(key));
                i = stop;
            }
        }

        return new SwitchbackAttributePath(keys.ToArray(), indices.ToArray());
    }

    // Guava Ints.tryParse(String): optional '-', then one or more ASCII digits, within int range;
    // otherwise null. "+1" and non-ASCII digits are not numbers; "-0" is 0.
    internal static int? TryParseInt(string s)
    {
        var length = s.Length;
        var negative = length > 0 && s[0] == '-';
        var i = negative ? 1 : 0;
        if (i == length) return null;

        long magnitude = 0;
        for (; i < length; ++i)
        {
            var c = s[i];
            if (c < '0' || c > '9') return null;
            magnitude = magnitude * 10 + (c - '0');
            if (magnitude > 1L << 31) return null;
        }

        var value = negative ? -magnitude : magnitude;
        return value > int.MaxValue ? null : (int)value;
    }

    // The publish serializer's options, plus converters that keep a whole double or float a float.
    private static readonly Lazy<JsonSerializerOptions> EncodingOptions = new(() =>
    {
        var options = new JsonSerializerOptions(JsonOptionsProvider.Default.SerializerOptions);
        options.Converters.Add(new FloatingPointConverter<double>(v => v));
        options.Converters.Add(new FloatingPointConverter<float>(v => v));
        return options;
    });

    private sealed class FloatingPointConverter<T> : JsonConverter<T>
    {
        private readonly Func<T, double> _toDouble;

        public FloatingPointConverter(Func<T, double> toDouble)
        {
            _toDouble = toDouble;
        }

        public override T Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
            throw new NotSupportedException();

        public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options)
        {
            var d = _toDouble(value);
            if (double.IsNaN(d) || double.IsInfinity(d))
            {
                writer.WriteNullValue();
                return;
            }

            var text = d.ToString("R", CultureInfo.InvariantCulture);
            if (text.IndexOfAny(FloatMarkers) < 0) text += ".0";
            writer.WriteRawValue(text, skipInputValidation: true);
        }

        private static readonly char[] FloatMarkers = { '.', 'e', 'E' };
    }
}
