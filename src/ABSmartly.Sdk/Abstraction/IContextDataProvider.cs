using System.Threading.Tasks;
using ABSmartly.Models;

namespace ABSmartly;

public interface IContextDataProvider
{
    Task<ContextData> GetContextDataAsync();
}