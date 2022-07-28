using System.Threading.Tasks;
using ABSmartly.Json;

namespace ABSmartly.Interfaces;

public interface IContextEventHandler
{
    Task PublishAsync(Context context, PublishEvent publishEvent);
}