using System.Collections.Generic;

namespace ABSmartlySdk.Interfaces;

public interface IVariableParser
{
    Dictionary<string, object> Parse(Context context, string experimentName, string variantName, string variableValue);
}