namespace ABSmartly;

public class ABSmartlyServiceConfiguration
{
    public string Environment { get; set; }
    public string Application { get; set; }
    public string Endpoint { get; set; }
    public string ApiKey { get; set; }

    /// <summary>
    /// The version of the application using the SDK, sent as the X-Application-Version header on the
    /// context fetch and publish. The collector accepts only a whole number there, so the value is
    /// sent (trimmed) only when it is 1 to 18 ASCII digits, for example "1004"; anything else,
    /// including a semantic version such as "2.4.1", a sign or a null/empty value, is sent as "0".
    /// </summary>
    public string ApplicationVersion { get; set; }
}