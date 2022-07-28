namespace ABSmartly.Interfaces;

public interface IResponse
{
    int GetStatusCode();

    string GetStatusMessage();

    string GetContentType();

    byte[] GetContent();
}