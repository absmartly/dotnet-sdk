using ABSmartly.Interfaces;
using ABSmartly.Json;

namespace ABSmartly.DefaultServiceImplementations;

public class DefaultContextDataDeserializer : IContextDataDeserializer
{


    public DefaultContextDataDeserializer()
    {

    }

    public ContextData Deserialize(byte[] bytes, int offset, int length)
    {
        throw new System.NotImplementedException();
    }
}