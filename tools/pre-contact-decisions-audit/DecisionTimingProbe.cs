using CoreSim;
using CoreSim.Analysis;
using CoreSim.Decisions;
using CoreSim.Interactions;
using CoreSim.PhysicalSpace;
using CoreSim.Race;

namespace PreContactDecisions;

// Evidence only. No production caller, new response algorithm or collision solver.
internal static class DecisionTimingProbe
{
    internal const double ReactionDelaySeconds = .20; // PROVISIONAL, independent of calculation cadence
    internal static readonly double ObservationHorizonSeconds = new ContestedSpaceParameters().EmergencyTimeSeconds;
    internal sealed record Cue(bool PossibleConflict, double? BoundingDiscEntrySeconds);
    internal sealed record Context(SimulationSnapshot Snapshot, RiderIntent[] Intents, SimulationEngine Engine);
    internal sealed record RunResult(ResolvedSimulationStep Step, Cue? Cue, bool ResponseApplied, ContestedSpaceReport? DirectAudit);
    internal static ContestedScenario[] Scenarios()
    {
        var existing = ContestedSpaceResponseEvidence.Scenarios();
        ContestedScenario Get(string name) => existing.Single(s => s.Name == name);
        return new[]
        {
            Get("D-inside-overlap") with { Name = "A-yield", Riders = Get("D-inside-overlap").Riders
                .Select(r => r with { Lateral = r.Id == 1 ? 1.2f : 1.6f }).ToArray() },
            Get("edge-trapped") with { Name = "B-no-room", SegmentIndex = 3 },
            Get("D-inside-overlap") with { Name = "C-aggressive", Riders = Get("D-inside-overlap").Riders
                .Select(r => r with { Progress = .75f, Lateral = r.Id == 1 ? 1 : 1.31f,
                    Attack = 99, Defense = 99, Combativeness = 1 }).ToArray() },
            Get("D-inside-overlap") with { Name = "D-close-parallel", Riders = new[]
                { new ContestedRiderInput(1, 1, .125f, 21, new(1,1,1)),
                  new ContestedRiderInput(2, 1.8f, .125f, 21, new(2,2,2)) } },
        };
    }
    internal static HeatSimulationOptions Options => new() { EnableContestedSpaceResponses = true,
        IncidentFrequency = 0, EnableLogging = false, InteractionDiagnostics = InteractionDiagnosticsLevel.Summary };
    internal static RiderIntent[] Intents(ContestedScenario input, bool reverse = false)
    {
        var type = ContestedSpaceResponseEvidence.CreateTrack(false).Segments[input.SegmentIndex].Type;
        var result = input.Riders.Select(r => new RiderIntent(r.Id,
            new RiderDecision(r.Intent.TargetFor(type)) { Trajectory = r.Intent })).ToArray();
        return reverse ? result.Reverse().ToArray() : result;
    }
    internal static RunResult Run(ContestedScenario input, bool waitForReaction, bool reverse = false)
        => Run(Prepare(input, reverse), waitForReaction);
    internal static Context Prepare(ContestedScenario input, bool reverse = false)
    {
        var snapshot = ContestedSpaceResponseEvidence.Snapshot(input, reverse); var intents = Intents(input, reverse);
        return new(snapshot, intents, new SimulationEngine(new Hold()));
    }
    internal static RunResult Run(Context context, bool waitForReaction)
    {
        var (snapshot, intents, engine) = context;
        var cue = waitForReaction ? Observe(snapshot, intents) : null;
        // A newly recognized threat cannot authorize a reaction at this same
        // boundary. There is no mid-segment command deadline in main's API.
        // Evaluate the ONE original segment as an uncommitted counterfactual.
        // Audit its collision witness; do not fabricate a state at first touch
        // or commit any part of this candidate, particularly its colliding tail.
        var step = waitForReaction ? engine.ResolveProduction(snapshot, intents, Options, legacyContacts: false)
            : engine.Resolve(snapshot, intents, Options);
        return new(step, cue, !waitForReaction && step.Interaction!.Episodes.SelectMany(e => e.SelectedResponses)
            .Any(a => a.Response != InteractionResponse.KeepIntent), waitForReaction ? Audit(step) : null);
    }
    internal static Cue Observe(SimulationSnapshot snapshot, IReadOnlyList<RiderIntent> intents)
    {
        if (snapshot.Riders.Count != 2 || snapshot.Riders.Select(r => r.ElapsedTimeSeconds).Distinct().Count() != 1)
            throw new ArgumentException("The isolated observation requires two current states at the same physical time.");
        var map = new TrackMetricEmbedding(snapshot.Track, true); var segment = map.Segments[snapshot.Step.SegmentIndex];
        var earliest = double.PositiveInfinity;
        // Own current request is known; the opponent is extrapolated from current
        // scalar speed and track tangent, NOT its future targets or resolved path.
        // Lateral velocity/yaw are absent from RiderSnapshot. This is an advisory
        // cue, never a #55 clearance certificate or permission to ignore contact.
        foreach (var rider in snapshot.Riders)
        {
            var ownOffset = LaneModel.PhysicalLateralOffsetFromInnerReferenceMeters(rider.LateralPosition, snapshot.Segment.Type, snapshot.Track.Geometry);
            var pose = segment.Map(rider.SegmentProgress, ownOffset);
            var forward = new MeterPoint(Math.Cos(pose.TangentHeadingRadians), Math.Sin(pose.TangentHeadingRadians));
            var target = intents.Single(i => i.RiderId == rider.RiderId).Decision.TargetLane;
            var destination = LaneModel.PhysicalLateralOffsetFromInnerReferenceMeters(target, snapshot.Segment.Type, snapshot.Track.Geometry);
            var budget = LateralMovementModel.CalculateMaxLateralDistanceMeters((float)ObservationHorizonSeconds,
                snapshot.Segment.Type, snapshot.Track.Geometry, snapshot.TrackState.SampleSurface(snapshot.Step.SegmentIndex, rider.LateralPosition), rider.Profile.Skills);
            var lateralRate = Math.Clamp(destination - ownOffset, -budget, budget) / ObservationHorizonSeconds;
            var ownVelocity = forward * rider.Speed + new MeterPoint(forward.Y, -forward.X) * lateralRate;
            foreach (var other in snapshot.Riders.Where(r => r.RiderId != rider.RiderId))
            {
                var offset = LaneModel.PhysicalLateralOffsetFromInnerReferenceMeters(other.LateralPosition, snapshot.Segment.Type, snapshot.Track.Geometry);
                var sample = segment.Map(other.SegmentProgress, offset);
                var d = sample.Position - pose.Position;
                var v = new MeterPoint(Math.Cos(sample.TangentHeadingRadians), Math.Sin(sample.TangentHeadingRadians)) * other.Speed - ownVelocity;
                var radius = 2 * SpeedwayBikeDimensions.Reference.BoundingRadiusMeters;
                var c = MeterPoint.Dot(d, d) - radius * radius; var a = MeterPoint.Dot(v, v); var b = MeterPoint.Dot(d, v);
                if (c <= 0) { earliest = 0; continue; }
                var discriminant = b * b - a * c;
                if (a <= 0 || b >= 0 || discriminant < 0) continue;
                var time = (-b - Math.Sqrt(discriminant)) / a;
                if (time <= ObservationHorizonSeconds) earliest = Math.Min(earliest, time);
            }
        }
        return double.IsPositiveInfinity(earliest) ? new(false, null) : new(true, earliest);
    }
    internal static ContestedSpaceReport Audit(ResolvedSimulationStep step)
        => CommonTimePoseHistory.Observe(step.Motions.SelectMany(m => ResolvedBikePoses.FromMotion(m, step.Snapshot.Track)));
    internal sealed class Hold : IRiderDecisionModel
    { public RiderDecision Decide(TrackSegment segment, RiderState rider) => new(rider.Lane); }
}
