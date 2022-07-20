using System.Threading.Tasks;
using ABSmartly.Json;

namespace ABSmartly;

public interface IContextEventHandler
{
    Task Publish(Context context, PublishEvent publishEvent);
}