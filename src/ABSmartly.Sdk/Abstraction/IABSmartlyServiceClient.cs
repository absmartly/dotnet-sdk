using System.Threading.Tasks;
using ABSmartly.Models;

namespace ABSmartly;

public interface IABSmartlyServiceClient
{
    Task<ContextData> GetContextDataAsync();
    Task<bool> PublishAsync(PublishEvent publishEvent);
}