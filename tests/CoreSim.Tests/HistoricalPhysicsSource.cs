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
        if (relative == "src/CoreSim/Track/TrackState.cs")
        {
            // Normalize only the explicit capture/cloning support extraction;
            // every historical physical expression remains protected by its old digest.
            text = text.Replace("    private TrackState(TrackSurfaceState[,] cells)\n    {\n        ProjectionCaptureAudit.Record(ProjectionMaterialization.TrackStateCopy);\n        _surface = cells; SegmentCount = cells.GetLength(0); LinesCount = cells.GetLength(1);\n    }\n    /// <summary>Detached exact-value copy for a hypothetical branch.</summary>\n    internal TrackState Clone() => new((TrackSurfaceState[,])_surface.Clone());\n\n", "", StringComparison.Ordinal);
            return text;
        }
        if (relative == "src/CoreSim/Track/ContinuousCornerEnvelope.cs")
        {
            // Strictly map this reviewed capture/performance extraction back to
            // the protected source block. Both complete blocks are frozen; an
            // arithmetic edit to the shared primitive fails the original digest.
            var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../"));
            using var extraction = JsonDocument.Parse(File.ReadAllText(Path.Combine(root,
                "tests/fixtures/envelope-scalar-source-extraction.json")));
            text = text.Replace(extraction.RootElement.GetProperty("Extracted").GetString()!,
                extraction.RootElement.GetProperty("Original").GetString()!, StringComparison.Ordinal);
            text = text.Replace("        ProjectionCaptureAudit.Record(ProjectionMaterialization.EnvelopeCreation);\n", "", StringComparison.Ordinal);
            // Normalize only the explicit capture/cloning support extraction;
            // every historical physical expression remains protected by its old digest.
            text = text.Replace("    internal ContinuousCornerTraversalProfile TraverseProjection(float entrySpeedMetersPerSecond,\n        float startProgress, float distanceMeters, bool allowDrive, bool allowCorrection, float retainedOverspeedMetersPerSecond)\n        => TraverseCore(entrySpeedMetersPerSecond, startProgress, distanceMeters,\n            0f, null, null, 1f, 0f, allowDrive, allowCorrection, retainedOverspeedMetersPerSecond, captureNodes: false);\n\n", "", StringComparison.Ordinal);
            text = text.Replace("float retainedOverspeedMetersPerSecond, bool captureNodes = true)", "float retainedOverspeedMetersPerSecond)", StringComparison.Ordinal);
            text = text.Replace("        var previousNodeSpeed = speed;\n", "", StringComparison.Ordinal);
            text = text.Replace("var nodes = captureNodes ? new List<ContinuousCornerNode>", "var nodes = new List<ContinuousCornerNode>", StringComparison.Ordinal);
            text = text.Replace("        } : null;\n        while (travelled", "        };\n        while (travelled", StringComparison.Ordinal);
            text = text.Replace("nodes?.Add(Node", "nodes.Add(Node", StringComparison.Ordinal);
            text = text.Replace("controlLossEligible ? previousNodeSpeed : 0f", "controlLossEligible ? nodes[^1].SpeedMetersPerSecond : 0f", StringComparison.Ordinal);
            text = text.Replace("            previousNodeSpeed = speed;\n", "", StringComparison.Ordinal);
            text = text.Replace("nodes is null ? ContinuousCornerNodes.Empty : new ContinuousCornerNodes(nodes)", "new ContinuousCornerNodes(nodes)", StringComparison.Ordinal);
            text = text.Replace("    internal static ContinuousCornerNodes Empty { get; } = new(Array.Empty<ContinuousCornerNode>());\n", "", StringComparison.Ordinal);
            text = text.Replace("            ProjectionCaptureAudit.Record(ProjectionMaterialization.CornerNode);\n", "", StringComparison.Ordinal);
            return text;
        }
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

    internal static void AssertRecordedDecisionProvenance(string expected)
    {
        // Static decision source identifies #39–#53 evidence. Current decisions
        // intentionally use production replay and are tested separately.
        Assert.Equal("A1FCBA7E068C09B69CF849492424C89083BAD69F965FC6D767415EA9F7478314", expected);
        AssertArtifactUnchanged("docs/calibration/four-rider-race-behavior-audit.md");
        AssertArtifactUnchanged("docs/calibration/four-rider-race-behavior-evidence.json");
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
