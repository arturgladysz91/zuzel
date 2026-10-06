using CoreSim.Decisions;
using CoreSim.PhysicalSpace;

namespace CoreSim.Interactions;

public enum InteractionContext { FirstBendCluster, CornerEntryClosing, InsideOverlap, OutsideOverlap,
    MidCornerPressure, CornerExitCross, StraightReattack, MechanicalConflict }
public enum OverlapState { Behind, Approaching, PartialOverlap, SideBySide, Ahead }
public enum InteractionResponse { KeepIntent, Hold, CoverInside, YieldOutward, ContinueOutside, CutInside, BackOut, EmergencyAvoid }
public enum InteractionDiagnosticsLevel { None, Summary, FullAudit }
public enum InteractionSkillDomain { Offensive, Defensive, Safety, Neutral }
public enum InteractionTacticalRole { Attacker, Defender, Neutral }

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
    IReadOnlyList<InteractionAlternative> AttemptedResponses, string Reason)
{
    public bool LegacyFallbackAuthorized { get; init; }
}
public sealed record InteractionFallbackProvenance(long OriginEpisodeId, IReadOnlyList<int> RiderIds,
    bool Attempted = false, int? RiderA = null, int? RiderB = null, double? CommonTimeSeconds = null);
internal sealed record FallbackDecision(ContestedSpaceEvent Contact, bool Authorized, string Reason);
public sealed record InteractionEpisodeDiagnostic(long EpisodeId, IReadOnlyList<int> RiderIds,
    InteractionContext Context, double StartTimeSeconds, double? EndTimeSeconds,
    double InitialMinimumSeparationMeters, SpaceConflictKind InitialConflictKind,
    IReadOnlyList<InteractionGeometry> Geometry, IReadOnlyList<InteractionAlternative> OriginalIntents,
    IReadOnlyList<InteractionAlternative> ResponseAlternatives, IReadOnlyList<InteractionAlternative> SelectedResponses,
    IReadOnlyList<InteractionCandidateDiagnostic> Candidates, int PassCount,
    double FinalMinimumSeparationMeters, bool ResolvedWithoutMechanicalContact,
    IReadOnlyList<UnresolvedMechanicalContact> UnresolvedMechanicalContacts, bool LegacyFallbackUsed,
    int ResponseChanges, double ActiveDurationSeconds)
{
    public bool Pass1ActualMechanicalContact { get; init; }
    public bool ActualClearanceCertifiedByReplay { get; init; }
    public IReadOnlyList<InteractionAlternative> Pass1SelectedResponses { get; init; } = Array.Empty<InteractionAlternative>();
    public bool WithinCompetitiveReach { get; init; }
    public bool CompetitiveReachCoverageCertified { get; init; }
    public bool MergedOtherRiders { get; init; }
    public IReadOnlyList<long> MergedEpisodeIds { get; init; } = Array.Empty<long>();
    public IReadOnlyList<InteractionFallbackProvenance> FallbackProvenance { get; init; } = Array.Empty<InteractionFallbackProvenance>();
}
public sealed record InteractionWork(int JointCombinations, int ProductionResolutions,
    int NarrowPhaseEvaluations, int ResponsePasses, int Clusters)
{
    public int UniqueRiderAlternativeProjections { get; init; }
    public int PairAlternativeChecks { get; init; }
    public int JointCombinationsScored => JointCombinations;
    public int ActualProductionVerifications { get; init; }
    public int SafetyPasses { get; init; }
    public int LegacyFallbackAttempts { get; init; }
    public int SafetyContactComponents { get; init; }
    public int FinalContactComponents { get; init; }
    public int SafetyJointCombinations { get; init; }
}
public sealed record InteractionResolution(IReadOnlyList<InteractionEpisodeDiagnostic> Episodes, InteractionWork Work)
{
    [System.Text.Json.Serialization.JsonIgnore]
    public PhysicalContactAnalysis? PhysicalContactAnalysis { get; init; }
}

internal sealed class InteractionEpisode(long id, int[] riders, double start)
{
    internal long Id = id;
    internal int[] Riders = riders;
    internal double Start = start, LastActive = start;
    internal double ObservedUntil = start;
    internal double ReleaseNotBefore = start;
    internal int[] ObservedRiders = riders;
    internal double? ClearSince, End;
    internal InteractionContext Context;
    internal bool PredictedMechanical, MergedOtherRiders;
    internal Dictionary<long, InteractionFallbackProvenance> FallbackOrigins = new()
        { [id] = new(id, riders.Order().ToArray()) };
    internal SortedSet<long> MergedEpisodeIds = new();
    internal bool FallbackAttempted
    {
        get => FallbackOrigins.Values.Any(p => p.Attempted);
        set
        {
            foreach (var key in FallbackOrigins.Keys.ToArray())
                FallbackOrigins[key] = FallbackOrigins[key] with { Attempted = value };
        }
    }
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
    internal int EpisodeCount => _episodes.Count;
    internal InteractionEpisodeTracker Clone()
    {
        var clone = new InteractionEpisodeTracker { _nextId = _nextId, _heat = _heat, Embedding = Embedding };
        clone.History.AddRange(History);
        foreach (var e in _episodes) clone._episodes.Add(new(e.Id, e.Riders.ToArray(), e.Start)
        {
            LastActive = e.LastActive, ObservedUntil = e.ObservedUntil, ReleaseNotBefore=e.ReleaseNotBefore, ObservedRiders = e.ObservedRiders.ToArray(),
            ClearSince = e.ClearSince, End = e.End, Context = e.Context,
            PredictedMechanical = e.PredictedMechanical, FallbackOrigins = new(e.FallbackOrigins),
            MergedEpisodeIds = new(e.MergedEpisodeIds), MergedOtherRiders = e.MergedOtherRiders,
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
        return Engage(riders, time, context, matches);
    }
    // Only a connected component built from eligible #55 interaction edges may
    // reconcile an edge touching one member of each previously active battle.
    // A temporary optimization scope never reaches this entry point.
    internal InteractionEpisode Reconcile(ContestedSpaceInteractionCoordinator.Cluster component, double time)
    {
        if (!component.Meaningful || component.Edges.Length == 0
            || component.Edges.Any(g => !g.Space.NumericallyResolved
                || g.Space.Kind.HasFlag(SpaceConflictKind.BoundaryAmbiguous)))
            throw new ArgumentException("Episode reconciliation requires certified interaction geometry.");
        var reached = new HashSet<int> { component.Edges[0].RiderA };
        for (var pass=0;pass<4;pass++) foreach (var edge in component.Edges)
            if (reached.Contains(edge.RiderA) || reached.Contains(edge.RiderB))
            { reached.Add(edge.RiderA); reached.Add(edge.RiderB); }
        if (!reached.SetEquals(component.Riders))
            throw new ArgumentException("An optimization scope is not a connected interaction component.");
        var matches = Active.Where(e => e.Riders.Intersect(component.Riders).Any()).ToArray();
        return Engage(component.Riders, time, component.Context, matches);
    }
    private InteractionEpisode Engage(int[] riders, double time, InteractionContext context, InteractionEpisode[] matches)
    {
        var episode = matches.OrderBy(e => e.Start).ThenBy(e => e.Id).FirstOrDefault();
        if (episode is null)
        {
            episode = new(++_nextId, riders.Order().ToArray(), time);
            _episodes.Add(episode);
        }
        foreach (var merged in matches.Where(e => e != episode))
        {
            episode.MergedOtherRiders = true;
            episode.MergedEpisodeIds.Add(merged.Id);
            episode.MergedEpisodeIds.UnionWith(merged.MergedEpisodeIds);
            foreach (var origin in merged.FallbackOrigins) episode.FallbackOrigins.TryAdd(origin.Key, origin.Value);
            foreach (var commitment in merged.Commitments) episode.Commitments.TryAdd(commitment.Key, commitment.Value);
            episode.ResponseChanges += merged.ResponseChanges;
            episode.PredictedMechanical |= merged.PredictedMechanical;
            episode.LastActive = Math.Max(episode.LastActive, merged.LastActive);
            episode.ReleaseNotBefore = Math.Max(episode.ReleaseNotBefore, merged.ReleaseNotBefore);
            episode.ClearSince = null;
            episode.ObservedUntil = Math.Min(episode.ObservedUntil, merged.ObservedUntil);
            merged.End = time;
        }
        var participants = episode.Riders.Union(riders).Union(matches.SelectMany(e => e.Riders)).Order().ToArray();
        if (!participants.SequenceEqual(episode.Riders))
        { episode.ClearSince = null; episode.ObservedUntil = Math.Min(episode.ObservedUntil,time); episode.MergedOtherRiders = true; }
        episode.Riders = participants;
        episode.LastActive = Math.Max(time, episode.LastActive);
        // Forecast threats must not reset certified clearance of the selected
        // executed response. ObserveClearance alone owns this physical clock.
        if (episode.Commitments.Count == 0) episode.Context = context;
        return episode;
    }
    internal IReadOnlyList<FallbackDecision> AuthorizeFallbacks(
        InteractionEpisode episode, IReadOnlyList<ContestedSpaceEvent> contacts, bool safetyEvaluated)
    {
        var result = new List<FallbackDecision>();
        foreach (var contact in contacts.OrderByDescending(c => episode.FallbackOrigins.Values.Any(p =>
                p.RiderIds.Contains(c.RiderA) && p.RiderIds.Contains(c.RiderB)))
            .ThenBy(c => c.FirstTouchCommonTimeSeconds)
            .ThenBy(c => Math.Min(c.RiderA,c.RiderB)).ThenBy(c => Math.Max(c.RiderA,c.RiderB)))
        {
            var a = Math.Min(contact.RiderA,contact.RiderB); var b = Math.Max(contact.RiderA,contact.RiderB);
            if (episode.FallbackOrigins.Values.Any(p => p.Attempted && (p.RiderA == a && p.RiderB == b
                    || p.RiderA is null && p.RiderIds.Contains(a) && p.RiderIds.Contains(b))))
            { result.Add(new(contact,false,"This contact already consumed a source episode's fallback allowance")); continue; }
            if (!safetyEvaluated)
            { result.Add(new(contact,false,"New final contact was not evaluated by the bounded safety pass; no fallback authorized")); continue; }
            // Preserve one allowance per originating continuous battle. A bridge
            // creates no allowance. Prefer the contact's original battle; a new
            // bridge/new participant contact may spend another unused origin
            // in this verified connected battle, but never reroll a handled pair.
            var origin = episode.FallbackOrigins.Values.Where(p => !p.Attempted)
                .OrderByDescending(p => p.RiderIds.Contains(a) && p.RiderIds.Contains(b))
                .ThenByDescending(p => p.RiderIds.Contains(a) || p.RiderIds.Contains(b))
                .ThenBy(p => p.OriginEpisodeId).FirstOrDefault();
            if (origin is null)
            { result.Add(new(contact,false,"Continuous episode has no unused originating fallback allowance")); continue; }
            episode.FallbackOrigins[origin.OriginEpisodeId] = origin with
                { Attempted = true, RiderA = a, RiderB = b, CommonTimeSeconds = contact.FirstTouchCommonTimeSeconds };
            result.Add(new(contact,true,"Authorized one legacy fallback from an unused originating episode allowance"));
        }
        return result;
    }
    internal bool Owns(int a, int b) => Active.Any(e => e.Riders.Contains(a) && e.Riders.Contains(b));
    internal void ObserveClearance(double time, IReadOnlyList<ContestedSpaceEvent> rows, ContestedSpaceParameters p,
        SimulationSnapshot? snapshot = null)
    {
        foreach (var e in Active.ToArray())
        {
            var riders = e.Riders.Where(id => snapshot is null || snapshot.Rider(id).IsActive).ToArray();
            if (snapshot is not null && riders.Length>=2)
                e.LastActive=Math.Max(e.LastActive,riders.Min(id=>snapshot.Rider(id).ElapsedTimeSeconds));
            if (!riders.SequenceEqual(e.ObservedRiders))
            {
                var removed = e.ObservedRiders.Except(riders).ToArray();
                e.ClearSince = null;
                if (snapshot is not null && removed.Length > 0)
                    e.ObservedUntil = Math.Max(e.ObservedUntil,removed.Max(id => snapshot.Rider(id).ElapsedTimeSeconds));
                e.ObservedRiders = riders;
            }
            if (riders.Length < 2)
            {
                // No competitive episode remains with fewer than two active riders.
                // Closing ownership adds no consequence or post-impact model.
                e.End = Math.Max(e.LastActive, riders.Length == 0 || snapshot is null ? time
                    : snapshot.Rider(riders[0]).ElapsedTimeSeconds);
                if (e.LastDiagnostic is { } last) Closed.Add(last with
                    { EndTimeSeconds = e.End, ActiveDurationSeconds = Math.Max(0,e.End.Value-e.Start), LegacyFallbackUsed = false });
                continue;
            }
            var relevant = rows.Where(r => riders.Contains(r.RiderA) && riders.Contains(r.RiderB)).ToArray();
            var pairs = (from a in riders from b in riders where a < b select (a,b)).ToArray();
            var byPair = relevant.GroupBy(r => (r.RiderA,r.RiderB))
                .ToDictionary(g => g.Key,g => g.OrderBy(r => r.IntervalStartSeconds).ToArray());
            if (pairs.Any(pair => !byPair.ContainsKey(pair))) {e.ClearSince=null;continue;}
            var sharedEnd = pairs.Min(pair => byPair[pair].Max(r => r.IntervalEndSeconds));
            e.LastActive=Math.Max(e.LastActive,sharedEnd);
            // #55 certifies a pair-wide minimum. Its local lower bound is required for release clearance.
            var lastPressureEnd = relevant.Where(r => r.IntervalEndSeconds>e.ObservedUntil
                && ((!r.NumericallyResolved && r.MinimumSeparationLowerBoundMeters <= p.CompetitiveReachMeters)
                    || r.Kind.HasFlag(SpaceConflictKind.BoundaryAmbiguous)
                    || r.MinimumSeparationMeters<=p.ReleaseClearanceMeters
                    || r.MinimumSeparationLowerBoundMeters<=p.ReleaseClearanceMeters))
                .Select(r => r.IntervalEndSeconds).DefaultIfEmpty(e.ObservedUntil).Max();
            if (lastPressureEnd>e.ObservedUntil) {e.ClearSince=null;e.ObservedUntil=lastPressureEnd;}
            if (sharedEnd<=e.ObservedUntil) continue;
            // Actual sustained, certified distance beyond competitive reach can end
            // an old forecast commitment. Unknown/boundary rows still fail closed.
            var outsideReach = relevant.Where(r => r.IntervalEndSeconds > e.ObservedUntil).ToArray();
            var certifiedOutsideReach = outsideReach.Length > 0 && outsideReach.All(r => !r.Kind.HasFlag(SpaceConflictKind.BoundaryAmbiguous)
                && r.MinimumSeparationLowerBoundMeters > p.CompetitiveReachMeters);
            if (!certifiedOutsideReach && sharedEnd <= e.ReleaseNotBefore)
            {
                // Do not turn a future forecast deadline into an observed clock:
                // a later actual clear interval must still be examined for reach.
                e.ClearSince = null; e.ObservedUntil = sharedEnd; continue;
            }
            if (!certifiedOutsideReach) e.ObservedUntil=Math.Max(e.ObservedUntil,e.ReleaseNotBefore);
            if (sharedEnd<=e.ObservedUntil) continue;
            var boundaries = relevant.SelectMany(r => new[]{r.IntervalStartSeconds,r.IntervalEndSeconds})
                .Where(t => t > e.ObservedUntil && t<=sharedEnd).Append(e.ObservedUntil).Append(sharedEnd).Distinct().Order().ToArray();
            var indices = pairs.ToDictionary(pair => pair,_ => 0);
            for (var i=0;i+1<boundaries.Length;i++)
            {
                var start = boundaries[i]; var end = boundaries[i+1]; var clear = true;
                foreach (var pair in pairs)
                {
                    if (!byPair.TryGetValue(pair,out var intervals)) {clear=false;break;}
                    var index=indices[pair];
                    while (index<intervals.Length && intervals[index].IntervalEndSeconds<=start) index++;
                    indices[pair]=index;
                    if (index==intervals.Length || intervals[index].IntervalStartSeconds>start || intervals[index].IntervalEndSeconds<end)
                    {clear=false;break;}
                    var row=intervals[index];
                    if ((!row.NumericallyResolved && row.MinimumSeparationLowerBoundMeters <= p.CompetitiveReachMeters)
                        || row.Kind.HasFlag(SpaceConflictKind.BoundaryAmbiguous)
                        || row.MinimumSeparationMeters<=p.ReleaseClearanceMeters
                        || row.MinimumSeparationLowerBoundMeters<=p.ReleaseClearanceMeters) {clear=false;break;}
                }
                if (!clear) e.ClearSince=null;
                else
                {
                    e.ClearSince ??= start;
                    if (end-e.ClearSince.Value>=p.ReleaseDelaySeconds)
                    {
                        e.End=e.ClearSince.Value+p.ReleaseDelaySeconds;
                        if (e.LastDiagnostic is { } diagnostic) Closed.Add(diagnostic with
                            {EndTimeSeconds=e.End,ActiveDurationSeconds=Math.Max(0,e.End.Value-e.Start),LegacyFallbackUsed=false});
                        break;
                    }
                }
                e.ObservedUntil=end;
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
    public static InteractionTacticalRole Role(int riderId, InteractionGeometry geometry)
    {
        var direction = geometry.RiderA == riderId ? 1 : -1;
        var ahead = direction * geometry.ForwardBFromAMeters;
        var outside = direction * geometry.OutwardBFromAMeters;
        var closing = direction * geometry.ForwardClosingMetersPerSecond;
        if (geometry.ForwardFootprintOverlapMeters > 0 && outside < 0)
            return InteractionTacticalRole.Defender;
        if (ahead > 0 && closing < 0 || geometry.ForwardFootprintOverlapMeters > 0 && outside > 0 && closing <= 0)
            return InteractionTacticalRole.Attacker;
        return ahead < 0 ? InteractionTacticalRole.Defender : InteractionTacticalRole.Neutral;
    }
    public static InteractionSkillDomain SkillDomain(InteractionResponse response, InteractionTacticalRole role)
        => response switch
        {
            InteractionResponse.BackOut or InteractionResponse.EmergencyAvoid => InteractionSkillDomain.Safety,
            InteractionResponse.CoverInside or InteractionResponse.YieldOutward => InteractionSkillDomain.Defensive,
            InteractionResponse.CutInside => InteractionSkillDomain.Offensive,
            InteractionResponse.Hold or InteractionResponse.ContinueOutside => role switch
            {
                InteractionTacticalRole.Attacker => InteractionSkillDomain.Offensive,
                InteractionTacticalRole.Defender => InteractionSkillDomain.Defensive,
                _ => InteractionSkillDomain.Neutral,
            },
            _ => InteractionSkillDomain.Neutral,
        };
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
        for (var component = 0; component < 2; component++)
        {
            var capsule = footprint.Component((BikeComponent)component);
            min = Math.Min(min, Math.Min(MeterPoint.Dot(capsule.Start, axis), MeterPoint.Dot(capsule.End, axis)) - capsule.RadiusMeters);
            max = Math.Max(max, Math.Max(MeterPoint.Dot(capsule.Start, axis), MeterPoint.Dot(capsule.End, axis)) + capsule.RadiusMeters);
        }
        return (min, max);
    }
    public static double ExecutionMarginMeters(RiderSnapshot rider, ContestedSpaceParameters p)
        => p.PressureClearanceMeters * (1.15 - .30 * (rider.Profile.Gameplay.Abilities.Technique - 1) / 98d)
            + .04 * (1 - rider.Condition);
}
