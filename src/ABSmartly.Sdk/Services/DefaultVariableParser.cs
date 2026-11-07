#nullable enable
using System;
using System.Collections.Generic;
using ABSmartly.Services.Json;
using Microsoft.Extensions.Logging;

namespace ABSmartly.Services;

public class DefaultVariableParser : JsonParserBase, IVariableParser
{
    private readonly ILogger<DefaultVariableParser> _logger;

    public DefaultVariableParser(ILoggerFactory loggerFactory = null)
    {
        _logger = loggerFactory?.CreateLogger<DefaultVariableParser>();
    }

    public Dictionary<string, object> Parse(IContext context, string experimentName, string variantName,
        string config)
    {
        try
        {
            var result = ParseJsonString(config);
            if (result == null)
            {
                _logger?.LogWarning("Failed to parse variant config for experiment '{ExperimentName}', variant '{VariantName}' - result was null", experimentName, variantName);
            }
            return result;
        }
        catch (Exception e)
        {
            _logger?.LogError(e, "Failed to parse variant config for experiment '{ExperimentName}', variant '{VariantName}': {Message}", experimentName, variantName, e.Message);
            return null;
        }
    }

    public static object? ParseValue(string json) => ParseJsonValue(json);

    // Full JSON value for a custom field, mirroring the JavaScript SDK
    // (context.ts _customFieldValue): objects, arrays, numbers, strings and
    // booleans are all supported, not just objects. "null" -> null, "" -> "",
    // and a parse error logs a warning and yields null. This is the default
    // parser's path for JSON custom fields; variant-config parsing (Parse) is
    // unchanged.
    public object? ParseCustomFieldValue(string experimentName, string fieldName, string value)
    {
        if (value == "null")
            return null;

        if (value == "")
            return "";

        try
        {
            return ParseJsonValue(value);
        }
        catch (Exception e)
        {
            _logger?.LogWarning(e, "Failed to parse JSON custom field value '{FieldName}' for experiment '{ExperimentName}': {Message}",
                fieldName, experimentName, e.Message);
            return null;
        }
    }
}