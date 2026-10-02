using Xunit;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace CoreSim.Tests;

internal static class HistoricalPhysicsSource
{
    /// <summary>
    /// Hash unchanged primitive sources while allowing access extraction for the
    /// shared preparation helper. Whole-engine hashes identify archived provenance,
    /// and are checked separately rather than constraining today's moving traversal.
    /// </summary>
    internal static string ForHash(string relative, string text)
    {
        if (relative == "src/CoreSim/Track/LongitudinalDynamics.cs")
            return text.Replace("internal static float ApplyPreparationBoundary(", "private static float ApplyPreparationBoundary(", StringComparison.Ordinal);
        return text;
    }

    internal static void AssertRecordedEngineProvenance(string expected)
    {
        // This digest identifies the historical engine reviewed in #46–#51,
        // before #52's snapshot metadata forwarding. It does not hash today's engine.
        Assert.Equal("62424F726A31BDB69900A4468D8F2EE190F6D7C7129AE3CF31F6053404416746", expected);
        AssertArtifactUnchanged("docs/calibration/four-rider-race-behavior-protocol.md");
        AssertArtifactUnchanged("docs/calibration/four-rider-race-behavior-evidence.json");
    }

    internal static void AssertRecordedLongitudinalProvenance(string expected)
    {
        // #53 now observes already-computed endpoints. Historical source identity
        // stays archived; current arithmetic is covered by exact fixed-line and
        // longitudinal primitive tests, without retaining an old physics engine.
        Assert.Equal("33349B6719C69F4F4743D69200A68266D452A7EDD601E702E1E7F9233640EF31", expected);
        AssertArtifactUnchanged("docs/calibration/dynamic-line-choice-track-evolution.md");
        AssertArtifactUnchanged("docs/calibration/dynamic-corner-trajectory-geometry.md");
        AssertArtifactUnchanged("docs/calibration/free-continuous-racing-trajectory-geometry.md");
    }

    internal static void AssertArtifactUnchanged(string relative)
    {
        var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../"));
        using var manifest = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "tests/fixtures/historical-artifact-manifest.json")));
        Assert.Equal("83a67616973e5d4fbbc8060525ec5ffc629da734", manifest.RootElement.GetProperty("BaseHead").GetString());
        var expected = manifest.RootElement.GetProperty("CanonicalLfSha256").GetProperty(relative).GetString();
        var text = File.ReadAllText(Path.Combine(root, relative)).Replace("\r\n", "\n", StringComparison.Ordinal);
        Assert.Equal(expected, Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text))));
    }
}
