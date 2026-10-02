using CoreSim.Logging;
using CoreSim.Race;

namespace CoreSim.Decisions;

public enum TrajectoryPhase { Entry, Middle, Exit, FollowingStraight }
public sealed record TrajectoryHorizonSegment(int SegmentIndex, int LapIndex, TrajectoryPhase Phase);
public sealed record TrajectoryPhaseEndpoint(int SegmentIndex, TrajectoryPhase Phase,
    int RequestedAnchor, float LateralPosition, float SpeedMetersPerSecond, SegmentOutcome Outcome, bool AnchorReached);
/// <summary>Measured production results. Missing past/future endpoints remain null.</summary>
public sealed record TrajectoryTraversal(TrajectoryIntent Intent,
    double PredictedTraversalTimeSeconds, double PhysicalDistanceMeters,
    float? CornerEntrySpeedMetersPerSecond, float? EntryLateralPosition,
    float? MiddleLateralPosition, float? ApexRegionLateralPosition, float? ExitLateralPosition,
    float? CornerExitSpeedMetersPerSecond, float? FollowingStraightEndSpeedMetersPerSecond,
    double FollowingStraightTimeSeconds, double FollowingStraightDistanceMeters,
    bool CompletedHorizon, IReadOnlyList<TrajectoryPhaseEndpoint> PhaseEndpoints,
    IReadOnlyList<ResolvedRiderMotion> ResolvedMotions);

/// <summary>
/// Isolated solo CaptureSnapshot -> Decide -> Resolve -> Commit, with scripted targets.
/// Contains no speed, force, curvature, movement or traversal formula.
/// One instance shares only immutable setup between candidate replays.
/// </summary>
public sealed class TrajectoryEvaluator
{
    private readonly RiderDecisionContext _context;
    private readonly TrackStateSnapshot _surface;
    private readonly Dictionary<string, ResolvedSimulationStep>? _productionPrefixes;
    public IReadOnlyList<TrajectoryHorizonSegment> Horizon { get; }
    public int CandidateTraversalCount { get; private set; }
    public int ProductionResolutionCount { get; private set; }
    public TrajectoryEvaluator(RiderDecisionContext context, TrackStateSnapshot? perceivedSurface = null,
        bool reuseProductionPrefixes = true)
    {
        ArgumentNullException.ThrowIfNull(context);
        _context = context; _surface = perceivedSurface ?? context.TrackState;
        _productionPrefixes = reuseProductionPrefixes ? new() : null;
        if (_surface.SegmentCount != context.Snapshot.Track.Segments.Count || _surface.LinesCount != LaneModel.LanesCount)
            throw new ArgumentException("Surface dimensions must match the decision track.", nameof(perceivedSurface));
        if (!context.Rider.IsActive || context.Rider.SegmentIndex != context.SegmentIndex)
            throw new ArgumentException("Trajectory evaluation requires an active rider in the current segment.", nameof(context));
        Horizon = BuildHorizon(context);
    }
    public TrajectoryTraversal Evaluate(TrajectoryIntent intent, bool retainResolvedMotions = false)
    {
        CandidateTraversalCount++;
        var track = _context.Snapshot.Track;
        var rider = _context.Rider.ToMutableCopy();
        var riders = new[] { rider };
        var state = new TrackState(_surface.SegmentCount, _surface.LinesCount, _surface.GetSurface);
        var engine = new SimulationEngine(new ScriptedTrajectory(intent, Horizon, _context.StepNumber));
        var options = new HeatSimulationOptions
        {
            Laps = _context.Snapshot.Step.RequiredLaps, Seed = _context.Seed,
            IncidentFrequency = 0f, EnableLogging = false,
        };
        var log = new SimLog(false);
        var endpoints = new List<TrajectoryPhaseEndpoint>(Horizon.Count);
        var motions = new List<ResolvedRiderMotion>();
        double time = 0, distance = 0, straightTime = 0, straightDistance = 0;
        float? entrySpeed = null, entryLateral = null, middleLateral = null, apexLateral = null;
        float? exitLateral = null, exitSpeed = null, straightSpeed = null;
        var prefix = string.Empty;
        for (var offset = 0; offset < Horizon.Count
            && rider.Status is RiderRaceStatus.NotStarted or RiderRaceStatus.Racing; offset++)
        {
            var part = Horizon[offset];
            var step = _context.Snapshot.Step with
            {
                StepNumber = _context.StepNumber + offset,
                SegmentIndex = part.SegmentIndex, LapIndex = part.LapIndex,
            };
            var requested = Target(intent, part.Phase);
            prefix += (char)('0' + requested);
            // Equal scripted prefixes have exactly the same production state and own wear.
            // Retain immutable resolutions only within this one decision. Every candidate
            // still commits them to its own fresh copies; no candidate's mutable state is reused.
            if (_productionPrefixes is null || !_productionPrefixes.TryGetValue(prefix, out var resolved))
            {
                var input = engine.CaptureSnapshot(track, state, riders, step);
                resolved = engine.Resolve(input, engine.Decide(input), options);
                ProductionResolutionCount++;
                _productionPrefixes?.Add(prefix, resolved);
            }
            var snapshot = resolved.Snapshot;
            var motion = resolved.Motions.Single(); var change = resolved.Changes.Single();
            var reached = LateralSpaceModel.LateralDistanceMeters(motion.Final.LateralPosition,
                requested, snapshot.Segment.Type, track.Geometry) <= LateralMovementModel.LaneArrivalToleranceMeters;
            endpoints.Add(new(part.SegmentIndex, part.Phase, requested, motion.Final.LateralPosition,
                motion.Final.SpeedMetersPerSecond, change.Outcome, reached));
            time += motion.TotalTimeSeconds; distance += motion.TotalDistanceMeters;
            if (retainResolvedMotions) motions.Add(motion);
            if (snapshot.Segment.Type != SegmentType.Straight)
            {
                entrySpeed ??= motion.Initial.SpeedMetersPerSecond;
                if (part.Phase == TrajectoryPhase.Entry) entryLateral = motion.Final.LateralPosition;
                if (part.Phase == TrajectoryPhase.Middle) middleLateral = motion.Final.LateralPosition;
                if (part.Phase == TrajectoryPhase.Exit) exitLateral = motion.Final.LateralPosition;
                var corner = track.CornerTopology.CornerForSegment(part.SegmentIndex)!;
                var apexProgress = (double)part.LapIndex * track.Segments.Count
                    + corner.StartSegmentIndex + corner.SegmentCount * (double)ContinuousCornerEnvelope.ApexProgress;
                if (motion.Initial.CanonicalProgress <= apexProgress && motion.Final.CanonicalProgress >= apexProgress)
                    apexLateral = SampleAtProgress(motion, apexProgress).LateralPosition;
                if (part.SegmentIndex == corner.EndSegmentIndex)
                {
                    exitLateral = motion.Final.LateralPosition; exitSpeed = motion.Final.SpeedMetersPerSecond;
                }
            }
            if (part.Phase == TrajectoryPhase.FollowingStraight)
            {
                straightTime += motion.TotalTimeSeconds; straightDistance += motion.TotalDistanceMeters;
                straightSpeed = motion.Final.SpeedMetersPerSecond;
            }
            engine.Commit(resolved, riders, state, log);
        }
        var completed = endpoints.Count == Horizon.Count && rider.Status != RiderRaceStatus.Crashed;
        return new(intent, time, distance, entrySpeed, entryLateral, middleLateral, apexLateral,
            exitLateral, exitSpeed, straightSpeed, straightTime, straightDistance, completed,
            endpoints.AsReadOnly(), motions.AsReadOnly());
    }
    public static int Target(TrajectoryIntent intent, TrajectoryPhase phase) => phase switch
    {
        TrajectoryPhase.Middle => intent.ApexTarget,
        TrajectoryPhase.Exit or TrajectoryPhase.FollowingStraight => intent.ExitTarget,
        _ => intent.EntryTarget,
    };
    private static IReadOnlyList<TrajectoryHorizonSegment> BuildHorizon(RiderDecisionContext context)
    {
        var track = context.Snapshot.Track; var count = track.Segments.Count;
        var finish = (long)context.Snapshot.Step.RequiredLaps * count;
        var start = (long)context.Rider.Position.LapsCompleted * count + context.SegmentIndex;
        var result = new List<TrajectoryHorizonSegment>();
        (int Lap, int Corner)? selectedCorner = null;
        var afterCorner = false;
        for (var progress = start; progress < finish; progress++)
        {
            var index = (int)(progress % count); var lap = (int)(progress / count);
            var corner = track.CornerTopology.CornerForSegment(index);
            if (corner is not null)
            {
                var occurrence = (lap, corner.CornerId);
                if (afterCorner || (selectedCorner.HasValue && selectedCorner.Value != occurrence)) break;
                selectedCorner ??= occurrence;
            }
            else if (selectedCorner.HasValue) afterCorner = true;
            var phase = afterCorner ? TrajectoryPhase.FollowingStraight : track.Segments[index].Type switch
            {
                SegmentType.TurnMiddle => TrajectoryPhase.Middle,
                SegmentType.TurnExit => TrajectoryPhase.Exit,
                _ => TrajectoryPhase.Entry,
            };
            result.Add(new(index, lap, phase));
        }
        return result.AsReadOnly();
    }
    private static RiderMotionSample SampleAtProgress(ResolvedRiderMotion motion, double progress)
    {
        for (var i = 1; i < motion.Nodes.Count; i++)
        {
            var before = motion.Nodes[i - 1]; var after = motion.Nodes[i];
            if (after.CanonicalProgress < progress) continue;
            if (after.CanonicalProgress == progress) return after;
            var fraction = (progress - before.CanonicalProgress) / (after.CanonicalProgress - before.CanonicalProgress);
            return motion.SampleAtTime((float)(before.LocalTimeSeconds
                + fraction * (after.LocalTimeSeconds - before.LocalTimeSeconds)));
        }
        return motion.Final;
    }
    private sealed class ScriptedTrajectory(TrajectoryIntent intent,
        IReadOnlyList<TrajectoryHorizonSegment> horizon, int firstStep) : IRiderDecisionModel
    {
        public RiderDecision Decide(TrackSegment segment, RiderState rider) => new(intent.TargetFor(segment.Type));
        public RiderDecision Decide(RiderDecisionContext context)
            => new(Target(intent, horizon[context.StepNumber - firstStep].Phase));
    }
}
