#nullable enable
using System;

namespace ABSmartly;

[Obsolete("IABSdkHttpClientFactory has been renamed to IABsmartlyHttpClientFactory. Please use IABsmartlyHttpClientFactory instead.")]
public interface IABSdkHttpClientFactory : IABsmartlyHttpClientFactory
{
}

[Obsolete("IABSdkHttpClient has been renamed to IABsmartlyHttpClient. Please use IABsmartlyHttpClient instead.")]
public interface IABSdkHttpClient : IABsmartlyHttpClient
{
}
