using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ABSmartly.Interfaces;

public interface IHttpClient : IDisposable
{
    Task<T> GetAsync<T>(string url, Dictionary<string, string> query, Dictionary<string, string> headers);
    Task<T> PutAsync<T>(string url, Dictionary<string, string> query, Dictionary<string, string> headers, byte[] body);
    Task<T> PostAsync<T>(string url, Dictionary<string, string> query, Dictionary<string, string> headers, byte[] body);

}