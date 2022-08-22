using ABSmartlySdk.Interfaces;
using ABSmartlySdk.Json;

namespace ABSmartlyDotNetExamples.Blazor.Shared.ABSmartlyServiceImplementations;

public class ExampleContextDataProvider : IContextDataProvider
{
    public async Task<ContextData> GetContextDataAsync()
    {
        await Task.Delay(0);
        return new ContextData(
            new Experiment[]
            {

            }
        );
    }
}