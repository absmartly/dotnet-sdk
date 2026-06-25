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

    public static object? ParseValue(string json)
    {
        try
        {
            return ParseJsonValue(json);
        }
        catch (Exception)
        {
            return null;
        }
    }
}