using System;
using ABSmartly.Interfaces;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;

namespace ABSmartly.DefaultServiceImplementations;

internal class DefaultHttpClient : IHttpClient, IDisposable
{
    private IHttpClientFactory _httpClientFactory;

    #region Lifecycle

    public DefaultHttpClient(DefaultHttpClientConfig config)
    {

    }

    public static DefaultHttpClient Create(DefaultHttpClientConfig config)
    {
        return new DefaultHttpClient(config);
    }    

    #endregion




    public async Task<IResponse> GetAsync(string url, Dictionary<string, string> query, Dictionary<string, string> headers)
    {
        var httpClient = _httpClientFactory.CreateClient();

        // Step 1: Buiild the request
        httpClient.DefaultRequestHeaders.Add("", query);

        foreach (var kvp in headers)
            httpClient.DefaultRequestHeaders.Add(kvp.Key, kvp.Value);
        


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



    private void BuildRequest(HttpClient httpClient, Dictionary<string, string> query, Dictionary<string, string> headers, byte[] body)
    {
        HttpClient httpClient;
        httpClient = new HttpClient();
        httpClient.re
    }



    #region IDisposable

    public void Dispose()
    {
        throw new System.NotImplementedException();
    }    

    #endregion
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