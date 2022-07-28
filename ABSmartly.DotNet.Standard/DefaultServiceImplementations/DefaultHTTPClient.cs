using ABSmartly.Interfaces;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ABSmartly.DefaultServiceImplementations;

internal class DefaultHttpClient : IHttpClient
{
    #region Lifecycle

    

    #endregion

    public DefaultHttpClient(DefaultHttpClientConfig config)
    {

    }

    public static DefaultHttpClient Create(DefaultHttpClientConfig config)
    {
        return new DefaultHttpClient(config);
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