using CoreSim.Logging;
using CoreSim.Race;

namespace CoreSim.Decisions;

public enum TrajectoryPhase { Entry, Middle, Exit, FollowingStraight }
/// <summary>Temporary traffic execution controls expire at the next production snapshot.</summary>
public sealed record InteractionProjectionControl(RiderDriveControl? CurrentStepDriveControl,
    bool CurrentStepHoldLateralPosition, InteractionLateralTarget? PhysicalTarget = null)
{
    internal bool IsControlled => CurrentStepDriveControl.HasValue || CurrentStepHoldLateralPosition || PhysicalTarget.HasValue;
    internal RiderDecision Decision(int target, int prefix) => new(target)
    {
        DriveControl = prefix == 0 ? CurrentStepDriveControl : null,
        HoldLateralPosition = prefix == 0 && CurrentStepHoldLateralPosition,
        InteractionTarget = PhysicalTarget,
    };
}
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
    private readonly bool _reusePrefixes;
    private readonly InteractionProjectionControl _control;
    private PrefixNode? _leanRoot, _richRoot;
    private sealed class PrefixNode(RiderSnapshot rider, TrackState surface, TrajectoryTraversal metrics)
    {
        internal RiderSnapshot Rider { get; } = rider;
        internal TrackState Surface { get; } = surface;
        internal TrajectoryTraversal Metrics { get; } = metrics;
        internal PrefixNode?[]? Children { get; set; }
    }
    public IReadOnlyList<TrajectoryHorizonSegment> Horizon { get; }
    public int CandidateTraversalCount { get; private set; }
    public int ProductionResolutionCount { get; private set; }
    public TrajectoryEvaluator(RiderDecisionContext context, TrackStateSnapshot? perceivedSurface = null,
        bool reuseProductionPrefixes = true, RiderDriveControl? driveControl = null, bool holdLateralPosition = false,
        InteractionLateralTarget? physicalTarget = null)
    {
        ArgumentNullException.ThrowIfNull(context);
        _context = context; _surface = perceivedSurface ?? context.TrackState;
        _reusePrefixes = reuseProductionPrefixes;
        _control = new(driveControl, holdLateralPosition, physicalTarget);
        if (_surface.SegmentCount != context.Snapshot.Track.Segments.Count || _surface.LinesCount != LaneModel.LanesCount)
            throw new ArgumentException("Surface dimensions must match the decision track.", nameof(perceivedSurface));
        if (!context.Rider.IsActive || context.Rider.SegmentIndex != context.SegmentIndex)
            throw new ArgumentException("Trajectory evaluation requires an active rider in the current segment.", nameof(context));
        Horizon = BuildHorizon(context);
    }
    public TrajectoryTraversal Evaluate(TrajectoryIntent intent, bool retainResolvedMotions = false)
    {
        if (_control.IsControlled && !retainResolvedMotions)
            throw new InvalidOperationException("Controlled traffic projection requires production motion capture.");
        CandidateTraversalCount++;
        ref var root = ref (retainResolvedMotions ? ref _richRoot : ref _leanRoot);
        if (!_reusePrefixes || root is null)
            root = new(_context.Rider,
                new TrackState(_surface.SegmentCount, _surface.LinesCount, _surface.GetSurface),
                new(default, 0, 0, null, null, null, null, null, null, null, 0, 0,
                    false, Array.Empty<TrajectoryPhaseEndpoint>(), Array.Empty<ResolvedRiderMotion>()));
        var node = root;
        for (var offset = 0; offset < Horizon.Count && node.Rider.IsActive; offset++)
        {
            var part = Horizon[offset]; var requested = Target(intent, part.Phase);
            node.Children ??= new PrefixNode?[LaneModel.LanesCount];
            if (node.Children[requested] is not { } child)
            {
                child = ResolvePrefix(node, part, offset, requested, retainResolvedMotions);
                node.Children[requested] = child;
                ProductionResolutionCount++;
            }
            else ProjectionCaptureAudit.Record(ProjectionMaterialization.PrefixCacheHit);
            node = child;
        }
        return node.Metrics with { Intent = intent,
            CompletedHorizon = node.Metrics.PhaseEndpoints.Count == Horizon.Count
                && node.Rider.Status != RiderRaceStatus.Crashed };
    }

    private PrefixNode ResolvePrefix(PrefixNode parent, TrajectoryHorizonSegment part,
        int offset, int requested, bool rich)
    {
        var track = _context.Snapshot.Track;
        var step = _context.Snapshot.Step with
        {
            StepNumber = _context.StepNumber + offset, SegmentIndex = part.SegmentIndex, LapIndex = part.LapIndex,
        };
        var state = parent.Surface.Clone();
        var input = new SimulationSnapshot(step, track, parent.Surface.Snapshot(), new[] { parent.Rider });
        var options = new HeatSimulationOptions { Laps = step.RequiredLaps, Seed = _context.Seed,
            IncidentFrequency = 0f, EnableLogging = false,
            EnableContestedSpaceResponses = (_control.IsControlled && offset == 0) || _control.PhysicalTarget.HasValue || parent.Rider.ContactRecovery is not null,
            EnablePhysicalContactConsequences = parent.Rider.ContactRecovery is not null };
        SoloProjectionResult projection;
        ResolvedRiderMotion? motion = null;
        if (rich)
        {
            var engine = new SimulationEngine(new FixedTarget(requested));
            var resolved = (_control.IsControlled && offset == 0) || _control.PhysicalTarget.HasValue || parent.Rider.ContactRecovery is not null
                ? engine.ResolveProduction(input, new[] { new RiderIntent(parent.Rider.RiderId,
                    _control.Decision(requested, offset)) }, options, legacyContacts: false)
                : engine.Resolve(input, engine.Decide(input), options);
            motion = resolved.Motions[0];
            projection = new(input, resolved.Changes[0], motion.TotalTimeSeconds, motion.TotalDistanceMeters, null, default);
            var rider = parent.Rider.ToMutableCopy();
            engine.Commit(resolved, new[] { rider }, state, new SimLog(false));
        }
        else
        {
            projection = SimulationEngine.ResolveSoloProjection(input, parent.Rider, new(requested), options);
            SimulationEngine.CommitProjectionWear(projection, state);
        }
        var change = projection.Change;
        var previous = parent.Metrics;
        var entrySpeed = previous.CornerEntrySpeedMetersPerSecond;
        var entry = previous.EntryLateralPosition; var middle = previous.MiddleLateralPosition;
        var apex = previous.ApexRegionLateralPosition; var exit = previous.ExitLateralPosition;
        var exitSpeed = previous.CornerExitSpeedMetersPerSecond;
        var straightSpeed = previous.FollowingStraightEndSpeedMetersPerSecond;
        var straightTime = previous.FollowingStraightTimeSeconds; var straightDistance = previous.FollowingStraightDistanceMeters;
        var reached = LateralSpaceModel.LateralDistanceMeters(change.LateralPosition, requested,
            input.Segment.Type, track.Geometry) <= LateralMovementModel.LaneArrivalToleranceMeters;
        var endpoint = new TrajectoryPhaseEndpoint(part.SegmentIndex, part.Phase, requested,
            change.LateralPosition, change.Speed, change.Outcome, reached);
        if (input.Segment.Type != SegmentType.Straight)
        {
            var corner = track.CornerTopology.CornerForSegment(part.SegmentIndex)!;
            if (part.SegmentIndex == corner.StartSegmentIndex && parent.Rider.SegmentProgress == 0f)
                entrySpeed ??= parent.Rider.Speed;
            if (part.Phase == TrajectoryPhase.Entry) entry = change.LateralPosition;
            if (part.Phase == TrajectoryPhase.Middle) middle = change.LateralPosition;
            if (part.Phase == TrajectoryPhase.Exit) exit = change.LateralPosition;
            var apexProgress = (double)part.LapIndex * track.Segments.Count
                + corner.StartSegmentIndex + corner.SegmentCount * (double)ContinuousCornerEnvelope.ApexProgress;
            if (parent.Rider.CanonicalProgress <= apexProgress && change.Position.TotalSegmentProgress >= apexProgress)
                apex = motion is not null ? SampleAtProgress(motion, apexProgress).LateralPosition : projection.ApexLateralPosition;
            if (part.SegmentIndex == corner.EndSegmentIndex)
            { exit = change.LateralPosition; exitSpeed = change.Speed; }
        }
        if (part.Phase == TrajectoryPhase.FollowingStraight)
        {
            straightTime += projection.TravelTimeSeconds; straightDistance += projection.DistanceMeters; straightSpeed = change.Speed;
        }
        var endpoints = previous.PhaseEndpoints.Append(endpoint).ToArray();
        var motions = motion is not null ? previous.ResolvedMotions.Append(motion).ToArray() : Array.Empty<ResolvedRiderMotion>();
        var metrics = new TrajectoryTraversal(default, previous.PredictedTraversalTimeSeconds + projection.TravelTimeSeconds,
            previous.PhysicalDistanceMeters + projection.DistanceMeters, entrySpeed, entry, middle, apex, exit, exitSpeed, straightSpeed,
            straightTime, straightDistance, false, Array.AsReadOnly(endpoints), Array.AsReadOnly(motions));
        return new(parent.Rider.Apply(change), state, metrics);
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
    private sealed class FixedTarget(int target) : IRiderDecisionModel
    {
        public RiderDecision Decide(TrackSegment segment, RiderState rider) => new(target);
    }
}
