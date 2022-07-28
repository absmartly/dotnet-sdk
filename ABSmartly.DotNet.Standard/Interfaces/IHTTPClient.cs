using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ABSmartly.Interfaces;

public interface IHttpClient : IDisposable
{
    Task<IResponse> GetAsync(string url, Dictionary<string, string> query, Dictionary<string, string> headers);
    Task<IResponse> PutAsync(string url, Dictionary<string, string> query, Dictionary<string, string> headers, byte[] body);
    Task<IResponse> PostAsync(string url, Dictionary<string, string> query, Dictionary<string, string> headers, byte[] body);

}