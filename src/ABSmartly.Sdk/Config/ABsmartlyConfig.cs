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

    internal void CopyNonNullFrom(ABsmartlyConfig source)
    {
        if (source == null) return;
        ContextDataProvider = source.ContextDataProvider ?? ContextDataProvider;
        ContextDataDeserializer = source.ContextDataDeserializer ?? ContextDataDeserializer;
        ContextEventSerializer = source.ContextEventSerializer ?? ContextEventSerializer;
        ContextPublisher = source.ContextPublisher ?? ContextPublisher;
        ContextEventLogger = source.ContextEventLogger ?? ContextEventLogger;
        VariableParser = source.VariableParser ?? VariableParser;
        AudienceDeserializer = source.AudienceDeserializer ?? AudienceDeserializer;
    }
}
