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
                var message = $"Failed to parse variant config for experiment '{experimentName}', variant '{variantName}' - result was null";
                _logger?.LogWarning(message);
                Console.Error.WriteLine($"[ABSmartly] WARNING: {message}");
            }
            return result;
        }
        catch (Exception e)
        {
            var message = $"Failed to parse variant config for experiment '{experimentName}', variant '{variantName}': {e.Message}";
            _logger?.LogError(e, message);
            Console.Error.WriteLine($"[ABSmartly] ERROR: {message}");
            return null;
        }
    }

    public static object ParseValue(string json)
    {
        try
        {
            return ParseJsonValue(json);
        }
        catch (Exception e)
        {
            return null;
        }
    }
}