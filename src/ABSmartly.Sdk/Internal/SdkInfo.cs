using System.Linq;
using System.Reflection;

namespace ABSmartly.Internal;

// The SDK's name and version, as sent in the X-Agent header ("absmartly-dotnet-sdk/<version>",
// following absmartly/opscore#24). The version is the package version, embedded at build time
// as assembly metadata, so it cannot drift from the published package.
internal static class SdkInfo
{
    internal const string Name = "absmartly-dotnet-sdk";

    internal static readonly string Version = typeof(SdkInfo).Assembly
        .GetCustomAttributes<AssemblyMetadataAttribute>()
        .FirstOrDefault(a => a.Key == "SdkVersion")?.Value ?? "unknown";

    internal static readonly string Agent = Name + "/" + Version;
}
