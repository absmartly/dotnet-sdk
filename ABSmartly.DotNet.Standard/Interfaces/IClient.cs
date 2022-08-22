using System.Threading.Tasks;
using ABSmartlySdk.Json;

namespace ABSmartlySdk.Interfaces;

public interface IClient
{
    Task<ContextData> GetContextDataAsync();
    Task<bool> PublishAsync(PublishEvent publishEvent);
}