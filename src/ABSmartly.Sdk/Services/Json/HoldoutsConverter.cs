using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using ABSmartly.Models;

namespace ABSmartly.Services.Json;

// Reads ContextData.Holdouts. A value that is not an array is read as null (no holdouts), and an
// entry that is not an object, or does not decode as one (for example a string id), is skipped,
// so a malformed holdout only loses that holdout instead of failing the whole context.
// Only the holdout fields (JS HoldoutData) are read: experiment-only keys such as variants or
// trafficSplit are ignored like unknown keys, whatever their type, and a name, unitType or
// holdoutType that is not a string is read as null.
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
                holdouts.Add(element.Deserialize<HoldoutData>(options).ToExperiment());
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

    private sealed class HoldoutData
    {
        public int Id { get; set; }
        [JsonConverter(typeof(LenientStringConverter))]
        public string Name { get; set; }
        [JsonConverter(typeof(LenientStringConverter))]
        public string UnitType { get; set; }
        public int Iteration { get; set; }
        public int SeedHi { get; set; }
        public int SeedLo { get; set; }
        public double[] Split { get; set; }
        [JsonConverter(typeof(LenientStringConverter))]
        public string HoldoutType { get; set; }

        public Experiment ToExperiment() => new()
        {
            Id = Id,
            Name = Name,
            UnitType = UnitType,
            Iteration = Iteration,
            SeedHi = SeedHi,
            SeedLo = SeedLo,
            Split = Split,
            HoldoutType = HoldoutType
        };
    }
}
