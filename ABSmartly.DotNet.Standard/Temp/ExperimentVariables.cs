using System.Collections.Generic;
using ABSmartly.Json;

namespace ABSmartly.Temp;

internal class ExperimentVariables
{
    public Experiment Data { get; set; }
    public List<Dictionary<string, object>> Variables { get; set; }
}