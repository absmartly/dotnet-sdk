using System.Collections.Generic;
using ABSmartlySdk.Json;

namespace ABSmartlySdk.Temp;

internal class ExperimentVariables
{
    public Experiment Data { get; set; }
    public List<Dictionary<string, object>> Variables { get; set; }
}