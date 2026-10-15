using CoreSim.Decisions;
using CoreSim.Interactions;
using CoreSim.PhysicalSpace;

namespace CoreSim.Race;

internal enum CausalRunStatus { HorizonReached, ContactResolved, InterruptedByCrash, Refused, NextContactPending }

/// <summary>The boundary is a solved accumulator, never a sampled position.</summary>
internal sealed record CausalRiderState(RiderSnapshot Rider, double ExecutedThroughSeconds,
    SimulationSnapshot? Origin, RiderIntent? Intent, ExecutedTraversalBoundary? Execution, RiderIntegrationContext? Integration);
internal sealed record CausalHistory(CausalHistory? Previous, double FromSeconds, double ThroughSeconds,
    IReadOnlyList<PhysicalPoseInterval> Poses, ContestedSpaceReport Certificate);
internal sealed record CausalContact(double TimeSeconds, int RiderA, int RiderB,
    PhysicalContactConsequencePlan Plan, PhysicalPairGeneration Generation,
    ContestedSpaceReport Certificate, double MaximumStateTimeErrorSeconds, double IntegratorClockSkewSeconds,
    double MaximumCenterDisplacementMeters, double MaximumFootprintDisplacementBoundMeters);
internal sealed record CausalPublication(double CommittedFrontier, IReadOnlyList<CausalRiderState> Riders,
    CausalHistory? History, CausalContact? Contact, int Generation, int AcceptedSteps, double AcceptedDistanceMeters)
{
    internal PhysicalPairGeneration? ContactOwnership { get; init; }
}
internal sealed record CausalNextContact(double TimeSeconds, int RiderA, int RiderB, ContestedSpaceReport Certificate);
internal sealed record CausalGeometryBarrier(double FromSeconds, double ThroughSeconds,
    IReadOnlyList<PhysicalPoseInterval> UnacceptedPoses, ContestedSpaceReport Certificate);
internal sealed record CausalRunResult(CausalRunStatus Status, CausalPublication Publication, string? Refusal)
{
    internal CausalNextContact? NextContact { get; init; }
    internal CausalGeometryBarrier? GeometryBarrier { get; init; }
}
internal sealed class CausalCoordinatorWork
{
    internal int Windows, PairTimeTrials, RiderTimeTrials, PairCertificates, AllRiderCertificates, NarrowEvaluations, GeometrySubdivisions;
    internal int InvalidatedForecasts, Decisions, SegmentHandoffs, ReadyGroupPlans;
    internal int WorldCopies => 0;
    internal int ObservedNativeStepStarts, ReexecutedAcceptedSteps;
    private readonly Dictionary<SimulationSnapshot, double> _accepted = new(ReferenceEqualityComparer.Instance);
    internal void Accepted(RiderIntegrationCursor cursor) => _accepted[cursor.Snapshot] = cursor.Boundary.CanonicalProgress;
    internal void Starting(SimulationSnapshot snapshot, double progress)
    {
        ObservedNativeStepStarts++;
        if (_accepted.TryGetValue(snapshot, out var accepted) && progress < accepted) ReexecutedAcceptedSteps++;
    }
}

/// <summary>
/// Opt-in causal four-rider execution from the actual heat origin. Forecasts are
/// disposable one-step forks; publication is one immutable four-state reference.
/// Neither this coordinator nor FrozenHeatContext publishes ordinary live A/B/C.
/// </summary>
internal sealed class FourRiderCausalCoordinator
{
    private sealed class Slot(CausalRiderState state)
    {
        internal CausalRiderState State = state;
        internal RiderIntegrationCursor? Cursor;
        internal Forecast? Pending;
    }
    private sealed record Forecast(RiderIntegrationCursor Cursor, IReadOnlyList<PhysicalPoseInterval> Poses);
    private readonly SimulationEngine _engine;
    private readonly FrozenHeatContext _heat;
    private readonly SimulationSnapshot _origin;
    private readonly TrackMetricEmbedding _embedding;
    private readonly Slot[] _slots;
    private readonly bool _reverse;
    private readonly Func<CausalPublication, bool>? _admit;
    private CausalRunResult? _terminal;
    private bool _continuing;
    private IReadOnlyList<PhysicalPoseInterval> _eventTails = Array.Empty<PhysicalPoseInterval>();
    private readonly CausalPublication _initialPublication;
    internal CausalPublication Published => _heat.CausalPublication ?? _initialPublication;
    internal CausalCoordinatorWork Work { get; } = new();
    private const double EventToleranceSeconds = TwoRiderContactExecution.MaximumEventTimeToleranceSeconds;

    internal FourRiderCausalCoordinator(SimulationEngine engine, FrozenHeatContext heat, SimulationSnapshot origin,
        bool reverseProcessing = false, Func<CausalPublication, bool>? admit = null)
    {
        if (origin.Riders.Count != 4 || origin.Riders.Select(r => r.RiderId).Distinct().Count() != 4
            || origin.Riders.Any(r => r.ElapsedTimeSeconds != 0 || !r.IsActive)
            || origin.Step.UseLegacyPhysics || !origin.Step.FreezeSurfaceForHeat
            || !ReferenceEquals(origin.TrackState, heat.Surface) || !ReferenceEquals(origin.Track, heat.Track)
            || origin.Step.HeatId != heat.HeatId || origin.Step.Seed != heat.Options.Seed
            || origin.Step.RequiredLaps != heat.Options.Laps || heat.PassageCount != 0
            || !heat.Options.EnablePhysicalContactConsequences || !heat.Options.EnableContestedSpaceResponses)
            throw new ArgumentException("Causal A11 requires four distinct actual heat-start riders on the one frozen surface, with physical consequences enabled.");
        _engine = engine; _heat = heat; _origin = origin; _embedding = new(origin.Track, true);
        _reverse = reverseProcessing; _admit = admit;
        _slots = origin.Riders.Select(r => new Slot(new(r, 0, null, null, null, null))).ToArray();
        _initialPublication = new(0, Array.AsReadOnly(_slots.Select(s => s.State).ToArray()), null, null, 0, 0, 0);
        heat.BeginCausalOwnership();
    }

    // The first event remains an explicit pause. A caller may then request a
    // bounded continuation; the next new contact stops before any second effects.
    internal CausalRunResult ContinueAfterFirstContact(double throughSeconds, int maximumWindows = 10000)
    {
        if (maximumWindows <= 0) throw new ArgumentOutOfRangeException(nameof(maximumWindows));
        if (!double.IsFinite(throughSeconds) || throughSeconds <= Published.CommittedFrontier)
            throw new ArgumentOutOfRangeException(nameof(throughSeconds));
        if (!_continuing)
        {
            if (_terminal?.Status != CausalRunStatus.ContactResolved)
                throw new RiderIntegrationRefusalException("Continuation requires an accepted nonterminal first contact.");
            foreach (var slot in Processing())
            {
                var effect = Published.Contact!.Plan.Riders.SingleOrDefault(r => r.RiderId == slot.State.Rider.RiderId);
                slot.Cursor = (effect is null ? slot.Cursor!.ResumeEventWithoutConsequence(0)
                    : slot.Cursor!.ResumeAfterContact(effect, 0, EventToleranceSeconds)).Cursor!;
            }
            _continuing = true; _terminal = null;
        }
        return Advance(maximumWindows, throughSeconds);
    }

    internal CausalRunResult Advance(int maximumWindows = 10000, double stopAtSeconds = double.PositiveInfinity)
    {
        if (maximumWindows <= 0) throw new ArgumentOutOfRangeException(nameof(maximumWindows));
        if (_terminal is not null) return _terminal;
        var priorObserver = ProjectionCaptureAudit.CausalStepStarting;
        ProjectionCaptureAudit.CausalStepStarting = (snapshot, start, drive, lateral, recovery) =>
        { priorObserver?.Invoke(snapshot, start, drive, lateral, recovery); Work.Starting(snapshot, start.CanonicalProgress); };
        try
        {
            for (var window = 0; window < maximumWindows; window++)
            {
                Prepare();
                var through = _slots.Min(s => s.Pending!.Cursor.ExecutedThroughSeconds);
                if (through <= Published.CommittedFrontier)
                    throw new RiderIntegrationRefusalException("Numerical horizon makes no forward progress.");
                var from = Published.History?.ThroughSeconds ?? Published.CommittedFrontier;
                var poses = Window(_eventTails.Concat(_slots.SelectMany(s => s.Pending!.Poses)), from, through);
                var report = Observe(poses, allRiders: true, penetrationProof: _continuing);
                var eventReport = EventCandidates(report);
                var ownership = Published.ContactOwnership;
                if (_continuing && ownership is not null && Published.Contact is { } contact)
                {
                    // Only actual certified coverage can release the consumed
                    // generation. Impulse suppression never bypasses penetration.
                    ownership = new PhysicalPairObservation(report, _heat.Options.ContestedSpaceParameters)
                        .Advance((contact.RiderA, contact.RiderB), ownership);
                }
                var touch = SelectFirstEvent(eventReport);
                Work.Windows++;
                if (touch is not null) return ResolveFirstContact(touch, eventReport, through);
                if (_continuing && report.Intervals.Any(r => r.MinimumSeparationLowerBoundMeters < -GeometryNumerics.ContactDistanceMeters))
                    return Refuse(FormattableString.Invariant($"Post-contact executed window [{from:R},{through:R}] cannot certify nonpenetration: ")
                        + string.Join("; ", report.Intervals.Where(r => r.MinimumSeparationLowerBoundMeters < -GeometryNumerics.ContactDistanceMeters)
                            .Select(r => FormattableString.Invariant($"R{r.RiderA}-R{r.RiderB} sampled={r.MinimumSeparationMeters:R}, lower={r.MinimumSeparationLowerBoundMeters:R}"))),
                        new(from, through, poses, report));
                if (_continuing) RequireResolved(report);
                var closing = _slots.Where(s => s.Pending!.Cursor.ExecutedThroughSeconds == through).ToArray();
                var states = _slots.Select(s => closing.Contains(s) ? State(s.Pending!.Cursor) : s.State).ToArray();
                var history = new CausalHistory(Published.History, from, through, poses, report);
                Publish(states, history, null, closing.Select(s => s.Pending!.Cursor).ToArray(), ownership);
                foreach (var s in closing)
                {
                    s.Cursor?.Seal(); s.Cursor = s.Pending!.Cursor; s.Pending = null;
                    s.State = states.Single(r => r.Rider.RiderId == s.State.Rider.RiderId);
                }
                if (through >= _slots.Max(s => s.Cursor!.ExecutedThroughSeconds)) _eventTails = Array.Empty<PhysicalPoseInterval>();
                if (Published.CommittedFrontier >= stopAtSeconds) return new(CausalRunStatus.HorizonReached, Published, null);
            }
            return new(CausalRunStatus.HorizonReached, Published, null);
        }
        catch (RiderIntegrationRefusalException refusal) { return Refuse(refusal.Message); }
        catch (ExecutedPathTraversal.TimeSolveFeasibilityException refusal) { return Refuse(refusal.Message); }
        finally { ProjectionCaptureAudit.CausalStepStarting = priorObserver; }
    }

    private IEnumerable<Slot> Processing() => _reverse ? _slots.Reverse() : _slots;

    private void Prepare()
    {
        // All ready decisions read the same detached four-rider values, never
        // another rider's forecast. Addresses belong to lap/segment, not windows.
        var values = _slots.Select(s => s.State.Rider).ToArray();
        foreach (var slot in Processing())
        {
            if (slot.Pending is not null) continue;
            if (slot.Cursor is { } old && old.Boundary.CanonicalProgress >= old.Boundary.EndCanonicalProgress)
            {
                var completed = old.Resume(_heat.Surface).Completed
                    ?? throw new RiderIntegrationRefusalException("Segment handoff did not complete the retained cursor.");
                var change = completed.Changes.Single();
                if (change.Status != RiderRaceStatus.Racing)
                    throw new RiderIntegrationRefusalException($"R{change.RiderId}: {change.Status} segment handoff needs A12 lifecycle handling.");
                var rider = old.Snapshot.Riders[0].Apply(change);
                var oldType = old.Snapshot.Segment.Type;
                var newType = _heat.Track.Segments[rider.SegmentIndex].Type;
                // A coordinate conversion, not lateral motion. Only the causal
                // handoff uses it; ordinary historical normalized remapping stays.
                var lateral = ReexpressLateralAtHandoff(rider.LateralPosition, oldType, newType, _heat.Track.Geometry);
                rider = rider with { LateralPosition = lateral, Lane = (int)MathF.Round(lateral) };
                slot.State = slot.State with { Rider = rider }; slot.Cursor = null;
                values = values.Select(r => r.RiderId == rider.RiderId ? rider : r).ToArray();
                Work.SegmentHandoffs++;
            }
        }
        // No callback sees incremental segment handoff values above. Every new
        // decision below sees this one complete set of accepted states.
        var requests = new Dictionary<int, (SimulationSnapshot Origin, RiderIntent Intent)>();
        foreach (var slot in Processing().Where(s => s.Cursor is null))
        {
            var rider = slot.State.Rider;
            var step = _origin.Step with { StepNumber = checked((int)Math.Floor(rider.CanonicalProgress)),
                LapIndex = rider.LapsCompleted, SegmentIndex = rider.SegmentIndex,
                ExecutionOriginTimeSeconds = slot.State.ExecutedThroughSeconds };
            var common = new SimulationSnapshot(step, _heat.Track, _heat.Surface, values);
            var intent = _engine.DecideCausalRider(common, rider.RiderId); Work.Decisions++;
            requests.Add(rider.RiderId, (new(step, _heat.Track, _heat.Surface, new[] { rider }), intent));
        }
        // Existing #56B can plan jointly for riders starting a segment together.
        // Its speculative full motions never enter the accepted causal history.
        // An already executing rider keeps its captured controls until its next
        // decision; all cross-segment interactions remain subject to all-six #55.
        foreach (var group in requests.Values.GroupBy(r => (r.Origin.Step.LapIndex, r.Origin.Step.SegmentIndex)).ToArray())
        {
            if (group.Count() < 2) continue;
            var members = group.OrderBy(r => r.Intent.RiderId).ToArray();
            var common = new SimulationSnapshot(members[0].Origin.Step, _heat.Track, _heat.Surface,
                members.Select(r => r.Origin.Riders[0]));
            var forecast = _engine.Resolve(common, members.Select(r => r.Intent).ToArray(), _heat.Options, new InteractionEpisodeTracker());
            Work.ReadyGroupPlans++;
            foreach (var member in members)
            {
                var response = forecast.Interaction!.Episodes.SelectMany(e => e.SelectedResponses)
                    .LastOrDefault(r => r.RiderId == member.Intent.RiderId);
                if (response is null) continue;
                var decision = member.Intent.Decision with { TargetLane = response.Intent.TargetFor(common.Segment.Type),
                    Trajectory = response.Intent, DriveControl = response.DriveControl,
                    HoldLateralPosition = response.HoldLateralPosition, InteractionTarget = response.PhysicalTarget };
                requests[member.Intent.RiderId] = (member.Origin, member.Intent with { Decision = decision });
            }
        }
        foreach (var slot in Processing())
        {
            if (slot.Cursor is null)
            {
                var request = requests[slot.State.Rider.RiderId];
                slot.Cursor = _engine.BeginCausalRiderIntegration(request.Origin, request.Intent, _heat.Options).Cursor
                    ?? throw new RiderIntegrationRefusalException($"R{slot.State.Rider.RiderId}: no actual initial execution cursor.");
            }
            if (slot.Pending is not null) continue;
            var root = slot.Cursor;
            var next = root.ForkFrozenExperiment().Resume(_heat.Surface, 1).Cursor
                ?? throw new RiderIntegrationRefusalException($"R{slot.State.Rider.RiderId}: forecast lacks exact final-step cursor.");
            for (var roundoff = 0; next.ExecutedThroughSeconds == root.ExecutedThroughSeconds && roundoff < 4; roundoff++)
                next = next.Resume(_heat.Surface, 1).Cursor
                    ?? throw new RiderIntegrationRefusalException("Zero-clock native step cannot reach a retained forward endpoint.");
            var poses = Poses(root, next.Boundary).ToList();
            if (root.Boundary.CompletedSteps == 0 && root.Boundary.TimeSeconds > 0)
            {
                // Native launch reaction: exact stationary state, no invented
                // path, distance, wear or lateral motion during this delay.
                var zero = root.Boundary with { TimeSeconds = 0 };
                var end = root.Boundary.ExecutionEndpoints![0];
                poses.InsertRange(0, ResolvedBikePoses.FromExecutedStep(root.Snapshot, zero, end, _embedding, true));
            }
            slot.Pending = new(next, poses.AsReadOnly());
            if (root.Boundary.CompletedSteps == 0 && Published.History is { } previous
                && root.OriginTimeSeconds == Published.CommittedFrontier)
            {
                var oldPose = previous.Poses.Last(p => p.RiderId == slot.State.Rider.RiderId).Sample(previous.ThroughSeconds);
                var newPose = poses[0].Sample(poses[0].StartTimeSeconds);
                var centerDisplacement = (newPose.Position - oldPose.Position).Length;
                var displacement = centerDisplacement + newPose.Dimensions.BoundingRadiusMeters
                    * Math.Abs(BikeAngles.Wrap(newPose.Attitude.BikeHeadingRadians - oldPose.Attitude.BikeHeadingRadians));
                if (centerDisplacement > GeometryNumerics.ContactDistanceMeters)
                    throw new RiderIntegrationRefusalException($"R{slot.State.Rider.RiderId}: segment handoff moves the physical center without an executed transition ({centerDisplacement:R} m).");
                if (displacement > GeometryNumerics.ContactDistanceMeters)
                    slot.Pending = new(next, poses.Select((p, i) => i == 0
                        ? (PhysicalPoseInterval)new Clip(p, p.StartTimeSeconds, p.EndTimeSeconds, true) : p).ToArray());
            }
        }
    }

    private CausalRunResult ResolveFirstContact(ContestedSpaceEvent candidate, ContestedSpaceReport horizon, double through)
    {
        var first = candidate.FirstTouchCommonTimeSeconds!.Value;
        if (through - first < EventToleranceSeconds)
            throw new RiderIntegrationRefusalException("Insufficient forecast coverage to exclude a simultaneous contact group within 10 microseconds; an extended causal horizon is required.");
        var pair = _slots.Where(s => s.State.Rider.RiderId == candidate.RiderA || s.State.Rider.RiderId == candidate.RiderB).ToArray();
        if (pair.Any(s => s.Cursor!.ExecutedThroughSeconds >= first))
            throw new RiderIntegrationRefusalException(FormattableString.Invariant($"First candidate R{candidate.RiderA}-R{candidate.RiderB} at {first:R} seconds starts at/before a retained actual rider prefix; minimum={candidate.MinimumSeparationMeters:R}; sources={candidate.SourceA}/{candidate.SourceB}."));
        var lo = Published.CommittedFrontier; var hi = through;
        (ExecutedTraversalBoundary[] Bounds, ContestedSpaceReport Geometry) Trial(double requested)
        {
            Work.PairTimeTrials++; Work.RiderTimeTrials += 2;
            var b = pair.Select(s => s.Cursor!.PreviewToTime(requested)).ToArray();
            var p = pair.SelectMany((s, i) => Poses(s.Cursor!, b[i]));
            var end = pair.Select((s, i) => s.Cursor!.OriginTimeSeconds + b[i].TimeSeconds).Min();
            return (b, Observe(Window(p, Published.CommittedFrontier, end, pair.Select(s => s.State.Rider.RiderId).ToArray()), false, false));
        }
        // The one-step forecasts bracket the event; no full segment or accepted
        // prefix is rerun to construct a root-search upper bound.
        var upper = (Bounds: pair.Select(s => s.Pending!.Cursor.Boundary).ToArray(), Geometry: horizon);
        for (var i = 0; i < ExecutedPathTraversal.BisectionIterations; i++)
        {
            var witness = upper.Geometry.Intervals.Where(r => r.HasConflict).Min(r => r.FirstTouchCommonTimeSeconds)!.Value;
            var times = pair.Select((s, j) => s.Cursor!.OriginTimeSeconds + upper.Bounds[j].TimeSeconds).ToArray();
            if (times.All(t => t >= witness && t - witness <= EventToleranceSeconds)
                && upper.Geometry.Intervals.All(r => r.NumericallyResolved && r.MinimumSeparationMeters >= -GeometryNumerics.ContactDistanceMeters)) break;
            if (hi - lo <= ExecutedPathTraversal.TimeToleranceSeconds / 64)
                throw new RiderIntegrationRefusalException("No geometry-certified actual event state within 10 microseconds.");
            var mid = (lo + hi) * .5; var trial = Trial(mid);
            if (trial.Geometry.Intervals.Any(r => r.HasConflict)) { hi = mid; upper = trial; }
            else
            {
                RequireResolved(trial.Geometry); lo = mid;
            }
        }
        RequireResolved(upper.Geometry);
        var touch = upper.Geometry.Intervals.Where(r => r.HasConflict).Min(r => r.FirstTouchCommonTimeSeconds)!.Value;
        // All four native executions reach the selected time request. The pair's
        // certificate alone cannot authorize the other two actual endpoints.
        var bounds = _slots.Select(s =>
        {
            var index = Array.IndexOf(pair, s);
            if (index >= 0) return upper.Bounds[index];
            Work.RiderTimeTrials++;
            return s.Cursor!.PreviewToTime(hi);
        }).ToArray();
        var ends = _slots.Select((s, i) => s.Cursor!.OriginTimeSeconds + bounds[i].TimeSeconds).ToArray();
        if (ends.Any(t => t < touch || t - touch > EventToleranceSeconds)
            || ends.Max() - ends.Min() > GeometryNumerics.TimeToleranceSeconds)
            throw new RiderIntegrationRefusalException("Four actual clocks cannot agree at the event within the local time bound and #55 clock-skew bound.");
        var actual = _slots.SelectMany((s, i) => Poses(s.Cursor!, bounds[i])).ToArray();
        var eventWindow = Window(actual, Published.CommittedFrontier, ends.Min());
        var report = Observe(eventWindow, true, true);
        RequireResolved(report);
        if (report.Intervals.Any(r => r.MinimumSeparationLowerBoundMeters < -GeometryNumerics.ContactDistanceMeters))
            throw new RiderIntegrationRefusalException("Executed four-rider event window has inadmissible penetration.");
        var actualFirst = EventCandidates(report).Intervals.Where(r => r.HasConflict).OrderBy(r => r.FirstTouchCommonTimeSeconds).FirstOrDefault()
            ?? throw new RiderIntegrationRefusalException("Executed four-rider states do not certify any contact.");
        if ((actualFirst.RiderA, actualFirst.RiderB) != (candidate.RiderA, candidate.RiderB)
            || Math.Abs(actualFirst.FirstTouchCommonTimeSeconds!.Value - touch) > GeometryNumerics.TimeToleranceSeconds)
            throw new RiderIntegrationRefusalException("Actual all-six certificate changes the first event; no stale pair may be accepted.");
        RequireSingleEvent(EventCandidates(report), touch);
        RequireSingleEvent(horizon, touch);
        if (through < touch + EventToleranceSeconds)
            throw new RiderIntegrationRefusalException("Final event lacks the full simultaneous-contact lookahead window.");
        if (_continuing)
        {
            // Four freshly solved paths certify the next common-time event.
            // Their slightly later, differently clocked endpoints are not being
            // accepted: preserve the preceding publication and apply no effects.
            foreach (var slot in _slots) { slot.Cursor!.Seal(); slot.Pending!.Cursor.Seal(); }
            return _terminal = new(CausalRunStatus.NextContactPending, Published, null)
            { NextContact = new(touch, actualFirst.RiderA, actualFirst.RiderB, report) };
        }
        var endpointPoses = _slots.Select((s, i) => actual.Last(p => p.RiderId == s.State.Rider.RiderId).Sample(ends[i])).ToArray();
        for (var a = 0; a < 4; a++) for (var b = a + 1; b < 4; b++)
            if (MechanicalSeparation.Between(endpointPoses[a], endpointPoses[b]).SignedMeters < -GeometryNumerics.ContactDistanceMeters)
                throw new RiderIntegrationRefusalException("Actual endpoint footprints penetrate outside #55 tolerance.");
        var displacementBounds = endpointPoses.Select(p =>
        {
            var span = eventWindow.Last(i => i.RiderId == p.RiderId && i.StartTimeSeconds <= touch && i.EndTimeSeconds >= touch);
            var atTouch = span.Sample(touch); var center = (p.Position - atTouch.Position).Length;
            return (Center: center, Footprint: center + p.Dimensions.BoundingRadiusMeters
                * Math.Abs(BikeAngles.Wrap(p.Attitude.BikeHeadingRadians - atTouch.Attitude.BikeHeadingRadians)));
        }).ToArray();
        var accepted = _slots.Select((s, i) => s.Cursor!.AcceptTimeTrial(_heat.Surface, bounds[i])).ToArray();
        var before = accepted.Select(State).ToArray();
        var snapshot = new SimulationSnapshot(_origin.Step, _heat.Track, _heat.Surface, before.Select(s => s.Rider));
        var tracker = new InteractionEpisodeTracker(); tracker.Bind(snapshot, true);
        var episode = tracker.Engage(new[] { actualFirst.RiderA, actualFirst.RiderB }, touch, InteractionContext.MechanicalConflict);
        PhysicalBikePose Pose(int id) => eventWindow.Last(p => p.RiderId == id && p.StartTimeSeconds <= touch && p.EndTimeSeconds >= touch).Sample(touch);
        PhysicalContactRiderInput Input(int id)
        {
            var s = before.Single(s => s.Rider.RiderId == id);
            return new(id, s.Rider.Profile.Gameplay, s.Rider.Condition,
                _heat.Surface.SampleSurface(s.Origin!.Step.SegmentIndex, s.Rider.LateralPosition).EffectiveGrip);
        }
        var pa = Pose(actualFirst.RiderA); var pb = Pose(actualFirst.RiderB);
        var directions = new[] { pa, pb }.ToDictionary(p => p.RiderId,
            p => ContactFrameArithmetic.Direction(p.Attitude.TravelHeadingRadians, true));
        var input = new PhysicalContactInput(actualFirst, pa, pb,
            directions[pa.RiderId] * before.Single(s => s.Rider.RiderId == pa.RiderId).Rider.Speed,
            directions[pb.RiderId] * before.Single(s => s.Rider.RiderId == pb.RiderId).Rider.Speed,
            Input(pa.RiderId), Input(pb.RiderId), episode.Id);
        var observation = new PhysicalPairObservation(report, _heat.Options.ContestedSpaceParameters);
        var analysis = PhysicalContactAnalyzer.AnalyzeForConsequences(new[] { input }, _heat.Options.PhysicalContactParameters,
            p => tracker.CanApplyPhysical(p, observation));
        var pairs = analysis.AuditPairs.Where(p => tracker.CanApplyPhysical(p, observation)).ToArray();
        var changes = accepted.Select(c => TwoRiderContactExecution.State(c)
            with { ElapsedTimeSeconds = (float)c.ExecutedThroughSeconds }).ToArray();
        var plan = PhysicalContactConsequenceResolver.Build(analysis, analysis.ApplicationRiders, pairs, snapshot, changes,
            directions, _heat.Options.PhysicalContactParameters, _heat.Options.PhysicalContactConsequenceParameters);
        if (pairs.Length != 1 || plan.Riders.Count != 2)
            throw new RiderIntegrationRefusalException("Existing contact analysis cannot produce one complete pair consequence plan.");
        tracker.ConsumePhysical(plan, observation);
        var after = before.Select((s, i) => plan.Riders.SingleOrDefault(r => r.RiderId == s.Rider.RiderId) is { } effect
            ? s with { Rider = s.Rider.Apply(PhysicalContactConsequenceResolver.Apply(changes[i], effect)) } : s).ToArray();
        var contact = new CausalContact(touch, actualFirst.RiderA, actualFirst.RiderB, plan,
            tracker.PhysicalPairs[InteractionEpisodeTracker.PhysicalPairKey(actualFirst.RiderA, actualFirst.RiderB)], report, ends.Max() - touch,
            ends.Max() - ends.Min(), displacementBounds.Max(d => d.Center), displacementBounds.Max(d => d.Footprint));
        Publish(after, new(Published.History, Published.CommittedFrontier, ends.Min(), eventWindow, report), contact, accepted);
        foreach (var s in _slots) { s.Pending!.Cursor.Seal(); s.Pending = null; s.Cursor?.Seal(); }
        _eventTails = actual.Where(p => p.EndTimeSeconds > ends.Min()).Select(p =>
            (PhysicalPoseInterval)new Clip(p, Math.Max(p.StartTimeSeconds, ends.Min()), p.EndTimeSeconds)).ToArray();
        for (var i = 0; i < _slots.Length; i++) { _slots[i].Cursor = accepted[i]; _slots[i].State = after[i]; }
        if (plan.Riders.Any(r => r.Severity == PhysicalContactSeverity.Crash)) foreach (var c in accepted) c.Seal();
        Work.InvalidatedForecasts += 4;
        return _terminal = new(plan.Riders.Any(r => r.Severity == PhysicalContactSeverity.Crash)
            ? CausalRunStatus.InterruptedByCrash : CausalRunStatus.ContactResolved, Published, null);
    }

    private void Publish(CausalRiderState[] states, CausalHistory history, CausalContact? contact,
        RiderIntegrationCursor[] newlyAccepted, PhysicalPairGeneration? ownership = null)
    {
        var frontier = contact?.TimeSeconds ?? history.ThroughSeconds;
        var transaction = _heat.PrepareCausalPrefixes(newlyAccepted, frontier, contact is not null);
        // Counters are derived from actual immutable prefix extensions, including
        // the final shortened steps; forecasting contributes zero wear.
        var steps = Published.AcceptedSteps; var distance = Published.AcceptedDistanceMeters;
        foreach (var c in newlyAccepted)
        {
            var old = _slots.Single(s => s.State.Rider.RiderId == c.Snapshot.Riders[0].RiderId).State;
            var same = ReferenceEquals(old.Origin, c.Snapshot);
            steps += c.Boundary.CompletedSteps - (same ? old.Execution!.CompletedSteps : 0);
            distance += c.Boundary.DistanceMeters - (same ? old.Execution!.DistanceMeters : 0);
        }
        var next = new CausalPublication(frontier, Array.AsReadOnly(states), history, contact ?? Published.Contact, Published.Generation + 1, steps, distance)
        { ContactOwnership = ownership ?? contact?.Generation ?? Published.ContactOwnership };
        if (_admit is not null && !_admit(next))
            throw new RiderIntegrationRefusalException("Atomic four-rider admission veto: no states, event generation or wear published.");
        transaction(next); // One CAS publishes the four states, history, event and passage journal together.
        foreach (var cursor in newlyAccepted) Work.Accepted(cursor);
    }

    private CausalRiderState State(RiderIntegrationCursor c)
    {
        var change = TwoRiderContactExecution.State(c) with { ElapsedTimeSeconds = (float)c.ExecutedThroughSeconds,
            Status = RiderRaceStatus.Racing, ContactRecovery = c.ActiveContactRecovery };
        return new(c.Snapshot.Riders[0].Apply(change), c.ExecutedThroughSeconds, c.Snapshot, c.Intent, c.Boundary, c.Context);
    }

    private IReadOnlyList<PhysicalPoseInterval> Poses(RiderIntegrationCursor root, ExecutedTraversalBoundary boundary)
    {
        var poses = new List<PhysicalPoseInterval>();
        var endpoints = boundary.ExecutionEndpoints!;
        for (var i = root.Boundary.CompletedSteps + 1; i < endpoints.Count; i++)
        {
            var a = endpoints[i - 1]; var b = endpoints[i];
            if (b.TimeSeconds == a.TimeSeconds && b.Speed == a.Speed
                && LateralSpaceModel.LateralDistanceMeters(a.LateralPosition, b.LateralPosition,
                    root.Snapshot.Segment.Type, _heat.Track.Geometry)
                    <= Math.Max(1, a.Speed) * (Math.BitIncrement(a.TimeSeconds) - a.TimeSeconds)
                && b.CanonicalProgress - a.CanonicalProgress <= Math.BitIncrement(a.CanonicalProgress) - a.CanonicalProgress
                && b.DistanceMeters - a.DistanceMeters <= Math.BitIncrement(a.DistanceMeters) - a.DistanceMeters)
                continue; // Retain the native one-ULP accumulator step; it has no representable swept duration.
            var start = boundary with { CanonicalProgress = a.CanonicalProgress, TimeSeconds = a.TimeSeconds,
                DistanceMeters = a.DistanceMeters, Speed = a.Speed, LateralPosition = a.LateralPosition };
            poses.AddRange(ResolvedBikePoses.FromExecutedStep(root.Snapshot, start, b, _embedding, true));
        }
        return poses.AsReadOnly();
    }

    private ContestedSpaceReport Observe(IReadOnlyList<PhysicalPoseInterval> poses, bool allRiders, bool penetrationProof)
    {
        var report = penetrationProof ? ContestedSpaceResolver.ObserveExecutedContactWindow(poses)
            : ContestedSpaceResolver.ObserveFirstContact(poses);
        if (allRiders) Work.AllRiderCertificates++; else Work.PairCertificates++;
        Work.NarrowEvaluations += report.Work.NarrowPhaseEvaluations; Work.GeometrySubdivisions += report.Work.AdaptiveSubdivisions;
        return report;
    }

    private ContestedSpaceReport EventCandidates(ContestedSpaceReport report)
        => Published.ContactOwnership?.Consumed == true && Published.Contact is { } contact
            ? report with { Intervals = report.Intervals.Where(r =>
                (r.RiderA, r.RiderB) != (contact.RiderA, contact.RiderB)).ToArray() } : report;

    internal static void RequireSingleEvent(ContestedSpaceReport report, double first)
    {
        var simultaneous = report.Intervals.Where(r => r.HasConflict && r.FirstTouchCommonTimeSeconds <= first + EventToleranceSeconds)
            .Select(r => (r.RiderA, r.RiderB)).Distinct().Count();
        if (simultaneous > 1)
            throw new RiderIntegrationRefusalException("Simultaneous contact group within 10 microseconds requires a group event transaction; A11 refuses arbitrary pair ordering.");
    }
    internal static ContestedSpaceEvent? SelectFirstEvent(ContestedSpaceReport report)
    {
        RequireFrames(report);
        if (report.Work.RiderPairs != 6)
            throw new RiderIntegrationRefusalException("A four-rider frontier requires all six pair certificates.");
        var first = report.Intervals.Where(r => r.HasConflict).OrderBy(r => r.FirstTouchCommonTimeSeconds).FirstOrDefault();
        if (first is null) { RequireResolved(report); return null; }
        if (report.Intervals.Any(r => !r.NumericallyResolved && (!r.HasConflict
            || (r.RiderA, r.RiderB) != (first.RiderA, first.RiderB))))
            throw new RiderIntegrationRefusalException("Uncertain earlier interaction among six pairs blocks event ordering.");
        RequireSingleEvent(report, first.FirstTouchCommonTimeSeconds!.Value);
        return first;
    }
    internal static float ReexpressLateralAtHandoff(float lateral, SegmentType from, SegmentType to, TrackGeometry geometry)
    {
        var offset = LaneModel.PhysicalLateralOffsetFromInnerReferenceMeters(lateral, from, geometry);
        if (offset > LaneModel.UsableRacingWidthMeters(to, geometry))
            throw new RiderIntegrationRefusalException($"Physical offset {offset:R} m is outside the next segment; a width-transition path is required, never a clamp or teleport.");
        return LaneModel.LateralPositionFromPhysicalOffsetMeters(offset, to, geometry);
    }
    private static void RequireFrames(ContestedSpaceReport r)
    {
        if (r.FrameCoverageGaps.Count != 0 || r.Intervals.Any(i => i.Kind.HasFlag(SpaceConflictKind.BoundaryAmbiguous)))
            throw new RiderIntegrationRefusalException("Six-pair coverage has unrelated frames or an ambiguous boundary transition.");
    }
    private static void RequireResolved(ContestedSpaceReport r)
    {
        RequireFrames(r);
        if (r.Intervals.Any(i => !i.NumericallyResolved))
            throw new RiderIntegrationRefusalException("#55 cannot certify an earlier contact/clear interval within its unchanged numerical limits.");
    }

    private IReadOnlyList<PhysicalPoseInterval> Window(IEnumerable<PhysicalPoseInterval> source, double from, double through,
        int[]? ids = null)
    {
        var poses = source.Where(p => p.EndTimeSeconds > from && p.StartTimeSeconds < through)
            .Select(p => (PhysicalPoseInterval)new Clip(p, Math.Max(from, p.StartTimeSeconds), Math.Min(through, p.EndTimeSeconds))).ToArray();
        foreach (var id in ids ?? _slots.Select(s => s.State.Rider.RiderId).ToArray())
        {
            var covered = from;
            foreach (var p in poses.Where(p => p.RiderId == id).OrderBy(p => p.StartTimeSeconds))
            {
                if (p.StartTimeSeconds != covered)
                    throw new RiderIntegrationRefusalException($"R{id}: actual executed coverage gap/overlap at {covered:R} seconds.");
                covered = p.EndTimeSeconds;
            }
            if (covered != through) throw new RiderIntegrationRefusalException($"R{id}: actual coverage ends at {covered:R}, needs {through:R}.");
        }
        return Array.AsReadOnly(poses);
    }
    private CausalRunResult Refuse(string reason, CausalGeometryBarrier? barrier = null)
    {
        foreach (var s in _slots) { s.Cursor?.Seal(); s.Pending?.Cursor.Seal(); }
        return _terminal = new(CausalRunStatus.Refused, Published, reason) { GeometryBarrier = barrier };
    }
    private sealed class Clip(PhysicalPoseInterval source, double from, double through, bool discontinuity = false) : PhysicalPoseInterval(source.RiderId,
        source.FrameId, from, through, source.Dimensions, discontinuity || source.StartsAtDiscontinuity && from == source.StartTimeSeconds,
        source.Source, source.SupportsLapWrap)
    {
        internal override bool DeterministicArithmetic => source.DeterministicArithmetic;
        public override PhysicalBikePose Sample(double time) { Fraction(time); return source.Sample(time); }
        public override PoseRateBounds RateBounds(double start, double end) { Fraction(start); Fraction(end); return source.RateBounds(start, end); }
    }
}
