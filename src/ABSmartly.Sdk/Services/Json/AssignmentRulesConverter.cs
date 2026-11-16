using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ABSmartly.Services.Json;

// Reads Experiment.AssignmentRules, which the API sends as a JSON string. An object, array or any
// other value in its place is skipped and read as null (no rules), as the Java SDK, so one
// malformed experiment does not fail the whole context.
internal sealed class AssignmentRulesConverter : JsonConverter<string>
{
    public override string Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.String) return reader.GetString();

        reader.Skip();
        return null;
    }

    public override void Write(Utf8JsonWriter writer, string value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value);
    }
}
