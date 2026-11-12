namespace ABSmartly;

public class ABSmartlyServiceConfiguration
{
    public string Environment { get; set; }
    public string Application { get; set; }
    public string Endpoint { get; set; }
    public string ApiKey { get; set; }

    /// <summary>
    /// The version of the application using the SDK, sent as the X-Application-Version header.
    /// "0" when null or empty, as before this setting existed.
    /// </summary>
    public string ApplicationVersion { get; set; }
}