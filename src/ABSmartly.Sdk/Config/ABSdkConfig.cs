namespace ABSmartly;

public class ABSdkConfig
{
    public IContextDataProvider ContextDataProvider { get; set; }
    public IContextDataDeserializer ContextDataDeserializer { get; set; }
    public IContextEventSerializer ContextEventSerializer { get; set; }
    public IContextEventHandler ContextEventHandler { get; set; }
    public IContextEventLogger ContextEventLogger { get; set; }
    public IVariableParser VariableParser { get; set; }
    public IAudienceDeserializer AudienceDeserializer { get; set; }
}