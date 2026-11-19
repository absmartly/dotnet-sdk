using System;
using System.Buffers;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ABSmartly.Services.Json;

// Reads Experiment.SwitchbackClusters in the order the object's properties are served: that order
// decides which unit serves a switchback, so the object is walked token by token and never decoded
// into a Dictionary (whose enumeration order is not guaranteed). A repeated key keeps its first
// position and takes the last value, as Jackson's LinkedHashMap does. A path that is not a string
// is kept as a null entry, which makes the map unreadable (the legacy path) rather than silently
// dropping that source; a value that is not an object is read as null.
internal sealed class SwitchbackClustersConverter : JsonConverter<KeyValuePair<string, string>[]>
{
    public override KeyValuePair<string, string>[] Read(ref Utf8JsonReader reader, Type typeToConvert,
        JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.StartObject)
        {
            reader.Skip();
            return null;
        }

        var clusters = new List<KeyValuePair<string, string>>();
        while (reader.Read() && reader.TokenType == JsonTokenType.PropertyName)
        {
            var unitType = reader.GetString();
            reader.Read();
            string path = null;
            if (reader.TokenType == JsonTokenType.String)
                path = reader.GetString();
            else
                reader.Skip();

            var at = clusters.FindIndex(c => c.Key == unitType);
            if (at < 0)
                clusters.Add(new KeyValuePair<string, string>(unitType, path));
            else
                clusters[at] = new KeyValuePair<string, string>(unitType, path);
        }

        return clusters.ToArray();
    }

    public override void Write(Utf8JsonWriter writer, KeyValuePair<string, string>[] value,
        JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        foreach (var cluster in value)
        {
            if (cluster.Value == null)
                writer.WriteNull(cluster.Key);
            else
                writer.WriteString(cluster.Key, cluster.Value);
        }

        writer.WriteEndObject();
    }
}

// Reads Experiment.SwitchbackClusterValues: an array of strings. A value that is not an array is
// read as null (no allow-list). As Jackson reads a String[], a number or boolean element is its JSON
// text ([7, 8] allows "7" and "8") and a null element is null. An object or array element, which
// Jackson rejects, is kept as a null entry: a non-empty list never becomes the empty, allow-all one.
internal sealed class LenientStringArrayConverter : JsonConverter<string[]>
{
    public override string[] Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.StartArray)
        {
            reader.Skip();
            return null;
        }

        var values = new List<string>();
        while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
        {
            switch (reader.TokenType)
            {
                case JsonTokenType.String:
                    values.Add(reader.GetString());
                    break;
                case JsonTokenType.Number:
                    values.Add(Encoding.UTF8.GetString(reader.HasValueSequence
                        ? reader.ValueSequence.ToArray()
                        : reader.ValueSpan.ToArray()));
                    break;
                case JsonTokenType.True:
                    values.Add("true");
                    break;
                case JsonTokenType.False:
                    values.Add("false");
                    break;
                case JsonTokenType.Null:
                    values.Add(null);
                    break;
                default:
                    reader.Skip();
                    values.Add(null);
                    break;
            }
        }

        return values.ToArray();
    }

    public override void Write(Utf8JsonWriter writer, string[] value, JsonSerializerOptions options)
    {
        writer.WriteStartArray();
        foreach (var item in value) writer.WriteStringValue(item);
        writer.WriteEndArray();
    }
}

// Reads the optional 64-bit switchback instants and durations. A whole number within the 64-bit
// range is read (an integral float such as 3.6e6 too, as Jackson coerces it); anything else, a
// fraction, a string, an object or an array, is read as null, so the schedule is unservable rather
// than the whole context failing.
internal sealed class LenientLongConverter : JsonConverter<long?>
{
    public override bool HandleNull => true;

    public override long? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.Number)
        {
            reader.Skip();
            return null;
        }

        if (reader.TryGetInt64(out var value)) return value;
        // 2^63 is the first double above long.MaxValue, so the range check is exact.
        if (reader.TryGetDouble(out var d) && d == Math.Floor(d) && d >= long.MinValue && d < 9223372036854775808.0)
            return (long)d;
        return null;
    }

    public override void Write(Utf8JsonWriter writer, long? value, JsonSerializerOptions options)
    {
        if (value == null)
            writer.WriteNullValue();
        else
            writer.WriteNumberValue(value.Value);
    }
}
