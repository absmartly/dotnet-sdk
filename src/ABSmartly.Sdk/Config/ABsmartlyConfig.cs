using System;

namespace ABSmartly;

public class ABsmartlyConfig
{
    public IContextDataProvider ContextDataProvider { get; set; }
    public IContextDataDeserializer ContextDataDeserializer { get; set; }
    public IContextEventSerializer ContextEventSerializer { get; set; }
    public IContextPublisher ContextPublisher { get; set; }

    /// <summary>Obsolete: Use <see cref="ContextPublisher"/> instead.</summary>
    [Obsolete("Use ContextPublisher instead.")]
    public IContextEventHandler ContextEventHandler
    {
        get => ContextPublisher as IContextEventHandler;
        set => ContextPublisher = value;
    }
    public IContextEventLogger ContextEventLogger { get; set; }
    public IVariableParser VariableParser { get; set; }
    public IAudienceDeserializer AudienceDeserializer { get; set; }
}
