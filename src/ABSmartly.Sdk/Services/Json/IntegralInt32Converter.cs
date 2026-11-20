using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ABSmartly.Services.Json;

// Reads a holdout's id, iteration or seed. An integral float such as 11.0 is the same number, as
// in JavaScript (and HoldoutIdsConverter); null, a fraction or a value outside the 32-bit range
// throws, so HoldoutsConverter drops that holdout.
internal sealed class IntegralInt32Converter : JsonConverter<int>
{
    public override int Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Number)
        {
            if (reader.TryGetInt32(out var value)) return value;
            if (reader.TryGetDouble(out var number) && number == Math.Floor(number) &&
                number >= int.MinValue && number <= int.MaxValue)
                return (int)number;
        }

        throw new JsonException("Expected a 32-bit integer.");
    }

    public override void Write(Utf8JsonWriter writer, int value, JsonSerializerOptions options)
    {
        writer.WriteNumberValue(value);
    }
}
