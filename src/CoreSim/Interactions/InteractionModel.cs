using CoreSim.Decisions;
using CoreSim.PhysicalSpace;

namespace CoreSim.Interactions;

public enum InteractionContext { FirstBendCluster, CornerEntryClosing, InsideOverlap, OutsideOverlap,
    MidCornerPressure, CornerExitCross, StraightReattack, MechanicalConflict }
public enum OverlapState { Behind, Approaching, PartialOverlap, SideBySide, Ahead }
public enum InteractionResponse { KeepIntent, Hold, CoverInside, YieldOutward, ContinueOutside, CutInside, BackOut, EmergencyAvoid }

/// <summary>Provisional synthetic controls; clearance does not inflate the mechanical motorcycle.</summary>
public sealed record ContestedSpaceParameters
{
    public double PressureClearanceMeters { get; init; } = .25;
    public double ReleaseClearanceMeters { get; init; } = .65;
    public double AlongsideCompetitiveClearanceMeters { get; init; } = .60;
    public double CompetitiveReachMeters { get; init; } = 2.5;
    public double ClusterTimeWindowSeconds { get; init; } = .30;
    public double ReleaseDelaySeconds { get; init; } = .45;
    public double EmergencyTimeSeconds { get; init; } = .35;
    public double MinimumCompetitiveClosingSpeedMetersPerSecond { get; init; } = .25;
    public int MaximumResponsePasses => 2;
    public int MaximumAlternativesPerRider => 3;
    public int MaximumJointCombinations => 81;
    public void Validate()
    {
        foreach (var value in new[] { PressureClearanceMeters, ReleaseClearanceMeters, AlongsideCompetitiveClearanceMeters, CompetitiveReachMeters,
            ClusterTimeWindowSeconds, ReleaseDelaySeconds, EmergencyTimeSeconds, MinimumCompetitiveClosingSpeedMetersPerSecond })
            if (!double.IsFinite(value) || value <= 0) throw new ArgumentOutOfRangeException(nameof(value));
        if (ReleaseClearanceMeters <= PressureClearanceMeters || CompetitiveReachMeters <= ReleaseClearanceMeters)
            throw new ArgumentException("Release and competitive reach must exceed pressure clearance.");
        if (AlongsideCompetitiveClearanceMeters >= ReleaseClearanceMeters)
            throw new ArgumentException("Alongside engagement must be below release clearance.");
    }
}

public sealed record InteractionGeometry(int RiderA, int RiderB, double CommonTimeSeconds,
    double ForwardBFromAMeters, double OutwardBFromAMeters, double ForwardClosingMetersPerSecond,
    double ForwardFootprintOverlapMeters, OverlapState AOverlap, OverlapState BOverlap,
    double TimeToConflictSeconds, ContestedSpaceEvent Space);
public sealed record InteractionAlternative(int RiderId, InteractionResponse Response,
    TrajectoryIntent Intent, RiderDriveControl? DriveControl, double TacticalPreference,
    string Reason, bool HoldLateralPosition = false);
public sealed record InteractionCost(double AdditionalTraversalTimeSeconds, double IntentDeviation,
    double TacticalPreference, double ClearanceShortfallMeters)
{
    public double Total => AdditionalTraversalTimeSeconds + IntentDeviation + TacticalPreference;
}
public sealed record InteractionCandidateDiagnostic(int Combination, bool Feasible, string Rejection,
    double MinimumSeparationMeters, InteractionCost Cost, IReadOnlyList<InteractionAlternative> Responses,
    int IneligibleIntervals = 0);
public sealed record UnresolvedMechanicalContact(long EpisodeId, int RiderA, int RiderB,
    double CommonTimeSeconds, BikeComponent ComponentA, BikeComponent ComponentB,
    double MinimumSeparationMeters, InteractionContext Context,
    IReadOnlyList<InteractionAlternative> AttemptedResponses, string Reason);
public sealed record InteractionEpisodeDiagnostic(long EpisodeId, IReadOnlyList<int> RiderIds,
    InteractionContext Context, double StartTimeSeconds, double? EndTimeSeconds,
    double InitialMinimumSeparationMeters, SpaceConflictKind InitialConflictKind,
    IReadOnlyList<InteractionGeometry> Geometry, IReadOnlyList<InteractionAlternative> OriginalIntents,
    IReadOnlyList<InteractionAlternative> ResponseAlternatives, IReadOnlyList<InteractionAlternative> SelectedResponses,
    IReadOnlyList<InteractionCandidateDiagnostic> Candidates, int PassCount,
    double FinalMinimumSeparationMeters, bool ResolvedWithoutMechanicalContact,
    IReadOnlyList<UnresolvedMechanicalContact> UnresolvedMechanicalContacts, bool LegacyFallbackUsed,
    int ResponseChanges, double ActiveDurationSeconds);
public sealed record InteractionWork(int JointCombinations, int ProductionResolutions,
    int NarrowPhaseEvaluations, int ResponsePasses, int Clusters);
public sealed record InteractionResolution(IReadOnlyList<InteractionEpisodeDiagnostic> Episodes, InteractionWork Work);

internal sealed class InteractionEpisode(long id, int[] riders, double start)
{
    internal long Id = id;
    internal int[] Riders = riders;
    internal double Start = start, LastActive = start;
    internal double? ClearSince, End;
    internal InteractionContext Context;
    internal bool PredictedMechanical, FallbackAttempted;
    internal int ResponseChanges;
    internal Dictionary<int, InteractionAlternative> Commitments = new();
    internal InteractionEpisodeDiagnostic? LastDiagnostic;
}

/// <summary>Owned by one heat. No process/engine shared state or randomness.</summary>
public sealed class InteractionEpisodeTracker
{
    private readonly List<InteractionEpisode> _episodes = new();
    private long _nextId;
    private (int Seed, int Heat)? _heat;
    internal readonly List<InteractionEpisodeDiagnostic> Closed = new();
    internal readonly List<PhysicalPoseInterval> History = new();
    internal TrackMetricEmbedding? Embedding;
    internal IEnumerable<InteractionEpisode> Active => _episodes.Where(e => !e.End.HasValue);
    internal InteractionEpisodeTracker Clone()
    {
        var clone = new InteractionEpisodeTracker { _nextId = _nextId, _heat = _heat, Embedding = Embedding };
        clone.History.AddRange(History);
        foreach (var e in _episodes) clone._episodes.Add(new(e.Id, e.Riders.ToArray(), e.Start)
        {
            LastActive = e.LastActive, ClearSince = e.ClearSince, End = e.End, Context = e.Context,
            PredictedMechanical = e.PredictedMechanical, FallbackAttempted = e.FallbackAttempted,
            ResponseChanges = e.ResponseChanges, Commitments = new(e.Commitments), LastDiagnostic = e.LastDiagnostic,
        });
        return clone;
    }
    internal void CommitFrom(InteractionEpisodeTracker resolved)
    {
        _nextId = resolved._nextId; _heat = resolved._heat; Embedding = resolved.Embedding;
        _episodes.Clear(); _episodes.AddRange(resolved._episodes);
        History.Clear(); History.AddRange(resolved.History);
        Closed.Clear();
    }
    internal void Bind(SimulationSnapshot snapshot)
    {
        Closed.Clear();
        var identity = (snapshot.Step.Seed, snapshot.Step.HeatId);
        if (_heat.HasValue && _heat.Value != identity)
            throw new InvalidOperationException("An interaction tracker belongs to exactly one heat.");
        _heat = identity;
        Embedding ??= new(snapshot.Track);
        Embedding.ValidateCompatible(snapshot.Track);
    }
    internal InteractionEpisode Engage(int[] riders, double time, InteractionContext context)
    {
        var matches = Active.Where(e => e.Riders.Intersect(riders).Count() >= 2).ToArray();
        var episode = matches.OrderBy(e => e.Start).ThenBy(e => e.Id).FirstOrDefault();
        if (episode is null)
        {
            episode = new(++_nextId, riders.Order().ToArray(), time);
            _episodes.Add(episode);
        }
        foreach (var merged in matches.Where(e => e != episode))
        {
            episode.FallbackAttempted |= merged.FallbackAttempted;
            foreach (var commitment in merged.Commitments) episode.Commitments.TryAdd(commitment.Key, commitment.Value);
            merged.End = time;
        }
        episode.Riders = episode.Riders.Union(riders).Order().ToArray();
        episode.LastActive = Math.Max(time, episode.LastActive);
        episode.ClearSince = null;
        if (episode.Commitments.Count == 0) episode.Context = context;
        return episode;
    }
    internal bool Owns(int a, int b) => Active.Any(e => e.Riders.Contains(a) && e.Riders.Contains(b));
    internal void ObserveClearance(double time, IReadOnlyList<ContestedSpaceEvent> rows, ContestedSpaceParameters p)
    {
        foreach (var e in Active.ToArray())
        {
            var relevant = rows.Where(r => e.Riders.Contains(r.RiderA) && e.Riders.Contains(r.RiderB)).ToArray();
            // Missing/ambiguous coverage is not evidence of clearance.
            if (relevant.Length == 0 || relevant.Any(r => !r.NumericallyResolved
                || r.Kind.HasFlag(SpaceConflictKind.BoundaryAmbiguous)
                || r.MinimumSeparationMeters <= p.ReleaseClearanceMeters))
            { e.ClearSince = null; continue; }
            e.ClearSince ??= time;
            if (time - e.ClearSince.Value >= p.ReleaseDelaySeconds)
            {
                e.End = time;
                if (e.LastDiagnostic is { } diagnostic) Closed.Add(diagnostic with
                    { EndTimeSeconds = time, ActiveDurationSeconds = Math.Max(0, time - e.Start), LegacyFallbackUsed = false });
            }
        }
    }
    internal int Retain(ResolvedSimulationStep resolved)
    {
        foreach (var motion in resolved.Motions)
            History.AddRange(ResolvedBikePoses.FromMotion(motion, resolved.Snapshot.Track, embedding: Embedding));
        var floor = resolved.Changes.Count == 0 ? 0 : resolved.Changes.Min(c => c.ElapsedTimeSeconds);
        return PruneHistory(floor);
    }
    internal int PruneHistory(double floor)
    {
        // #55 quarantine starts at a discontinuity and persists until physical
        // separation. Retain its causal history even when it exceeds the usual
        // two-second asynchronous overlap window; never promote it by pruning.
        var observed = CommonTimePoseHistory.Observe(History);
        var unresolvedPairs = observed.Intervals.GroupBy(r => (r.RiderA,r.RiderB))
            .Where(g => g.OrderBy(r => r.IntervalEndSeconds).Last() is var last
                && (!last.NumericallyResolved || last.Kind.HasFlag(SpaceConflictKind.BoundaryAmbiguous)))
            .Select(g => g.Key).ToHashSet();
        var protectedStart = observed.Intervals.Where(r => unresolvedPairs.Contains((r.RiderA,r.RiderB))
            && (!r.NumericallyResolved || r.Kind.HasFlag(SpaceConflictKind.BoundaryAmbiguous)))
            .Select(r => r.IntervalStartSeconds).DefaultIfEmpty(floor - 2).Min();
        var cutoff = Math.Min(floor - 2, protectedStart);
        History.RemoveAll(i => i.EndTimeSeconds < cutoff);
        return observed.Work.NarrowPhaseEvaluations;
    }
}

public static class InteractionGeometryModel
{
    /// <summary>Actual rotated #55 capsules projected onto the common reference tangent.</summary>
    public static InteractionGeometry Describe(ContestedSpaceEvent space, PhysicalBikePose a, PhysicalBikePose b,
        PoseRateBounds rateA, PoseRateBounds rateB)
    {
        var heading = BikeAngles.Interpolate(a.ReferenceTangentHeadingRadians, b.ReferenceTangentHeadingRadians, .5);
        var forward = new MeterPoint(Math.Cos(heading), Math.Sin(heading));
        var outward = new MeterPoint(forward.Y, -forward.X);
        var delta = b.Position - a.Position;
        var longitudinal = MeterPoint.Dot(delta, forward);
        var relative = MeterPoint.Dot(rateB.CenterVelocityMetersPerSecond - rateA.CenterVelocityMetersPerSecond, forward);
        var aa = Project(a.Footprint, forward); var bb = Project(b.Footprint, forward);
        var overlap = Math.Min(aa.Max, bb.Max) - Math.Max(aa.Min, bb.Min);
        OverlapState State(double otherAhead) => overlap > 0
            ? Math.Abs(otherAhead) < overlap * .5 ? OverlapState.SideBySide : OverlapState.PartialOverlap
            : otherAhead > 0 ? relative < 0 ? OverlapState.Approaching : OverlapState.Behind : OverlapState.Ahead;
        return new(space.RiderA, space.RiderB, a.CommonTimeSeconds, longitudinal, MeterPoint.Dot(delta, outward),
            relative, overlap, State(longitudinal), overlap > 0 ? State(-longitudinal)
                : longitudinal > 0 ? OverlapState.Ahead : relative > 0 ? OverlapState.Approaching : OverlapState.Behind,
            space.FirstTouchCommonTimeSeconds is { } touch ? Math.Max(0,touch-a.CommonTimeSeconds)
                : longitudinal * relative < 0 && Math.Abs(relative) > 1e-9
                    ? Math.Max(0,-overlap) / Math.Abs(relative) : double.MaxValue, space);
    }
    public static (double Min, double Max) Project(BikeFootprint footprint, MeterPoint axis)
    {
        var min = double.PositiveInfinity; var max = double.NegativeInfinity;
        foreach (var capsule in new[] { footprint.Chassis, footprint.Handlebar })
        {
            min = Math.Min(min, Math.Min(MeterPoint.Dot(capsule.Start, axis), MeterPoint.Dot(capsule.End, axis)) - capsule.RadiusMeters);
            max = Math.Max(max, Math.Max(MeterPoint.Dot(capsule.Start, axis), MeterPoint.Dot(capsule.End, axis)) + capsule.RadiusMeters);
        }
        return (min, max);
    }
    public static double ExecutionMarginMeters(RiderSnapshot rider, ContestedSpaceParameters p)
        => p.PressureClearanceMeters * (1.15 - .30 * (rider.Profile.Gameplay.Abilities.Technique - 1) / 98d)
            + .04 * (1 - rider.Condition);
}
