using System.Threading.Tasks;
using ABSmartlySdk.Json;

namespace ABSmartlySdk.Interfaces;

public interface IContextDataProvider
{
    Task<ContextData> GetContextDataAsync();
}