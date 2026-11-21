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
        if (TryRead(ref reader, out var value)) return value;
        throw new JsonException("Expected a 32-bit integer.");
    }

    // A number token that is a whole 32-bit integer, written as an integer or an integral float.
    // Shared with HoldoutIdsConverter, which skips what this rejects instead of throwing.
    internal static bool TryRead(ref Utf8JsonReader reader, out int value)
    {
        value = 0;
        if (reader.TokenType != JsonTokenType.Number) return false;
        if (reader.TryGetInt32(out value)) return true;
        if (reader.TryGetDouble(out var number) && number == Math.Floor(number) &&
            number >= int.MinValue && number <= int.MaxValue)
        {
            value = (int)number;
            return true;
        }

        return false;
    }

    public override void Write(Utf8JsonWriter writer, int value, JsonSerializerOptions options)
    {
        writer.WriteNumberValue(value);
    }
}
