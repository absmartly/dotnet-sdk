using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Nodes;
using ABSmartly.Interfaces;
using Microsoft.Extensions.Logging;

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


            var jsonNode = JsonNode.Parse(config);
            //var array = jsonNode!.AsArray();

            var resultDict = new Dictionary<string, object>();




            var rerr = jsonNode.Deserialize<Dictionary<string, object>>();
            //foreach (var VARIABLE in result.Deserialize<>())
            //{
                
            //}



            var data2 = JsonSerializer.Deserialize<JsonNode>(config);

            
            var data22 = JsonSerializer.Deserialize<Dictionary<string, dynamic>>(config);
            var data = JsonSerializer.Deserialize<Dictionary<string, object>>(config);
            return data;
        }
        catch (Exception e)
        {
            _logger?.LogError(e.Message);
            return null;
        }
    }
}