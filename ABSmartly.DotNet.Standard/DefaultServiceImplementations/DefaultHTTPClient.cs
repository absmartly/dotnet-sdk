using System;
using ABSmartly.Interfaces;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace ABSmartly.DefaultServiceImplementations;

internal class DefaultHttpClient : IHttpClient
{
    private readonly IHttpClientFactory _httpClientFactory;
    public const string ABSmartyHttpClientName = "absmartly-defaulthttpclient";

    #region Lifecycle

    public DefaultHttpClient(DefaultHttpClientConfig config, IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    //public static DefaultHttpClient Create(DefaultHttpClientConfig config, IHttpClientFactory httpClientFactory)
    //{
    //    return new DefaultHttpClient(config, httpClientFactory);
    //}    

    #endregion

    public async Task<T> GetAsync<T>(string url, Dictionary<string, string> query, Dictionary<string, string> headers)
    {
        var httpClient = _httpClientFactory.CreateClient(ABSmartyHttpClientName);

        var requestQueryString = "";

        try
        {
            // Step 1: Buiild the request
            if (query is not null)
            {
                var dictFormUrlEncoded = new FormUrlEncodedContent(query);
                var queryString = await dictFormUrlEncoded.ReadAsStringAsync();
                if (!string.IsNullOrWhiteSpace(queryString))
                    requestQueryString = $"?{queryString}";
            }

            if (headers is not null)
            {
                foreach (var kvp in headers)
                    httpClient.DefaultRequestHeaders.Add(kvp.Key, kvp.Value);
            }

            // Step 2: Send the request
            var responseMessage = await httpClient.GetAsync($"{url}{requestQueryString}");
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

    public async Task<T> PutAsync<T>(string url, Dictionary<string, string> query, Dictionary<string, string> headers, byte[] body)
    {
        var httpClient = _httpClientFactory.CreateClient(ABSmartyHttpClientName);

        var requestQueryString = "";

        try
        {
            // Step 1: Buiild the request
            if (query is not null)
            {
                var dictFormUrlEncoded = new FormUrlEncodedContent(query);
                var queryString = await dictFormUrlEncoded.ReadAsStringAsync();
                if (!string.IsNullOrWhiteSpace(queryString))
                    requestQueryString = $"?{queryString}";
            }

            if (headers is not null)
            {
                foreach (var kvp in headers)
                    httpClient.DefaultRequestHeaders.Add(kvp.Key, kvp.Value);
            }

            HttpContent requestContent = new ByteArrayContent(body);

            // Step 2: Send the request
            var responseMessage = await httpClient.PutAsync($"{url}{requestQueryString}", requestContent);
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

    public async Task<T> PostAsync<T>(string url, Dictionary<string, string> query, Dictionary<string, string> headers, byte[] body)
    {
        var httpClient = _httpClientFactory.CreateClient(ABSmartyHttpClientName);

        var requestQueryString = "";

        try
        {
            // Step 1: Buiild the request
            if (query is not null)
            {
                var dictFormUrlEncoded = new FormUrlEncodedContent(query);
                var queryString = await dictFormUrlEncoded.ReadAsStringAsync();
                if (!string.IsNullOrWhiteSpace(queryString))
                    requestQueryString = $"?{queryString}";
            }

            if (headers is not null)
            {
                foreach (var kvp in headers)
                    httpClient.DefaultRequestHeaders.Add(kvp.Key, kvp.Value);
            }

            HttpContent requestContent = new ByteArrayContent(body);

            // Step 2: Send the request
            var responseMessage = await httpClient.PostAsync($"{url}{requestQueryString}", requestContent);
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
}

