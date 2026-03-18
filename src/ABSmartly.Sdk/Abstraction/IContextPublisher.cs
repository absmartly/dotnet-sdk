using System.Threading.Tasks;
using ABSmartly.Models;

namespace ABSmartly;

public interface IContextPublisher
{
    Task PublishAsync(IContext context, PublishEvent publishEvent);
}
