using System.Threading.Tasks;
using ABSmartly.Json;

namespace ABSmartly.Interfaces;

public interface IClient
{
    Task<ContextData> GetContextDataAsync();
    Task<bool> PublishAsync(PublishEvent publishEvent);
}