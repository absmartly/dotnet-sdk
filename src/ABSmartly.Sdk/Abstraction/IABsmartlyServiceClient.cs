using System.Threading.Tasks;
using ABSmartly.Models;

namespace ABSmartly;

public interface IABsmartlyServiceClient
{
    Task<ContextData> GetContextDataAsync();
    Task<bool> PublishAsync(PublishEvent publishEvent);
}