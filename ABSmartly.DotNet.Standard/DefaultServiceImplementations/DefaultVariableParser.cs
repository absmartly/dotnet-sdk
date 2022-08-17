using System;
using System.Collections.Generic;
using ABSmartly.Interfaces;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

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
            var jsonDict = JsonConvert.DeserializeObject<Dictionary<string, object>>(config);

            var jArrayDict = new Dictionary<string, object>();
            var jObjectDict = new Dictionary<string, Dictionary<string, object>>();

            foreach (var kvp in jsonDict!)
            {
                if (kvp.Value is JArray jArray)
                {
                    var list = new List<object>();

                    foreach (var jItem in jArray)
                    {
                        list.Add(jItem.ToObject<object>());
                    }

                    jArrayDict.Add(kvp.Key, list);
                }

                else if (kvp.Value is JObject jObject)
                {

                    var dict = new Dictionary<string, object>();
                    foreach (var jObjectKvP in jObject)
                    {
                        dict.Add(jObjectKvP.Key, jObjectKvP.Value.ToObject<object>());
                    }

                    jObjectDict.Add(kvp.Key, dict);
                }
            }

            foreach (var kvp in jArrayDict)
            {
                jsonDict[kvp.Key] = kvp.Value;
            }

            foreach (var kvp in jObjectDict)
            {
                jsonDict[kvp.Key] = kvp.Value;
            }

            return jsonDict;
        }
        catch (Exception e)
        {
            _logger?.LogError(e.Message);
            return null;
        }
    }
}