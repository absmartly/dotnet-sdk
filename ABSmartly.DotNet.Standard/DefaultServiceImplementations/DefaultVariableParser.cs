using System;
using System.Collections.Generic;
using ABSmartlySdk.Interfaces;
using ABSmartlySdk.Utils.NewtonsoftJsonUtils;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace ABSmartlySdk.DefaultServiceImplementations;

public class DefaultVariableParser : IVariableParser
{
    private readonly ILogger<DefaultVariableParser> _logger;

    public DefaultVariableParser(ILoggerFactory loggerFactory = null)
    {
        _logger = loggerFactory?.CreateLogger<DefaultVariableParser>();
    }

    public Dictionary<string, object> Parse(Context context, string experimentName, string variantName, string config)
    {
        try
        {
            var jsonDict = JsonConvert.DeserializeObject<Dictionary<string, object>>(config);
            var parsedDict = JsonUtils.ParseJsonObject(jsonDict);
            return parsedDict as Dictionary<string, object>;
        }
        catch (Exception e)
        {
            _logger?.LogError(e.Message);
            return null;
        }
    }
}