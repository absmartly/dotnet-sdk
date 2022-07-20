using System.Threading.Tasks;
using ABSmartly.Json;

namespace ABSmartly;

public interface IContextDataProvider
{
    Task<ContextData> GetContextDataAsync();
}