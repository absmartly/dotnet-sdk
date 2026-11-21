using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ABSmartly.Services.Json;

// Reads Experiment.HoldoutIds. Elements that are not 32-bit integers (null, strings, fractions,
// objects) are skipped, and a value that is not an array is read as null (no holdouts), so a
// malformed reference only loses that reference instead of failing the whole context.
internal sealed class HoldoutIdsConverter : JsonConverter<int[]>
{
    public override int[] Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.StartArray)
        {
            reader.Skip();
            return null;
        }

        var ids = new List<int>();
        while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
        {
            if (IntegralInt32Converter.TryRead(ref reader, out var id))
                ids.Add(id);
            else
                reader.Skip();
        }

        return ids.ToArray();
    }

    public override void Write(Utf8JsonWriter writer, int[] value, JsonSerializerOptions options)
    {
        writer.WriteStartArray();
        foreach (var id in value) writer.WriteNumberValue(id);
        writer.WriteEndArray();
    }
}
