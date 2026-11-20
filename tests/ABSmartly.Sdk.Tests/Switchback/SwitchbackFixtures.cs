using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace ABSmartly.Sdk.Tests.Switchback;

// The switchback fixtures under Resources/switchback are byte-identical copies, vendored through
// java-sdk 07807b51 (core-api/src/test/resources/switchback) and cross-sdk-tests fixtures/switchback,
// of the platform's files at absmartly/abs 9bd2b5ca35:
// - golden-vectors.json: lib/utils/src/test/resources/switchback/golden-vectors.json, git blob b314abeb9e97.
// - attribute-path-cases.json: collector/src/test/resources/switchback/attribute-path-cases.json,
//   git blob 7b1d3fed5985.
// Do not edit them; SharedFixturesMatchThePlatformBlobs checks the blob ids.
internal static class SwitchbackFixtures
{
    public const string GoldenVectorsBlob = "b314abeb9e97b35f911c7caacec90b8ff73b9d77";
    public const string AttributePathCasesBlob = "7b1d3fed59856fcbc296ed68a820b43359ae1932";

    public static byte[] Bytes(string name)
    {
        using var stream = typeof(SwitchbackFixtures).Assembly
            .GetManifestResourceStream("ABSmartly.Sdk.Tests.Resources.switchback." + name)!;
        using var memory = new MemoryStream();
        stream.CopyTo(memory);
        return memory.ToArray();
    }

    public static JsonElement Json(string name) => JsonDocument.Parse(Bytes(name)).RootElement;

    // git hash-object: sha1("blob <length>\0" + content).
    public static string GitBlobId(byte[] content)
    {
        var header = Encoding.ASCII.GetBytes("blob " + content.Length + "\0");
        using var sha1 = SHA1.Create();
        return Convert.ToHexString(sha1.ComputeHash(header.Concat(content).ToArray())).ToLowerInvariant();
    }
}
