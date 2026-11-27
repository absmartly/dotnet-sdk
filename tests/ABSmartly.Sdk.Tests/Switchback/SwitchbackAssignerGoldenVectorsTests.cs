using System.Text.Json;
using ABSmartly.Internal;

namespace ABSmartly.Sdk.Tests.Switchback;

// Ported from java-sdk SwitchbackAssignerGoldenVectorsTest: the balanced-block draw against the
// platform's golden vectors (SwitchbackFixtures), including the long.MinValue/MaxValue periods
// and seeds.
[TestFixture]
public class SwitchbackAssignerGoldenVectorsTests
{
    // The platform hashes a numeric cluster unit as Long.toString(n); strings pass through.
    private static string ClusterUnit(JsonElement node)
    {
        if (node.ValueKind == JsonValueKind.Number) return node.GetInt64().ToString(System.Globalization.CultureInfo.InvariantCulture);
        node.ValueKind.Should().Be(JsonValueKind.String, "clusterUnit must be a string or integer: {0}", node);
        return node.GetString()!;
    }

    // 64-bit fields are decimal strings; a numeric node would mean the fixture regressed to a lossy
    // encoding.
    private static long ExactLong(JsonElement node)
    {
        node.ValueKind.Should().Be(JsonValueKind.String, "64-bit field must be a decimal string: {0}", node);
        return long.Parse(node.GetString()!, System.Globalization.CultureInfo.InvariantCulture);
    }

    [Test]
    public void SharedFixturesMatchThePlatformBlobs()
    {
        SwitchbackFixtures.GitBlobId(SwitchbackFixtures.Bytes("golden-vectors.json"))
            .Should().Be(SwitchbackFixtures.GoldenVectorsBlob);
        SwitchbackFixtures.GitBlobId(SwitchbackFixtures.Bytes("attribute-path-cases.json"))
            .Should().Be(SwitchbackFixtures.AttributePathCasesBlob);
    }

    [Test]
    public void AssignSwitchbackMatchesEveryGoldenVector()
    {
        var cases = SwitchbackFixtures.Json("golden-vectors.json").GetProperty("assignSwitchback");
        cases.GetArrayLength().Should().Be(882);

        var armCounts = new HashSet<int>();
        var boundaryCases = 0;
        var integerUnits = 0;
        foreach (var c in cases.EnumerateArray())
        {
            var unit = ClusterUnit(c.GetProperty("clusterUnit"));
            if (c.GetProperty("clusterUnit").ValueKind == JsonValueKind.Number) ++integerUnits;
            var periodIndex = ExactLong(c.GetProperty("periodIndex"));
            var seed = ExactLong(c.GetProperty("seed"));
            var armCount = c.GetProperty("armCount").GetInt32();
            armCounts.Add(armCount);
            if (periodIndex is long.MinValue or long.MaxValue || seed is long.MinValue or long.MaxValue) ++boundaryCases;

            SwitchbackAssigner.Assign(unit, periodIndex, seed, armCount)
                .Should().Be(c.GetProperty("expectedVariant").GetInt32(), c.ToString());
        }

        armCounts.Should().Contain(new[] { 2, 3, 10 });
        boundaryCases.Should().BePositive();
        integerUnits.Should().BePositive();
    }

    [Test]
    public void FisherYatesPermutationMatchesEveryGoldenVector()
    {
        var cases = SwitchbackFixtures.Json("golden-vectors.json").GetProperty("fisherYatesPermutation");
        cases.GetArrayLength().Should().Be(504);

        foreach (var c in cases.EnumerateArray())
        {
            var unit = ClusterUnit(c.GetProperty("clusterUnit"));
            var cycleIndex = ExactLong(c.GetProperty("cycleIndex"));
            var seed = ExactLong(c.GetProperty("seed"));
            var armCount = c.GetProperty("armCount").GetInt32();
            var expected = c.GetProperty("expectedPermutation").EnumerateArray().Select(x => x.GetInt32()).ToArray();

            SwitchbackAssigner.FisherYatesPermutation(armCount, SwitchbackAssigner.BaseHash(unit, cycleIndex, seed))
                .Should().Equal(expected, c.ToString());
        }
    }

    [Test]
    public void EveryCycleCoversEveryArmExactlyOnce()
    {
        foreach (var armCount in new[] { 2, 3, 10 })
        {
            for (long cycle = -3; cycle < 20; ++cycle)
            {
                var seen = new bool[armCount];
                for (var slot = 0; slot < armCount; ++slot)
                {
                    var variant = SwitchbackAssigner.Assign("store_42", cycle * armCount + slot, 0x1234_5678_9abcL,
                        armCount);
                    seen[variant].Should().BeFalse("arm repeated within a cycle");
                    seen[variant] = true;
                }
            }
        }
    }

    [Test]
    public void RejectsFewerThanTwoArms()
    {
        FluentActions.Invoking(() => SwitchbackAssigner.Assign("u", 0, 0, 1)).Should().Throw<ArgumentException>();
        FluentActions.Invoking(() => SwitchbackAssigner.FisherYatesPermutation(0, 0)).Should().Throw<ArgumentException>();
    }

    [Test]
    public void FloorDiv()
    {
        SwitchbackAssigner.FloorDiv(0, 3).Should().Be(0);
        SwitchbackAssigner.FloorDiv(2, 3).Should().Be(0);
        SwitchbackAssigner.FloorDiv(3, 3).Should().Be(1);
        SwitchbackAssigner.FloorDiv(-1, 3).Should().Be(-1);
        SwitchbackAssigner.FloorDiv(-3, 3).Should().Be(-1);
        SwitchbackAssigner.FloorDiv(-4, 3).Should().Be(-2);
        // Math.floorDiv(Long.MIN_VALUE, 3) and Math.floorDiv(Long.MAX_VALUE, 10).
        SwitchbackAssigner.FloorDiv(long.MinValue, 3).Should().Be(-3074457345618258603L);
        SwitchbackAssigner.FloorDiv(long.MaxValue, 10).Should().Be(922337203685477580L);
    }

    // The key is the UTF-8 of the cluster unit as java-sdk Buffers.encodeUTF8 writes it: a pair is
    // one 4-byte code point, and an unpaired surrogate its own 3 bytes (Encoding.UTF8 would write
    // U+FFFD), so non-ASCII cluster values draw as on the JVM. Expected values computed by java-sdk
    // 07807b51 SwitchbackAssigner on the same inputs. A source rather than [TestCase]: attribute
    // strings are stored as UTF-8, which loses an unpaired surrogate.
    private static IEnumerable<TestCaseData> NonAsciiUnits()
    {
        yield return new TestCaseData("Lisboa-S\u00e3o", -270880671, -1484660829, 0).SetName("Latin-1");
        yield return new TestCaseData("\ud83d\ude00", -1380329320, 1532414528, 0).SetName("SurrogatePair");
        yield return new TestCaseData("\ud83d", 92275467, 257677457, 2).SetName("UnpairedHighSurrogate");
        yield return new TestCaseData("\ude00x", -1466195753, 978905521, 8).SetName("UnpairedLowSurrogate");
        yield return new TestCaseData("\u00fcn\u00efc\u00f6d\u00e9-\u6771\u4eac", -1451147801, 899200994, 0)
            .SetName("ThreeByteSequences");
    }

    [TestCaseSource(nameof(NonAsciiUnits))]
    public void NonAsciiClusterUnitsHashAsOnTheJvm(string unit, int baseHash, int baseHashOfCycleMinus7, int variant)
    {
        SwitchbackAssigner.BaseHash(unit, 0, 0).Should().Be(baseHash);
        SwitchbackAssigner.BaseHash(unit, -7, 0x123456789abcL).Should().Be(baseHashOfCycleMinus7);
        SwitchbackAssigner.Assign(unit, 5, -42L, 10).Should().Be(variant);
    }
}
