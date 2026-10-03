using System.Security.Cryptography;
using System.Text;
using CoreSim.Analysis;
using Xunit;

namespace CoreSim.Tests;

public sealed class TrajectoryBehaviorFingerprintTests
{
    [Fact]
    public void AllCandidateNumbersCostsAndHeatDecisionsMatchAuditedHeadExactly()
        => Assert.Equal(TrajectoryBehaviorFingerprint.ExpectedSha256, TrajectoryBehaviorFingerprint.Sha256());

    [Fact]
    public void BehaviorEvidenceRetainsOriginalGitBlobBytes()
    {
        var path = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,
            "../../../../../docs/calibration/trajectory-intent-evaluation.json"));
        var actual = File.ReadAllBytes(path);
        var regenerated = Encoding.UTF8.GetBytes(TrajectoryIntentEvidence.Json(TrajectoryIntentEvidence.Run()));
        Assert.Equal(actual, regenerated);
        var header = Encoding.UTF8.GetBytes($"blob {actual.Length}\0");
        Assert.Equal(TrajectoryBehaviorFingerprint.ExpectedEvidenceBlob,
            Convert.ToHexString(SHA1.HashData(header.Concat(actual).ToArray())).ToLowerInvariant());
    }
}
