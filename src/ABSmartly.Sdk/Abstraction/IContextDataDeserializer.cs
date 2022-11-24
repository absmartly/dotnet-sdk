using System.IO;
using ABSmartly.Models;

namespace ABSmartly;

public interface IContextDataDeserializer
{
    ContextData Deserialize(Stream stream);
}