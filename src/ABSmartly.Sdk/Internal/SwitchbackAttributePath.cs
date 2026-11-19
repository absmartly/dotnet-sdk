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
    private readonly int?[] _indices;

    private SwitchbackAttributePath(string[] keys, int?[] indices)
    {
        _keys = keys;
        _indices = indices;
    }

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
    // the published attribute: through its wire encoding (WireOptions, the JSON the context
    // publishes), so a dictionary key is read under the name it is published with, a whole double
    // stays a float and an enum value is its name. A string, boolean, integer or
    // floating-point root value is returned as is (a char as a string); anything else is encoded and
    // the result is a JsonElement. Null when the path finds nothing, or the value has no JSON
    // encoding (a cycle, nesting deeper than the serializer's MaxDepth, or a type it rejects).
    public object ResolveAttribute(object rootValue)
    {
        if (rootValue == null) return null;

        if (rootValue is char c) rootValue = c.ToString();
        if (IsScalar(rootValue)) return _keys.Length == 1 ? rootValue : null;

        JsonElement element;
        try
        {
            element = JsonSerializer.SerializeToElement(rootValue, WireOptions.Value);
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

    // Java String.trim(), as the Collector trims attribute names: only characters at or below
    // U+0020 are removed from either end (.NET Trim() also removes Unicode white space, e.g. U+00A0).
    internal static string JavaTrim(string s)
    {
        int start = 0, end = s.Length;
        while (start < end && s[start] <= ' ') start++;
        while (end > start && s[end - 1] <= ' ') end--;
        return start == 0 && end == s.Length ? s : s.Substring(start, end - start);
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

    // The JSON a switchback context publishes its attributes as, and so the JSON both the cluster
    // lookup and the Collector read the cluster from: the publish serializer's options, except that
    // a floating-point number (double, float, Half) is written with a fraction even when it is whole
    // (3.0, where the serializer writes 3) and as null when it is NaN or infinite, so it never keys a
    // cluster and never fails the batch; and an enum value is written as its name, as java-sdk's
    // Jackson writes it. Dictionary keys are written exactly as the publish serializer writes them.
    internal static readonly Lazy<JsonSerializerOptions> WireOptions = new(() =>
    {
        var options = new JsonSerializerOptions(JsonOptionsProvider.Default.SerializerOptions);
        options.Converters.Add(new FloatingPointConverter<double>(v => (double)v));
        options.Converters.Add(new FloatingPointConverter<float>(v => (float)v,
            v => ((float)v).ToString("R", CultureInfo.InvariantCulture)));
        // System.Half, by name: the netstandard2.0 build (used on net6.0 and .NET Framework) does
        // not see the type, and must still treat it as a float.
        options.Converters.Add(new HalfAsFloatConverterFactory());
        options.Converters.Add(new EnumAsNameConverterFactory());
        return options;
    });

    private sealed class EnumAsNameConverterFactory : JsonConverterFactory
    {
        public override bool CanConvert(Type typeToConvert) => typeToConvert.IsEnum;

        public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options) =>
            (JsonConverter)Activator.CreateInstance(typeof(EnumAsNameConverter<>).MakeGenericType(typeToConvert));
    }

    private sealed class EnumAsNameConverter<T> : JsonConverter<T>
    {
        public override T Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
            throw new NotSupportedException();

        public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options) =>
            writer.WriteStringValue(value!.ToString());

        public override void WriteAsPropertyName(Utf8JsonWriter writer, T value, JsonSerializerOptions options) =>
            ((JsonConverter<T>)JsonOptionsProvider.Default.SerializerOptions.GetConverter(typeof(T)))
            .WriteAsPropertyName(writer, value, options);
    }

    // The publish serializer's options with attribute values written in the wire encoding, used by
    // DefaultContextEventSerializer for a context with switchbacks: everything but the attribute
    // values serializes as before.
    internal static readonly Lazy<JsonSerializerOptions> PublishOptions = new(() =>
    {
        var options = new JsonSerializerOptions(JsonOptionsProvider.Default.SerializerOptions);
        options.Converters.Add(new WireAttributeConverter());
        return options;
    });

    private sealed class WireAttributeConverter : JsonConverter<Models.Attribute>
    {
        public override Models.Attribute Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
            throw new NotSupportedException();

        public override void Write(Utf8JsonWriter writer, Models.Attribute value, JsonSerializerOptions options)
        {
            var naming = options.PropertyNamingPolicy;
            writer.WriteStartObject();
            if (value.Name != null) writer.WriteString(naming.ConvertName(nameof(Models.Attribute.Name)), value.Name);
            if (value.Value != null)
            {
                writer.WritePropertyName(naming.ConvertName(nameof(Models.Attribute.Value)));
                JsonSerializer.Serialize(writer, value.Value, WireOptions.Value);
            }

            writer.WriteNumber(naming.ConvertName(nameof(Models.Attribute.SetAt)), value.SetAt);
            writer.WriteEndObject();
        }
    }

    private sealed class HalfAsFloatConverterFactory : JsonConverterFactory
    {
        public override bool CanConvert(Type typeToConvert) => typeToConvert.FullName == "System.Half";

        public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options) =>
            (JsonConverter)Activator.CreateInstance(typeof(FloatingPointConverter<>).MakeGenericType(typeToConvert),
                (Func<object, double>)(v => double.Parse(Text(v), NumberStyles.Float, CultureInfo.InvariantCulture)),
                (Func<object, string>)Text);

        private static string Text(object v) => ((IFormattable)v).ToString("R", CultureInfo.InvariantCulture);
    }

    private sealed class FloatingPointConverter<T> : JsonConverter<T>
    {
        private readonly Func<object, double> _toDouble;
        // The shortest round-trip text of the value in its own type (a float as "0.1", not the
        // double "0.10000000149011612"), as the publish serializer writes it.
        private readonly Func<object, string> _toText;

        public FloatingPointConverter(Func<object, double> toDouble, Func<object, string> toText = null)
        {
            _toDouble = toDouble;
            _toText = toText ?? (v => toDouble(v).ToString("R", CultureInfo.InvariantCulture));
        }

        public override T Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
            throw new NotSupportedException();

        public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options)
        {
            var d = _toDouble(value!);
            if (double.IsNaN(d) || double.IsInfinity(d))
            {
                writer.WriteNullValue();
                return;
            }

            var text = _toText(value!);
            if (text.IndexOfAny(FloatMarkers) < 0) text += ".0";
            writer.WriteRawValue(text, skipInputValidation: true);
        }

        public override void WriteAsPropertyName(Utf8JsonWriter writer, T value, JsonSerializerOptions options) =>
            Default(options).WriteAsPropertyName(writer, value, options);

        private static JsonConverter<T> Default(JsonSerializerOptions options) =>
            (JsonConverter<T>)JsonOptionsProvider.Default.SerializerOptions.GetConverter(typeof(T));

        private static readonly char[] FloatMarkers = { '.', 'e', 'E' };
    }
}
