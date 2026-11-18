using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using ABSmartly.Models;

namespace ABSmartly.Services.Json;

// Reads ContextData.Holdouts. A value that is not an array is read as null (no holdouts), and an
// entry that is not an object, or does not decode as one (for example a string id), is skipped,
// so a malformed holdout only loses that holdout instead of failing the whole context.
internal sealed class HoldoutsConverter : JsonConverter<Experiment[]>
{
    public override Experiment[] Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.StartArray)
        {
            reader.Skip();
            return null;
        }

        var holdouts = new List<Experiment>();
        while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
        {
            if (reader.TokenType != JsonTokenType.StartObject)
            {
                reader.Skip();
                continue;
            }

            var element = JsonElement.ParseValue(ref reader);
            try
            {
                holdouts.Add(element.Deserialize<Experiment>(options));
            }
            catch (JsonException)
            {
            }
        }

        return holdouts.ToArray();
    }

    public override void Write(Utf8JsonWriter writer, Experiment[] value, JsonSerializerOptions options)
    {
        JsonSerializer.Serialize(writer, value, options);
    }
}
