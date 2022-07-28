using System;
using ABSmartly.Interfaces;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;

namespace ABSmartly.DefaultServiceImplementations;

internal class DefaultHttpClient : /*IHttpClient,*/ IDisposable
{
    private IHttpClientFactory _httpClientFactory;

    #region Lifecycle

    public DefaultHttpClient(DefaultHttpClientConfig config, IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    public static DefaultHttpClient Create(DefaultHttpClientConfig config, IHttpClientFactory httpClientFactory)
    {
        return new DefaultHttpClient(config, httpClientFactory);
    }    

    #endregion



    // Todo: Comment: I usually use a 'Result' object for return value, now I made it generic, so returns the expected data directly



    public async Task<T> GetAsync<T>(string url, Dictionary<string, string> query, Dictionary<string, string> headers)
    {
        var httpClient = _httpClientFactory.CreateClient();

        var queryurlstring = "";

        try
        {
            // Step 1: Buiild the request
            if (query is not null)
            {
                var dictFormUrlEncoded = new FormUrlEncodedContent(query);
                var queryString = await dictFormUrlEncoded.ReadAsStringAsync();
                if (!string.IsNullOrWhiteSpace(queryString))
                    queryurlstring = $"?{queryString}";
            }

            if (headers is not null)
            {
                foreach (var kvp in headers)
                    httpClient.DefaultRequestHeaders.Add(kvp.Key, kvp.Value);
            }

            // Step 2: Send the request
            var responseMessage = await httpClient.GetAsync($"{url}{queryurlstring}");
            responseMessage.EnsureSuccessStatusCode();

            var content = await responseMessage.Content.ReadAsStringAsync();
            var data = JsonSerializer.Deserialize<T>(content);

            return data;
        }
        catch (Exception e)
        {
            Debug.WriteLine(e);
            return default;
        }
    }

    public async Task<IResponse> PutAsync(string url, Dictionary<string, string> query, Dictionary<string, string> headers, byte[] body)
    {
        throw new System.NotImplementedException();
    }

    public async Task<IResponse> PostAsync(string url, Dictionary<string, string> query, Dictionary<string, string> headers, byte[] body)
    {
        throw new System.NotImplementedException();
    }



    //private void BuildRequest(HttpClient httpClient, Dictionary<string, string> query, Dictionary<string, string> headers, byte[] body)
    //{
    //    HttpClient httpClient;
    //    httpClient = new HttpClient();
    //    httpClient.re
    //}



    #region IDisposable

    public void Dispose()
    {
        throw new System.NotImplementedException();
    }    

    #endregion
}

//public class DefaultResponse : IResponse
//{
//    public int GetStatusCode()
//    {
//        throw new System.NotImplementedException();
//    }

//    public string GetStatusMessage()
//    {
//        throw new System.NotImplementedException();
//    }

//    public string GetContentType()
//    {
//        throw new System.NotImplementedException();
//    }

//    public byte[] GetContent()
//    {
//        throw new System.NotImplementedException();
//    }
//}