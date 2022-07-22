using System.Collections.Generic;
using System.Threading.Tasks;

namespace ABSmartly;

public class DefaultHTTPClient : IHTTPClient
{
    public static DefaultHTTPClient Create(DefaultHTTPClientConfig config)
    {
        return new DefaultHTTPClient(config);
    }

    public DefaultHTTPClient(DefaultHTTPClientConfig config)
    {

    }




    public async Task<IResponse> GetAsync(string url, Dictionary<string, string> query, Dictionary<string, string> headers)
    {

        throw new System.NotImplementedException();
    }

    public async Task<IResponse> PutAsync(string url, Dictionary<string, string> query, Dictionary<string, string> headers, byte[] body)
    {
        throw new System.NotImplementedException();
    }

    public async Task<IResponse> PostAsync(string url, Dictionary<string, string> query, Dictionary<string, string> headers, byte[] body)
    {
        throw new System.NotImplementedException();
    }


    public void Dispose()
    {
        throw new System.NotImplementedException();
    }
}

public class DefaultResponse : IResponse
{
    public int GetStatusCode()
    {
        throw new System.NotImplementedException();
    }

    public string GetStatusMessage()
    {
        throw new System.NotImplementedException();
    }

    public string GetContentType()
    {
        throw new System.NotImplementedException();
    }

    public byte[] GetContent()
    {
        throw new System.NotImplementedException();
    }
}