using System;
using System.Collections.Generic;
//using System.Text.Json;
//using System.Text.Json.Nodes;
using ABSmartly.Interfaces;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace ABSmartly.DefaultServiceImplementations;

public class DefaultVariableParser : IVariableParser
{
    private readonly ILogger<DefaultVariableParser> _logger;

    // Todo: add the com.fasterxml.jackson.databind C# (Jackson.Core?) package???? or use simple JSON

    public DefaultVariableParser(ILoggerFactory loggerFactory = null)
    {
        _logger = loggerFactory?.CreateLogger<DefaultVariableParser>();
    }

    public Dictionary<string, object> Parse(Context context, string experimentName, string variantName, string config)
    {
        try
        {
            var data22 = JsonConvert.DeserializeObject<Dictionary<string, dynamic>>(config);
            return data22;
        }
        catch (Exception e)
        {
            _logger?.LogError(e.Message);
            return null;
        }
    }
}