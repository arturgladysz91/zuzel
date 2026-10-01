using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CoreSim.Decisions;
using CoreSim.Logging;
using CoreSim.Race;
using CoreSim.Setup;

namespace CoreSim.Analysis;

/// <summary>Exact pre-change compatibility control; never reconstructs physics.</summary>
public static class FixedLineCompatibility
{
    public static IReadOnlyList<FixedLineResult> Run()
    {
        var results = new List<FixedLineResult>();
        for (var lane = 0; lane <= 4; lane++)
        {
            var track = MatchedVenueProfiles.CreateMotoarenaStandingStartTrack();
            var rider = new RiderState(RiderProfile.CreateDefault(1), lane) { ActiveSetup = BikeSetup.Neutral };
            var engine = new SimulationEngine(new Hold(lane));
            var options = new HeatSimulationOptions { Laps = 4, IncidentFrequency = 0f, EnableLogging = false };
            var observations = new List<object>();
            var laps = new List<float>();
            var lapStart = 0f;
            for (var step = 0; step < 4 * track.Segments.Count; step++)
            {
                var segment = step % track.Segments.Count;
                var state = TrackState.CreateDefault(track, new TrackSurfaceState(1f, 0f, .35f));
                var snapshot = engine.CaptureSnapshot(track, state, new[] { rider },
                    new SimulationStepContext(53, step, step / track.Segments.Count, segment, 0, 4));
                var resolved = engine.Resolve(snapshot, engine.Decide(snapshot), options);
                var d = resolved.Diagnostics.Single();
                observations.Add(new { Change = resolved.Changes.Single(), d.TravelledMeters, d.TravelTimeSeconds,
                    d.PeakSpeedMetersPerSecond, d.FullDriveEquilibriumSpeedMetersPerSecond,
                    d.TurnExitNetAccelerationMetersPerSecondSquared, d.TurnExitDriveProfile, d.StraightProfile,
                    d.TurnEntryScrubProfile, d.EntrySurface, d.StandingStartLaunchProfile,
                    d.CornerSpeedCorrectionProfile, d.CornerPhaseContext, d.ContinuousCornerProfile });
                engine.Commit(resolved, new[] { rider }, state, new SimLog());
                if (MathF.Abs(rider.LateralPosition - lane) > 1e-6f) throw new InvalidOperationException($"Fixed-line control moved: lane {lane}, step {step}.");
                if (segment == track.Segments.Count - 1) { laps.Add(rider.ElapsedTimeSeconds - lapStart); lapStart = rider.ElapsedTimeSeconds; }
            }
            var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(observations))));
            results.Add(new(lane, hash, rider.DistanceMeters, rider.ElapsedTimeSeconds, rider.Speed, laps.ToArray()));
        }
        return results;
    }
    private sealed class Hold(int lane) : IRiderDecisionModel
    {
        public RiderDecision Decide(TrackSegment segment, RiderState rider) => new(lane, 0f);
    }
}

public sealed record FixedLineResult(int Lane, string ExactDiagnosticsSha256, float DistanceMeters,
    float TimeSeconds, float ExitSpeedMetersPerSecond, IReadOnlyList<float> LapTimesSeconds);
