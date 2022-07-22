using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;

namespace ABSmartly;

public class DefaultVariableParser : IVariableParser
{
    private readonly ILogger<DefaultVariableParser> _logger;

    // Todo: add the com.fasterxml.jackson.databind C# (Jackson.Core?) package???? or use simple JSON

    public DefaultVariableParser(ILogger<DefaultVariableParser> logger)
    {
        _logger = logger;
    }

    public Dictionary<string, object> Parse(Context context, string experimentName, string variantName, string variableValue)
    {
        try
        {
            throw new NotImplementedException();
        }
        catch (Exception e)
        {
            _logger.LogError(e.Message);
            return null;
        }
    }
}