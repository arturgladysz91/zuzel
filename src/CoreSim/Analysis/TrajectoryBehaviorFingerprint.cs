using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using CoreSim.Decisions;
using CoreSim.Race;

namespace CoreSim.Analysis;

/// <summary>Exact all-candidate audit captured before the #54 performance pass.</summary>
public static class TrajectoryBehaviorFingerprint
{
    public const string AuditedHead = "9ff730c5b1b2cfca739c3ead2ccc2760be1b7c71";
    public const string ExpectedSha256 = "F20AE8E3895CF54625D549DB127BA1C8E2168A7AA3549ED94F305A04D89C3D18";
    public const string ExpectedEvidenceBlob = "ad266b8861a7518fd62da28ccd7ed4c4eaeabd34";

    public static byte[] Capture()
    {
        var decisions = new List<object>();
        foreach (var source in FourRiderBehaviorSuite.CreateScenarios().Where(s =>
            new[] { "B", "C", "H", "F", "G", "J-tight", "J-wide" }.Contains(s.Id)))
        foreach (var seed in new[] { 0, 17 })
        foreach (var modelSeed in new[] { 1234, 37 })
        {
            var rider = source.Riders.Single(r => r.Id == 2).Create(source.Track);
            var snapshot = new SimulationEngine(new Hold()).CaptureSnapshot(source.Track,
                source.CreateSurface(), new[] { rider }, new(54, rider.SegmentIndex, rider.LapsCompleted,
                    rider.SegmentIndex, seed, 4));
            var result = new AdaptiveDecisionModel(modelSeed).Evaluate(new(snapshot, snapshot.Rider(2)));
            decisions.Add(new { Scenario = source.Id, Seed = seed, ModelSeed = modelSeed, result.Decision, result.Candidates });
        }
        var track = MatchedVenueProfiles.CreateMotoarenaStandingStartTrack();
        new HeatSimulator(new CaptureDecisions(decisions)).SimulateHeat(track, TrackState.CreateDefault(track),
            Enumerable.Range(1, 4).Select(id => new RiderState(RiderProfile.CreateDefault(id),
                (StartingGate)(id - 1), track)).ToList(),
            new HeatSimulationOptions { Laps = 4, IncidentFrequency = 0, EnableLogging = false });
        var options = new JsonSerializerOptions { WriteIndented = true,
            Converters = { new JsonStringEnumConverter() }, NumberHandling = JsonNumberHandling.AllowNamedFloatingPointLiterals };
        // .NET 8's indented writer uses the host newline. The audited Windows
        // blob contains CRLF inside the document and a final LF. Freeze that
        // byte format on every host; no numbers or production operations change.
        var json = JsonSerializer.Serialize(decisions, options)
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace("\n", "\r\n", StringComparison.Ordinal);
        return Encoding.UTF8.GetBytes(json + "\n");
    }

    public static string Sha256() => Convert.ToHexString(SHA256.HashData(Capture()));
    private sealed class Hold : IRiderDecisionModel
    {
        public RiderDecision Decide(TrackSegment segment, RiderState rider) => new(rider.Lane);
    }
    private sealed class CaptureDecisions(List<object> trace) : IRiderDecisionModel
    {
        private readonly AdaptiveDecisionModel model = new();
        public RiderDecision Decide(TrackSegment segment, RiderState rider) => model.Decide(segment, rider);
        public RiderDecision Decide(RiderDecisionContext context)
        {
            var result = model.Evaluate(context);
            trace.Add(new { Heat = true, context.StepNumber, context.Rider.RiderId, result.Decision, result.Candidates });
            return result.Decision;
        }
    }
}
