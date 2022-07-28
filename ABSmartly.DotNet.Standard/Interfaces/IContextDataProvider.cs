using System.Threading.Tasks;
using ABSmartly.Json;

namespace ABSmartly.Interfaces;

public interface IContextDataProvider
{
    Task<ContextData> GetContextDataAsync();
}