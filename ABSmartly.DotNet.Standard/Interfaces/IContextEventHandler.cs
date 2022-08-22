using System.Threading.Tasks;
using ABSmartlySdk.Json;

namespace ABSmartlySdk.Interfaces;

public interface IContextEventHandler
{
    Task PublishAsync(Context context, PublishEvent publishEvent);
}