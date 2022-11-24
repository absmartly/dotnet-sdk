using System.Threading.Tasks;
using ABSmartly.Models;

namespace ABSmartly;

public interface IContextEventHandler
{
    Task PublishAsync(IContext context, PublishEvent publishEvent);
}