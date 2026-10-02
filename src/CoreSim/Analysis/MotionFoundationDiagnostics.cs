using System.Text.Json;
using CoreSim.Decisions;
using CoreSim.Logging;
using CoreSim.Race;

namespace CoreSim.Analysis;

public sealed record CrossingMotionDiagnostic(float BracketStartSeconds, float BracketEndSeconds,
    RiderMotionSample RiderOne, RiderMotionSample RiderTwo, bool LateralOrderReversed);
public sealed record LeaderMotionDiagnostic(float CommonTimeSeconds, RiderMotionSample Leader,
    RiderMotionSample Follower, float LongitudinalSeparationMeters);
public sealed record MotionStorageDiagnostic(string Control, int Segments, int Nodes, int Intervals,
    int ExecutedPathNodes, int LongitudinalSolverFallbacks);

/// <summary>Read-only common-time evidence. No contact distance threshold or pair resolver.</summary>
public static class MotionFoundationDiagnostics
{
    public const string AuditedHead = "daf88c1b5d95eed49c4b65f3bb4290c6312cfc1c";

    public static ResolvedSimulationStep CrossingStep(int seed = 0, bool reverse = false)
    {
        // Exact #51 K-crossing geometry, surface, start speed and opposite intents.
        var track = new Track(new[] { new TrackSegment(0, SegmentType.Straight), new TrackSegment(1, SegmentType.Straight) },
            new TrackGeometry(60f, 24f, 10f, 14f, MathF.PI / 3f));
        var riders = new[] { new RiderState(RiderProfile.CreateDefault(1), 0) { Speed = 18f },
            new RiderState(RiderProfile.CreateDefault(2), 1) { Speed = 18f } };
        return Resolve(track, TrackState.CreateDefault(track, new TrackSurfaceState(1f, 0f, 0f)),
            reverse ? riders.Reverse().ToArray() : riders, new CrossingTargets(), seed);
    }

    public static CrossingMotionDiagnostic Crossing()
    {
        var step = CrossingStep();
        var a = step.Motions[0]; var b = step.Motions[1];
        var low = 0f; var high = MathF.Min(a.TotalTimeSeconds, b.TotalTimeSeconds);
        float Difference(float t) => a.SampleAtTime(t).PhysicalOffsetMeters - b.SampleAtTime(t).PhysicalOffsetMeters;
        if (!(Difference(low) < 0f && Difference(high) > 0f))
            throw new InvalidOperationException("Crossing control did not reverse lateral order.");
        for (var i = 0; i < 64; i++)
        {
            var mid = (float)(((double)low + high) * .5d);
            if (mid == low || mid == high) break;
            if (Difference(mid) < 0f) low = mid; else high = mid;
        }
        return new(low, high, a.SampleAtTime(high), b.SampleAtTime(high), true);
    }

    public static IReadOnlyList<LeaderMotionDiagnostic> DistantLeader()
    {
        var scenario = FourRiderBehaviorSuite.CreateScenarios().Single(s => s.Id == "A");
        var riders = scenario.Riders.Where(r => r.Id <= 2).Select(r => r.Create(scenario.Track)).ToArray();
        var step = Resolve(scenario.Track, scenario.CreateSurface(), riders, new Hold());
        return new[] { 0f, .01f, .5f }.Select(t =>
        {
            var leader = step.Motions[0].SampleAtTime(t); var follower = step.Motions[1].SampleAtTime(t);
            return new LeaderMotionDiagnostic(t, leader, follower,
                (leader.SegmentProgress - follower.SegmentProgress) * scenario.Track.Geometry.StraightLengthMeters);
        }).ToArray();
    }

    public static MotionStorageDiagnostic FourLaps(bool moving)
    {
        var track = MatchedVenueProfiles.CreateMotoarenaStandingStartTrack();
        var riders = new[] { new RiderState(RiderProfile.CreateDefault(1), 2) };
        var engine = new SimulationEngine(moving ? new MovingTargets() : new Hold());
        var options = new HeatSimulationOptions { Laps = 4, IncidentFrequency = 0f, EnableLogging = false };
        var nodes = 0; var intervals = 0; var pathNodes = 0; var fallbacks = 0;
        for (var index = 0; index < 4 * track.Segments.Count; index++)
        {
            var state = TrackState.CreateDefault(track, new TrackSurfaceState(1f, 0f, .35f));
            var snapshot = engine.CaptureSnapshot(track, state, riders,
                new SimulationStepContext(53, index, index / track.Segments.Count, index % track.Segments.Count, 0, 4));
            var step = engine.Resolve(snapshot, engine.Decide(snapshot), options);
            nodes += step.Motions[0].Nodes.Count; intervals += step.Motions[0].Nodes.Count - 1;
            pathNodes += step.Diagnostics[0].ExecutedPath?.Nodes.Count ?? 0;
            fallbacks += step.Diagnostics[0].ExecutedPath?.Steps.Count(s => s.UsedBisectionFallback) ?? 0;
            engine.Commit(step, riders, state, new SimLog());
        }
        return new(moving ? "moving" : "fixed", 4 * track.Segments.Count, nodes, intervals, pathNodes, fallbacks);
    }

    public static string Evidence() => JsonSerializer.Serialize(new
    {
        AuditedHead, Crossing = Crossing(), DistantLeader = DistantLeader(),
        Storage = new[] { FourLaps(false), FourLaps(true) }, FixedLineCompatibility = FixedLineCompatibility.Run(),
    }, new JsonSerializerOptions { WriteIndented = true }).Replace("\r\n", "\n", StringComparison.Ordinal) + "\n";

    private static ResolvedSimulationStep Resolve(Track track, TrackState state, IReadOnlyList<RiderState> riders,
        IRiderDecisionModel model, int seed = 0)
    {
        var engine = new SimulationEngine(model);
        var snapshot = engine.CaptureSnapshot(track, state, riders, new SimulationStepContext(53, 0, 0, 0, seed, 1));
        return engine.Resolve(snapshot, engine.Decide(snapshot), new HeatSimulationOptions { Laps = 1, IncidentFrequency = 0f });
    }
    private sealed class CrossingTargets : IRiderDecisionModel
    {
        public RiderDecision Decide(TrackSegment segment, RiderState rider) => new(rider.RiderId == 1 ? 1 : 0, 0f);
    }
    private sealed class Hold : IRiderDecisionModel
    {
        public RiderDecision Decide(TrackSegment segment, RiderState rider) => new(rider.Lane, 0f);
    }
    private sealed class MovingTargets : IRiderDecisionModel
    {
        public RiderDecision Decide(TrackSegment segment, RiderState rider)
            => new(segment.Type is SegmentType.TurnEntry or SegmentType.TurnMiddle ? 0 : 4, 0f);
    }
}
