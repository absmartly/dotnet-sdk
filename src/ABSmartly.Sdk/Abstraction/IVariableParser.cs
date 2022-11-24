using System.Collections.Generic;

namespace ABSmartly;

public interface IVariableParser
{
    Dictionary<string, object> Parse(IContext context, string experimentName, string variantName,
        string variableValue);
}