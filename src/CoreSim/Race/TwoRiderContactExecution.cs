using CoreSim.Decisions;
using CoreSim.Interactions;
using CoreSim.PhysicalSpace;

namespace CoreSim.Race;

internal sealed record TwoRiderContactResult(double ContactTimeSeconds, IReadOnlyList<RiderIntegrationCursor> Before,
    PhysicalContactConsequencePlan Plan, IReadOnlyList<RiderIntegrationCursor> After,
    IReadOnlyList<RiderStateChange> AfterEvent, ContestedSpaceReport ContactGeometry,
    ContestedSpaceReport ContinuationGeometry, bool ContinuationCertified, PhysicalPairGeneration Ownership,
    double CertifiedThroughSeconds, IReadOnlyList<PhysicalPoseInterval> CertifiedHistory,
    double RequestedEventTimeSeconds, int PairTimeTrials);

internal sealed class PairContactCertificationRefusal(string detail, double requestedTime,
    IReadOnlyList<ExecutedTraversalBoundary> candidates, ContestedSpaceReport report, int trials)
    : RiderIntegrationRefusalException(detail)
{
    internal double RequestedTimeSeconds { get; } = requestedTime;
    internal IReadOnlyList<ExecutedTraversalBoundary> Candidates { get; } = candidates;
    internal ContestedSpaceReport Report { get; } = report;
    internal int PairTimeTrials { get; } = trials;
}

/// <summary>Bounded two-rider experiment. No live publication or normal heat-loop caller.</summary>
internal static class TwoRiderContactExecution
{
    internal static TwoRiderContactResult Execute(SimulationEngine engine, SimulationSnapshot snapshot,
        IReadOnlyList<RiderIntent> intents, HeatSimulationOptions options, int continuationSteps = 6)
    {
        if (snapshot.Riders.Count != 2)
            throw new RiderIntegrationRefusalException("The contact demonstration requires exactly two riders and multiple continuation steps.");
        return Execute(engine, snapshot.Riders.Select(r => new SimulationSnapshot(snapshot.Step with { SegmentIndex = r.SegmentIndex },
            snapshot.Track, snapshot.TrackState, new[] { r })).ToArray(), intents, options, continuationSteps);
    }

    // Preserve each captured decision address and surface identity before any forecasts or trials.
    internal static TwoRiderContactResult Execute(SimulationEngine engine, IReadOnlyList<SimulationSnapshot> capturedOrigins,
        IReadOnlyList<RiderIntent> intents, HeatSimulationOptions options, int continuationSteps = 6)
        => ExecuteCore(engine, capturedOrigins, intents, options, continuationSteps, null, null);

    internal static TwoRiderContactResult Execute(SimulationEngine engine, IReadOnlyList<RiderIntegrationCursor> retained,
        HeatSimulationOptions options, int continuationSteps,
        Func<IReadOnlyList<PhysicalPoseInterval>, double, bool, bool>? certifyOtherRiders = null)
        => ExecuteCore(engine, retained.Select(c => c.Snapshot).ToArray(), retained.Select(c => c.Intent).ToArray(),
            options, continuationSteps, retained, certifyOtherRiders);

    private static TwoRiderContactResult ExecuteCore(SimulationEngine engine, IReadOnlyList<SimulationSnapshot> capturedOrigins,
        IReadOnlyList<RiderIntent> intents, HeatSimulationOptions options, int continuationSteps,
        IReadOnlyList<RiderIntegrationCursor>? retained,
        Func<IReadOnlyList<PhysicalPoseInterval>, double, bool, bool>? certifyOtherRiders)
    {
        options.Validate();
        if (capturedOrigins.Count != 2 || capturedOrigins.Any(s => s.Riders.Count != 1) || intents.Count != 2
            || continuationSteps < 1 || !options.EnablePhysicalContactConsequences || !options.EnableContestedSpaceResponses)
            throw new RiderIntegrationRefusalException("The contact demonstration requires two separate rider origins and multiple continuation steps.");
        var origins = capturedOrigins.OrderBy(s => s.Riders[0].RiderId).ToArray();
        var riders = origins.Select(s => s.Riders[0]).ToArray();
        if (riders.Select(r => r.RiderId).Distinct().Count() != 2
            || !intents.Select(i => i.RiderId).Order().SequenceEqual(riders.Select(r => r.RiderId))
            || origins.Any(s => !ReferenceEquals(s.Track, origins[0].Track) || s.Step.UseLegacyPhysics
                || s.Step.HeatId != origins[0].Step.HeatId || s.Step.Seed != options.Seed || s.Step.RequiredLaps != options.Laps
                || s.Step.SegmentIndex != s.Riders[0].SegmentIndex || s.Step.LapIndex != s.Riders[0].LapsCompleted))
            throw new RiderIntegrationRefusalException("Captured pair origins must preserve their track, heat, seed, lap and segment contexts.");
        if (!ReferenceEquals(origins[0].TrackState, origins[1].TrackState))
            throw new RiderIntegrationRefusalException($"Captured steps {origins[0].Step.StepNumber}/{origins[1].Step.StepNumber} have different surface versions; causal surface synchronization is required before pair execution.");
        if (retained is null && riders.Select(r => r.ElapsedTimeSeconds).Distinct().Count() != 1)
            throw new RiderIntegrationRefusalException("Different start clocks require retained executed prefixes and a cursor before the later rider's first step; missing time coverage cannot be interpolated.");
        var snapshot = new SimulationSnapshot(origins[0].Step, origins[0].Track, origins[0].TrackState, riders);
        var embedding = new TrackMetricEmbedding(snapshot.Track, deterministicArithmetic: true);
        var selected = riders.Select(r => intents.Single(i => i.RiderId == r.RiderId)).ToArray();
        var forecasts = origins.Select((s, i) => engine.ResolveProduction(s, new[] { selected[i] }, options, legacyContacts: false)).ToArray();
        var forecastPoses = forecasts.SelectMany(f => f.Motions.SelectMany(m => ResolvedBikePoses.FromMotion(m, snapshot.Track, embedding: embedding))).ToArray();
        var forecastReport = ContestedSpaceResolver.Observe(forecastPoses);
        var oldTouch = forecastReport.Intervals.Where(r => r.HasConflict).Min(r => r.FirstTouchCommonTimeSeconds)
            ?? throw new RiderIntegrationRefusalException("No forecast contact to bracket.");
        var cursors = retained?.OrderBy(c => c.Snapshot.Riders[0].RiderId).ToArray()
            ?? origins.Select((s, i) => engine.BeginRiderIntegration(s, selected[i], options, 1).Cursor
                ?? throw new RiderIntegrationRefusalException("Cursor completed before the contact bracket.")).ToArray();
        var lo = cursors.Max(c => c.Snapshot.Riders[0].ElapsedTimeSeconds + c.Boundary.TimeSeconds);
        var hi = forecasts.Select(f => f.Motions.Single()).Min(m => m.StartElapsedTimeSeconds
            + m.Nodes.Where(n => m.StartElapsedTimeSeconds + n.LocalTimeSeconds > oldTouch).Take(2).Average(n => (double)n.LocalTimeSeconds));
        var timeTrials = 0;
        (ExecutedTraversalBoundary[] Boundaries, ContestedSpaceReport Report) Trial(double time)
        {
            timeTrials++;
            var boundaries = cursors.Select(c => c.PreviewToTime(time)).ToArray();
            var poses = boundaries.SelectMany((b, i) => Poses(origins[i], b, embedding)).ToArray();
            return (boundaries, ContestedSpaceResolver.Observe(poses));
        }
        bool? Contact(ContestedSpaceReport report, bool final = false)
        {
            if (report.FrameCoverageGaps.Count != 0 || report.Intervals.Any(r => r.Kind.HasFlag(SpaceConflictKind.BoundaryAmbiguous))
                || report.Intervals.Any(r => !r.NumericallyResolved) && final)
                throw new RiderIntegrationRefusalException($"#55 cannot certify the common executed pair: final={final}; gaps={report.FrameCoverageGaps.Count}; failed={string.Join(';', report.Intervals.Where(r=>!r.NumericallyResolved || r.Kind.HasFlag(SpaceConflictKind.BoundaryAmbiguous)).Select(r=>$"{r.IntervalStartSeconds:R}..{r.IntervalEndSeconds:R},min={r.MinimumSeparationMeters:R},touch={r.FirstTouchCommonTimeSeconds:R},kind={r.Kind}"))}.");
            if (!report.Intervals.Any(r => r.HasConflict) && report.Intervals.Any(r => !r.NumericallyResolved)) return null;
            return report.Intervals.Any(r => r.HasConflict);
        }
        if (hi <= lo) throw new RiderIntegrationRefusalException("Contact precedes the supported retained pair prefix.");
        var upper = Trial(hi);
        if (Contact(upper.Report) != true) throw new RiderIntegrationRefusalException("New executed trajectories do not bracket a contact.");
        // All lower trials must be geometrically resolved and clear. Neither forecast is published.
        var lower = Trial(Math.BitIncrement(lo));
        if (Contact(lower.Report) != false) throw new RiderIntegrationRefusalException("No certified clear retained pair prefix.");
        for (var iteration = 0; iteration < ExecutedPathTraversal.BisectionIterations
            && hi - lo > ExecutedPathTraversal.TimeToleranceSeconds * .25; iteration++)
        {
            var mid = (lo + hi) * .5; var trial = Trial(mid);
            var conflict = Contact(trial.Report);
            if (!conflict.HasValue) break; // Keep both certified bounds; an unknown trial is never declared clear.
            if (conflict.Value) { hi = mid; upper = trial; } else lo = mid;
        }
        try { Contact(upper.Report, final: true); }
        catch (RiderIntegrationRefusalException refusal)
        {
            foreach (var cursor in cursors) cursor.Seal();
            throw new PairContactCertificationRefusal(refusal.Message, hi, upper.Boundaries, upper.Report, timeTrials);
        }
        var contact = upper.Report.Intervals.Where(r => r.HasConflict).OrderBy(r => r.FirstTouchCommonTimeSeconds).First();
        var time = contact.FirstTouchCommonTimeSeconds!.Value;
        if (upper.Boundaries.Select((b, i) => origins[i].Riders[0].ElapsedTimeSeconds + b.TimeSeconds)
            .Any(t => t < time || t - time > ExecutedPathTraversal.TimeToleranceSeconds))
            throw new RiderIntegrationRefusalException(FormattableString.Invariant($"Actual event states do not share touch={time:R}; A={upper.Boundaries[0].TimeSeconds:R}; B={upper.Boundaries[1].TimeSeconds:R}; lo={lo:R}; hi={hi:R} within the solver bound."));
        var actualPoses = upper.Boundaries.SelectMany((b, i) => Poses(origins[i], b, embedding)).ToArray();
        if (certifyOtherRiders is not null && !certifyOtherRiders(actualPoses, time, false))
        {
            foreach (var cursor in cursors) cursor.Seal();
            throw new PairContactCertificationRefusal("Another rider's earlier contact, uncertain geometry or missing coverage blocks this pair.",
                hi, upper.Boundaries, upper.Report, timeTrials);
        }
        var before = cursors.Select((c, i) => c.AcceptTimeTrial(snapshot.TrackState, upper.Boundaries[i])).ToArray();
        var eventPoses = riders.Select(r => actualPoses.Where(p => p.RiderId == r.RiderId && p.StartTimeSeconds <= time && p.EndTimeSeconds >= time)
            .OrderByDescending(p => p.StartTimeSeconds).First().Sample(time)).ToArray();
        var tracker = new InteractionEpisodeTracker(); tracker.Bind(snapshot, deterministicArithmetic: true);
        var episode = tracker.Engage(riders.Select(r => r.RiderId).ToArray(), time, InteractionContext.MechanicalConflict);
        PhysicalContactRiderInput Input(int i) => new(riders[i].RiderId, riders[i].Profile.Gameplay, riders[i].Condition,
            snapshot.TrackState.SampleSurface(riders[i].SegmentIndex, before[i].Boundary.LateralPosition).EffectiveGrip);
        var directions = eventPoses.ToDictionary(p => p.RiderId,
            p => ContactFrameArithmetic.Direction(p.Attitude.TravelHeadingRadians, p.DeterministicArithmetic));
        var input = new PhysicalContactInput(contact, eventPoses[0], eventPoses[1], directions[riders[0].RiderId] * before[0].Boundary.Speed,
            directions[riders[1].RiderId] * before[1].Boundary.Speed, Input(0), Input(1), episode.Id);
        var observation = new PhysicalPairObservation(upper.Report, options.ContestedSpaceParameters);
        var analysis = PhysicalContactAnalyzer.AnalyzeForConsequences(new[] { input }, options.PhysicalContactParameters,
            pair => tracker.CanApplyPhysical(pair, observation));
        var pairs = analysis.AuditPairs.Where(p => tracker.CanApplyPhysical(p, observation)).ToArray();
        var states = before.Select(State).ToArray();
        var plan = PhysicalContactConsequenceResolver.Build(analysis, analysis.ApplicationRiders, pairs, snapshot,
            states, directions, options.PhysicalContactParameters, options.PhysicalContactConsequenceParameters);
        if (plan.AppliedPairs.Count != 1 || plan.Riders.Count != 2)
            throw new RiderIntegrationRefusalException("The verified pair did not produce one simultaneous consequence plan.");
        tracker.ConsumePhysical(plan, observation);
        var afterEvent = states.Select(s => PhysicalContactConsequenceResolver.Apply(s, plan.Riders.Single(p => p.RiderId == s.RiderId))).ToArray();
        var after = before.Select(c => c.ResumeAfterContact(plan.Riders.Single(r => r.RiderId == c.Snapshot.Riders[0].RiderId), continuationSteps).Cursor
            ?? throw new RiderIntegrationRefusalException("Demonstration needs multiple steps before a segment boundary.")).ToArray();
        var postPoses = after.SelectMany((c, i) => Poses(origins[i], c.Boundary, embedding, before[i].Boundary.CompletedSteps)).ToArray();
        var verified = ContestedSpaceResolver.Observe(postPoses);
        var safe = verified.Intervals.Count > 0 && verified.FrameCoverageGaps.Count == 0
            && verified.Intervals.All(r => r.NumericallyResolved && !r.Kind.HasFlag(SpaceConflictKind.BoundaryAmbiguous)
                && r.MinimumSeparationLowerBoundMeters >= -GeometryNumerics.MinimumSeparationToleranceMeters);
        safe &= verified.Intervals.All(r => r.MinimumSeparationMeters >= -GeometryNumerics.ContactDistanceMeters);
        if (certifyOtherRiders is not null)
            safe &= certifyOtherRiders(postPoses, after.Min(c => c.Snapshot.Riders[0].ElapsedTimeSeconds + c.Boundary.TimeSeconds), true);
        if (safe) tracker.ObservePhysicalClearance(verified, options.ContestedSpaceParameters);
        var through = safe ? after.Min(c => c.Snapshot.Riders[0].ElapsedTimeSeconds + c.Boundary.TimeSeconds) : time;
        var history = actualPoses.Concat(safe ? postPoses : Array.Empty<PhysicalPoseInterval>())
            .Where(p => p.StartTimeSeconds < through).Select(p => p.EndTimeSeconds <= through ? p : new ClippedPose(p, through)).ToArray();
        foreach (var c in after) c.Seal(); // A bounded demo ends here; an unsafe candidate cannot be resumed or published.
        // Suppressed impulse ownership alone never authorizes an unsafe continuation.
        return new(time, before, plan, after, afterEvent, upper.Report, verified, safe,
            tracker.PhysicalPairs[InteractionEpisodeTracker.PhysicalPairKey(riders[0].RiderId, riders[1].RiderId)], through, history, hi, timeTrials);
    }

    internal static RiderStateChange State(RiderIntegrationCursor cursor)
    {
        var rider = cursor.Snapshot.Riders[0]; var b = cursor.Boundary;
        return new(rider.RiderId, rider.Lane, cursor.Context.PlannedLane, cursor.Intent.Decision.TargetLane,
            cursor.Context.Resolution.Lane, b.LateralPosition, b.Speed, cursor.Context.Risk, rider.Status,
            (float)(rider.ElapsedTimeSeconds + b.TimeSeconds), rider.Position.AdvanceExecuted(b.CanonicalProgress - rider.SegmentProgress, (float)b.DistanceMeters),
            rider.LastResolvedSegmentId, rider.Morale, cursor.Context.Resolution.Outcome, cursor.Context.EntrySpeed, b.Speed, false);
    }

    internal static IReadOnlyList<PhysicalPoseInterval> Poses(SimulationSnapshot origin, ExecutedTraversalBoundary boundary,
        TrackMetricEmbedding embedding, int skipSteps = 0)
    {
        var poses = new List<PhysicalPoseInterval>();
        var endpoints = boundary.ExecutionEndpoints ?? throw new RiderIntegrationRefusalException("Pair geometry requires actual double execution endpoints.");
        for (var i = skipSteps + 1; i < endpoints.Count; i++)
        {
            var a = endpoints[i - 1]; var b = endpoints[i];
            var start = boundary with { CanonicalProgress = a.CanonicalProgress, TimeSeconds = a.TimeSeconds,
                DistanceMeters = a.DistanceMeters, Speed = a.Speed, LateralPosition = a.LateralPosition };
            poses.AddRange(ResolvedBikePoses.FromExecutedStep(origin, start, b, embedding, exactProgress: true));
        }
        return poses;
    }

    private sealed class ClippedPose(PhysicalPoseInterval source, double end) : PhysicalPoseInterval(source.RiderId,
        source.FrameId, source.StartTimeSeconds, end, source.Dimensions, source.StartsAtDiscontinuity, source.Source, source.SupportsLapWrap)
    {
        internal override bool DeterministicArithmetic => source.DeterministicArithmetic;
        public override PhysicalBikePose Sample(double time) { Fraction(time); return source.Sample(time); }
        public override PoseRateBounds RateBounds(double start, double finish) { Fraction(start); Fraction(finish); return source.RateBounds(start, finish); }
    }
}
