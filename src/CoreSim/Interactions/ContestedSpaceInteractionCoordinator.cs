using CoreSim.Decisions;
using CoreSim.PhysicalSpace;
using CoreSim.Race;

namespace CoreSim.Interactions;

/// <summary>
/// Bounded simultaneous traffic layer. #54 executes every alternative; #55 alone
/// determines mechanical overlap. This class changes requests, never rider state.
/// </summary>
internal sealed class ContestedSpaceInteractionCoordinator(InteractionEpisodeTracker tracker, bool reusePairResults = true)
{
    private sealed record Projection(InteractionAlternative Alternative, TrajectoryTraversal Traversal,
        IReadOnlyList<PhysicalPoseInterval> Poses, bool WithinTrack);
    internal sealed record Cluster(int[] Riders, InteractionGeometry[] Edges, InteractionContext Context, bool Meaningful = true);
    private readonly record struct ProjectionKey(int RiderId, TrajectoryIntent Intent, float Drive, bool Hold);
    private static ProjectionKey Key(InteractionAlternative a)
        => new(a.RiderId, a.Intent, a.DriveControl?.PositiveDriveFraction ?? 1f, a.HoldLateralPosition);

    public ResolvedSimulationStep Resolve(SimulationEngine engine, SimulationSnapshot snapshot,
        IReadOnlyList<RiderIntent> intents, HeatSimulationOptions options, InteractionEpisodeTracker? owner = null)
    {
        ArgumentNullException.ThrowIfNull(engine); ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(intents); options.Validate();
        if (snapshot.Riders.Count > 4) throw new ArgumentException("Contested space supports at most four riders per heat.");
        tracker.Bind(snapshot);
        var p = options.ContestedSpaceParameters;
        var independent = engine.ResolveProduction(snapshot, intents, options, legacyContacts: false);
        var currentPoses = Poses(independent, snapshot.Track);
        var direct = CommonTimePoseHistory.Observe(tracker.History.Concat(currentPoses));
        var now = snapshot.Riders.Where(r => r.IsActive).Select(r => (double)r.ElapsedTimeSeconds).DefaultIfEmpty(0).Min();
        var directRows = direct.Intervals.Where(r => r.IntervalEndSeconds > now).ToArray();
        tracker.ObserveClearance(now, directRows, p);
        // No planning or extra production replay for spatially unrelated riders.
        if (!directRows.Any(r => Eligible(r) && r.MinimumSeparationMeters <= p.CompetitiveReachMeters)
            && !tracker.Active.Any())
        {
            var quiet = engine.ResolveProduction(snapshot, intents, options);
            var retainedWork = tracker.Retain(quiet);
            return quiet.withInteraction(new(tracker.Closed.ToArray(),
                new(0, 2, direct.Work.NarrowPhaseEvaluations + retainedWork, 0, 0)), owner, tracker);
        }

        var original = intents.ToDictionary(i => i.RiderId, i => Original(i));
        var evaluators = snapshot.Riders.Where(r => r.IsActive).ToDictionary(r => r.RiderId,
            r => new TrajectoryEvaluator(new RiderDecisionContext(snapshot, r)));
        var cache = new Dictionary<(int, TrajectoryIntent, float, bool), Projection>();
        var production = 1; var narrow = direct.Work.NarrowPhaseEvaluations; var combinations = 0; var passes = 0;
        var pairReports = new Dictionary<(ProjectionKey A, ProjectionKey B), ContestedSpaceReport>();
        ContestedSpaceReport Verify(IEnumerable<Projection> projected)
        {
            var riders = projected.OrderBy(x => x.Alternative.RiderId).ToArray();
            var reports = new List<ContestedSpaceReport>();
            for (var a = 0; a < riders.Length; a++) for (var b = a + 1; b < riders.Length; b++)
            {
                var key = (Key(riders[a].Alternative), Key(riders[b].Alternative));
                if (!reusePairResults || !pairReports.TryGetValue(key, out var report))
                {
                    report = CommonTimePoseHistory.Observe(tracker.History.Where(i => i.RiderId == key.Item1.RiderId || i.RiderId == key.Item2.RiderId)
                        .Concat(riders[a].Poses).Concat(riders[b].Poses));
                    pairReports[key] = report; narrow += report.Work.NarrowPhaseEvaluations;
                }
                reports.Add(report);
            }
            return new(reports.SelectMany(r => r.Intervals).ToArray(), new(reports.Count,
                reports.Sum(r => r.Work.CandidateIntervals), reports.Sum(r => r.Work.BroadPhaseRejects),
                reports.Sum(r => r.Work.NarrowPhaseEvaluations), reports.Sum(r => r.Work.AdaptiveSubdivisions),
                reports.Sum(r => r.Work.RootIterations), reports.Sum(r => r.Work.DetectedConflicts), reports.Sum(r => r.Work.UnresolvedIntervals)),
                reports.SelectMany(r => r.FrameCoverageGaps).ToArray());
        }
        Projection Project(InteractionAlternative alternative)
        {
            var key = (alternative.RiderId, alternative.Intent, alternative.DriveControl?.PositiveDriveFraction ?? 1f, alternative.HoldLateralPosition);
            if (cache.TryGetValue(key, out var existing)) return existing with { Alternative = alternative };
            var evaluator = alternative.DriveControl.HasValue || alternative.HoldLateralPosition
                ? new TrajectoryEvaluator(new RiderDecisionContext(snapshot, snapshot.Rider(alternative.RiderId)),
                    driveControl: alternative.DriveControl, holdLateralPosition: alternative.HoldLateralPosition) : evaluators[alternative.RiderId];
            var before = evaluator.ProductionResolutionCount;
            var traversal = evaluator.Evaluate(alternative.Intent, retainResolvedMotions: true);
            production += evaluator.ProductionResolutionCount - before;
            var projectedPoses = traversal.ResolvedMotions.SelectMany(m => ResolvedBikePoses.FromMotion(m, snapshot.Track, embedding: tracker.Embedding)).ToArray();
            var projection = new Projection(alternative, traversal, projectedPoses,
                WithinTrack(projectedPoses, snapshot.Track, tracker.Embedding!));
            cache.Add(key, projection); return projection;
        }
        var baseline = original.Values.Select(Project).ToDictionary(x => x.Alternative.RiderId);
        var forecast = Verify(baseline.Values);
        tracker.ObserveClearance(now, forecast.Intervals.Where(r => r.IntervalEndSeconds > now).ToArray(), p);
        var edges = Threats(forecast, baseline.Values.SelectMany(x => x.Poses).ToArray(), now, p);
        var clusters = Clusters(edges, snapshot, p);
        foreach (var episode in tracker.Active.Where(e => e.LastDiagnostic is not null))
        {
            var active = episode.Riders.Where(id => snapshot.Rider(id).IsActive).ToArray();
            if (active.Length >= 2 && !clusters.Any(c => c.Riders.Intersect(active).Count() >= 2))
                clusters.Add(new(active, episode.LastDiagnostic!.Geometry.ToArray(), episode.Context, Meaningful:false));
        }
        var selected = new Dictionary<int, InteractionAlternative>(original);
        var diagnostics = new List<InteractionEpisodeDiagnostic>(tracker.Closed);
        var fallbackPairs = new HashSet<(int, int)>();
        foreach (var cluster in clusters)
        {
            var episode = cluster.Meaningful
                ? tracker.Engage(cluster.Riders, Math.Max(now, cluster.Edges.Min(g => g.CommonTimeSeconds)), cluster.Context)
                : tracker.Active.First(e => e.Riders.Intersect(cluster.Riders).Count() >= 2);
            episode.PredictedMechanical |= cluster.Edges.Any(e => e.Space.HasConflict);
            var alternatives = cluster.Riders.ToDictionary(id => id,
                id => Alternatives(snapshot, snapshot.Rider(id), original[id], cluster, episode, p).ToArray());
            var candidates = new List<InteractionCandidateDiagnostic>();
            (InteractionAlternative[] Responses, InteractionCost Cost, double Minimum)? winner = null;
            double TacticalTie(InteractionAlternative[] joint) => joint.Sum(a => DeterministicRandom.Sample01(
                snapshot.Step.Seed, snapshot.Step.HeatId, (int)episode.Id, a.RiderId,
                RandomChannel.InteractionTacticalTie, (int)cluster.Context, (int)a.Response,
                a.Intent.EntryTarget, a.Intent.ApexTarget, a.Intent.ExitTarget,
                BitConverter.SingleToInt32Bits(a.DriveControl?.PositiveDriveFraction ?? 1f), a.HoldLateralPosition ? 1 : 0));
            void Search()
            {
                var choices = cluster.Riders.Select(id => alternatives[id]).ToArray();
                var requestedCount = choices.Aggregate(1, (n, a) => n * a.Length);
                if (candidates.Count + requestedCount > p.MaximumJointCombinations) return;
                foreach (var joint in Joint(choices))
                {
                    combinations++;
                    var trial = new Dictionary<int, InteractionAlternative>(selected);
                    foreach (var a in joint) trial[a.RiderId] = a;
                    Projection[] projections;
                    try { projections = trial.Values.Select(Project).ToArray(); }
                    catch (InvalidOperationException)
                    {
                        candidates.Add(new(candidates.Count, false, "No finite production traversal", 0,
                            new(0, 0, 0, 0), joint)); continue;
                    }
                    var space = Verify(projections);
                    var affected = space.Intervals.Where(r => r.IntervalEndSeconds > now
                        && (cluster.Riders.Contains(r.RiderA) || cluster.Riders.Contains(r.RiderB))).ToArray();
                    var minimum = affected.Where(Eligible).Select(r => r.MinimumSeparationMeters).DefaultIfEmpty(p.CompetitiveReachMeters).Min();
                    var mechanical = affected.Any(r => Eligible(r) && r.HasConflict);
                    var ambiguous = affected.Any(r => !r.NumericallyResolved) || !affected.Any(Eligible) || space.FrameCoverageGaps.Any(g =>
                        g.EndCommonTimeSeconds > now && (cluster.Riders.Contains(g.RiderA) || cluster.Riders.Contains(g.RiderB)));
                    var bounds = projections.Any(x => cluster.Riders.Contains(x.Alternative.RiderId) && !x.WithinTrack);
                    var failed = projections.Any(x => cluster.Riders.Contains(x.Alternative.RiderId) && !x.Traversal.CompletedHorizon);
                    var cost = new InteractionCost(joint.Sum(a => Project(a).Traversal.PredictedTraversalTimeSeconds
                            - baseline[a.RiderId].Traversal.PredictedTraversalTimeSeconds),
                        joint.Sum(a => Deviation(a.Intent, original[a.RiderId].Intent) * .025),
                        joint.Sum(a => a.TacticalPreference),
                        joint.Sum(a => Math.Max(0, InteractionGeometryModel.ExecutionMarginMeters(snapshot.Rider(a.RiderId), p)
                            + .10 * (1 - snapshot.Rider(a.RiderId).Profile.Gameplay.InteractionStyle.Combativeness) - minimum)));
                    var feasible = !mechanical && !ambiguous && !bounds && !failed;
                    candidates.Add(new(candidates.Count, feasible, mechanical ? "Mechanical overlap (#55)" : ambiguous ? "Ineligible boundary/coverage"
                        : bounds ? "Mechanical footprint outside usable track" : failed ? "Incomplete/crashed production horizon" : "", minimum, cost, joint,
                        affected.Count(r => !Eligible(r))));
                    if (feasible && (winner is null || Better(cost, joint, winner.Value.Cost, winner.Value.Responses, TacticalTie)))
                        winner = (joint, cost, minimum);
                }
            }
            var clusterPasses = 1;
            Search(); passes++;
            if (episode.Context == cluster.Context && episode.Commitments.Count > 0
                && !cluster.Edges.Any(e => e.Space.HasConflict && e.TimeToConflictSeconds <= p.EmergencyTimeSeconds))
            {
                var retained = candidates.FirstOrDefault(c => c.Feasible && c.Responses.All(a =>
                    episode.Commitments.TryGetValue(a.RiderId, out var previous) && previous.Response == a.Response
                    && previous.Intent == a.Intent && previous.DriveControl == a.DriveControl
                    && previous.HoldLateralPosition == a.HoldLateralPosition));
                if (retained is not null) winner = (retained.Responses.ToArray(), retained.Cost, retained.MinimumSeparationMeters);
            }
            var unresolvedSafety = candidates.FirstOrDefault(c => !c.Feasible && c.Rejection == "Mechanical overlap (#55)"
                && c.Responses.All(a => a.Response is InteractionResponse.BackOut or InteractionResponse.EmergencyAvoid)
                && c.MinimumSeparationMeters >= cluster.Edges.Min(g => g.Space.MinimumSeparationMeters) - GeometryNumerics.MinimumSeparationToleranceMeters);
            var chosen = winner?.Responses ?? unresolvedSafety?.Responses.ToArray() ?? cluster.Riders.Select(id => selected[id]).ToArray();
            if (winner is null && unresolvedSafety is not null)
            {
                var attempted = new Dictionary<int, InteractionAlternative>(selected);
                foreach (var a in chosen) attempted[a.RiderId] = a;
                var projected = attempted.Values.Select(Project).ToArray();
                var originalConflicts = forecast.Intervals.Where(r => r.EligibleForFutureInteraction).Select(r => Pair(r.RiderA,r.RiderB)).ToHashSet();
                if (projected.Any(x => !x.WithinTrack || !x.Traversal.CompletedHorizon)
                    || Verify(projected).Intervals.Any(r => r.EligibleForFutureInteraction && !originalConflicts.Contains(Pair(r.RiderA,r.RiderB))))
                    chosen = cluster.Riders.Select(id => selected[id]).ToArray();
            }
            foreach (var choice in chosen)
            {
                selected[choice.RiderId] = choice;
                if (episode.Commitments.TryGetValue(choice.RiderId, out var previous) && previous.Response != choice.Response)
                    episode.ResponseChanges++;
                episode.Commitments[choice.RiderId] = choice;
            }
            episode.Context = cluster.Context;
            var unresolved = new List<UnresolvedMechanicalContact>();
            var projectedFinal = Verify(selected.Values.Select(Project));
            foreach (var conflict in projectedFinal.Intervals.Where(r => r.IntervalEndSeconds > now && r.EligibleForFutureInteraction
                && (cluster.Riders.Contains(r.RiderA) || cluster.Riders.Contains(r.RiderB)))
                .GroupBy(r => Pair(r.RiderA, r.RiderB)).Select(g => g.OrderBy(r => r.FirstTouchCommonTimeSeconds).First()))
                unresolved.Add(Unresolved(episode, conflict, chosen, "No clear bounded joint alternative"));
            diagnostics.Add(new(episode.Id, episode.Riders, cluster.Context, episode.Start, episode.End,
                cluster.Edges.Min(g => g.Space.MinimumSeparationMeters), cluster.Edges.Aggregate(SpaceConflictKind.None, (k, g) => k | g.Space.Kind),
                cluster.Edges, cluster.Riders.Select(id => original[id]).ToArray(), alternatives.Values.SelectMany(a => a).ToArray(), chosen,
                candidates, clusterPasses, winner?.Minimum ?? cluster.Edges.Min(g => g.Space.MinimumSeparationMeters),
                unresolved.Count == 0, unresolved, false, episode.ResponseChanges, Math.Max(0, episode.LastActive - episode.Start)));
        }
        var finalIntents = intents.Select(i => new RiderIntent(i.RiderId, i.Decision with
        {
            TargetLane = CurrentTarget(selected[i.RiderId].Intent, snapshot),
            Trajectory = selected[i.RiderId].Intent, DriveControl = selected[i.RiderId].DriveControl,
            HoldLateralPosition = selected[i.RiderId].HoldLateralPosition,
        })).ToArray();
        // Re-resolve actual current paths with the real addressed incident options.
        var actual = engine.ResolveProduction(snapshot, finalIntents, options, legacyContacts: false);
        production++;
        var verification = CommonTimePoseHistory.Observe(tracker.History.Concat(Poses(actual, snapshot.Track)));
        narrow += verification.Work.NarrowPhaseEvaluations;
        foreach (var conflict in verification.Intervals.Where(r => r.IntervalEndSeconds > now && r.EligibleForFutureInteraction))
        {
            var episode = tracker.Active.FirstOrDefault(e => e.Riders.Contains(conflict.RiderA) && e.Riders.Contains(conflict.RiderB));
            if (episode is null) continue;
            var index = diagnostics.FindIndex(d => d.EpisodeId == episode.Id);
            if (index >= 0)
            {
                var d = diagnostics[index];
                var contacts = d.UnresolvedMechanicalContacts.ToList();
                if (!contacts.Any(c => Pair(c.RiderA, c.RiderB) == Pair(conflict.RiderA, conflict.RiderB)))
                    contacts.Add(Unresolved(episode, conflict, d.SelectedResponses, "Actual production verification remains in contact"));
                diagnostics[index] = d with { ResolvedWithoutMechanicalContact = false,
                    FinalMinimumSeparationMeters = Math.Min(d.FinalMinimumSeparationMeters, conflict.MinimumSeparationMeters),
                    UnresolvedMechanicalContacts = contacts };
            }
            // One attempt per whole episode, even if the legacy occurrence roll does nothing.
            if (!episode.FallbackAttempted)
            { episode.FallbackAttempted = true; fallbackPairs.Add(Pair(conflict.RiderA, conflict.RiderB)); }
        }
        foreach (var pair in fallbackPairs)
        {
            var index = diagnostics.FindIndex(d => d.RiderIds.Contains(pair.Item1) && d.RiderIds.Contains(pair.Item2));
            if (index >= 0) diagnostics[index] = diagnostics[index] with { LegacyFallbackUsed = true };
        }
        foreach (var episode in tracker.Active)
            episode.LastDiagnostic = diagnostics.LastOrDefault(d => d.EpisodeId == episode.Id) ?? episode.LastDiagnostic;
        var final = engine.ResolveProduction(snapshot, finalIntents, options, contactFilter: (a, b) =>
            !tracker.Owns(a, b) || fallbackPairs.Contains(Pair(a, b)), unresolvedPairs: fallbackPairs.Select(pair =>
                actual.Changes.Single(c => c.RiderId == pair.Item1).ElapsedTimeSeconds
                    <= actual.Changes.Single(c => c.RiderId == pair.Item2).ElapsedTimeSeconds
                    ? (pair.Item1, pair.Item2) : (pair.Item2, pair.Item1)).ToArray());
        production++;
        narrow += tracker.Retain(final);
        return final.withInteraction(new(diagnostics, new(combinations, production, narrow, passes, clusters.Count)), owner, tracker);
    }

    private IReadOnlyList<PhysicalPoseInterval> Poses(ResolvedSimulationStep step, Track track)
        => step.Motions.SelectMany(m => ResolvedBikePoses.FromMotion(m, track, embedding: tracker.Embedding)).ToArray();
    private static bool Eligible(ContestedSpaceEvent r) => r.NumericallyResolved && !r.Kind.HasFlag(SpaceConflictKind.BoundaryAmbiguous);
    private static (int, int) Pair(int a, int b) => (Math.Min(a, b), Math.Max(a, b));
    private static InteractionAlternative Original(RiderIntent i) => new(i.RiderId, InteractionResponse.KeepIntent,
        i.Decision.Trajectory ?? new(i.Decision.TargetLane, i.Decision.TargetLane, i.Decision.TargetLane), null, 0, "Independent #54 intent");
    private static int CurrentTarget(TrajectoryIntent intent, SimulationSnapshot snapshot)
        => intent.TargetFor(snapshot.Segment.Type);
    private static double Deviation(TrajectoryIntent a, TrajectoryIntent b)
        => Math.Abs(a.EntryTarget - b.EntryTarget) + Math.Abs(a.ApexTarget - b.ApexTarget) + Math.Abs(a.ExitTarget - b.ExitTarget);
    private static bool Better(InteractionCost a, InteractionAlternative[] ar, InteractionCost b, InteractionAlternative[] br,
        Func<InteractionAlternative[],double> tacticalTie)
    {
        if (Math.Abs(a.ClearanceShortfallMeters - b.ClearanceShortfallMeters) > GeometryNumerics.MinimumSeparationToleranceMeters)
            return a.ClearanceShortfallMeters < b.ClearanceShortfallMeters;
        if (Math.Abs(a.Total - b.Total) > 1e-9) return a.Total < b.Total;
        var originalsA = ar.Count(x => x.Response == InteractionResponse.KeepIntent);
        var originalsB = br.Count(x => x.Response == InteractionResponse.KeepIntent);
        if (originalsA != originalsB) return originalsA > originalsB;
        // Only genuinely tied, already-safe alternatives reach this addressed
        // sample. IDs never confer fixed priority; safety consumes no randomness.
        return tacticalTie(ar) < tacticalTie(br);
    }
    private static IEnumerable<InteractionAlternative[]> Joint(InteractionAlternative[][] choices, int index = 0)
    {
        if (index == choices.Length) { yield return Array.Empty<InteractionAlternative>(); yield break; }
        foreach (var suffix in Joint(choices, index + 1)) foreach (var choice in choices[index])
            yield return new[] { choice }.Concat(suffix).ToArray();
    }
    internal static bool WithinTrack(IEnumerable<PhysicalPoseInterval> poses, Track track, TrackMetricEmbedding embedding)
    {
        foreach (var interval in poses)
            if (!Certified(interval, interval.StartTimeSeconds, interval.EndTimeSeconds, 0)) return false;
        return true;

        bool Certified(PhysicalPoseInterval interval, double start, double end, int depth)
        {
            var middle = (start + end) / 2;
            var clearance = Math.Min(Clearance(interval.Sample(start)),
                Math.Min(Clearance(interval.Sample(middle)), Clearance(interval.Sample(end))));
            if (clearance < 0) return false;
            var rates = interval.RateBounds(start,end);
            var centerSpeed = rates.CenterVelocityMetersPerSecond.Length
                + rates.CenterAccelerationBoundMetersPerSecondSquared * (end - start) / 2;
            // The support axis turns with the production corner tangent. These
            // conservative rates bound edge clearance between sampled poses.
            var rate = centerSpeed + interval.Dimensions.BoundingRadiusMeters
                * (rates.AngularSpeedBoundRadiansPerSecond + centerSpeed / track.Geometry.InnerRadiusMeters);
            if (clearance > rate * (end - start) / 4) return true;
            if (depth == 12) return false; // Uncertified edge clearance fails closed.
            return Certified(interval,start,middle,depth+1) && Certified(interval,middle,end,depth+1);
        }
        double Clearance(PhysicalBikePose pose)
        {
            var source = pose.Source!;
            var tangent = pose.ReferenceTangentHeadingRadians;
            var outward = new MeterPoint(Math.Sin(tangent), -Math.Cos(tangent));
            var projected = InteractionGeometryModel.Project(pose.Footprint, outward);
            var center = MeterPoint.Dot(pose.Position, outward);
            var span = LaneModel.UsableRacingWidthMeters(source.SegmentType, track.Geometry);
            // Find physical offset from the exact common #55 embedding, not requested Lane.
            var segment = embedding.Segments[source.SegmentIndex];
            var offset = source.SegmentType == SegmentType.Straight
                ? MeterPoint.Dot(pose.Position - segment.StartReferencePosition, outward)
                : (pose.Position - (segment.StartReferencePosition + new MeterPoint(-Math.Sin(segment.StartTangentHeadingRadians),
                    Math.Cos(segment.StartTangentHeadingRadians)) * segment.InnerRadiusMeters)).Length - track.Geometry.InnerRadiusMeters;
            var radialBulge = source.SegmentType == SegmentType.Straight ? 0
                : pose.Dimensions.BoundingRadiusMeters * pose.Dimensions.BoundingRadiusMeters
                    / (2 * Math.Max(1, track.Geometry.InnerRadiusMeters - pose.Dimensions.BoundingRadiusMeters));
            return Math.Min(offset + projected.Min - center + TrackGeometry.InnerReferenceOffsetFromTrackEdgeMeters,
                span + TrackGeometry.ProvisionalOuterReferenceOffsetFromTrackEdgeMeters - offset - projected.Max + center - radialBulge);
        }
    }
    private static InteractionGeometry[] Threats(ContestedSpaceReport report, PhysicalPoseInterval[] poses,
        double now, ContestedSpaceParameters p)
    {
        var edges = new List<InteractionGeometry>();
        foreach (var pair in report.Intervals.Where(r => r.IntervalEndSeconds > now && Eligible(r)
            && r.MinimumSeparationMeters <= p.CompetitiveReachMeters).GroupBy(r => Pair(r.RiderA, r.RiderB)))
        {
            var row = pair.OrderBy(r => r.MinimumSeparationMeters).First();
            var common = report.Intervals.Where(r => Pair(r.RiderA, r.RiderB) == pair.Key && r.IntervalEndSeconds > now && Eligible(r))
                .OrderBy(r => r.IntervalStartSeconds).First();
            var time = Math.Max(now, common.IntervalStartSeconds);
            var a = poses.FirstOrDefault(i => i.RiderId == row.RiderA && i.StartTimeSeconds <= time && i.EndTimeSeconds >= time);
            var b = poses.FirstOrDefault(i => i.RiderId == row.RiderB && i.StartTimeSeconds <= time && i.EndTimeSeconds >= time);
            if (a is null || b is null) continue;
            var geometry = InteractionGeometryModel.Describe(row, a.Sample(time), b.Sample(time), a.RateBounds(time, time), b.RateBounds(time, time));
            if (row.HasConflict || row.MinimumSeparationMeters <= p.PressureClearanceMeters
                || (geometry.ForwardFootprintOverlapMeters > 0 && row.MinimumSeparationMeters <= p.AlongsideCompetitiveClearanceMeters)
                || (Math.Abs(geometry.ForwardClosingMetersPerSecond) >= p.MinimumCompetitiveClosingSpeedMetersPerSecond
                    && geometry.ForwardBFromAMeters * geometry.ForwardClosingMetersPerSecond < 0))
                edges.Add(geometry);
        }
        return edges.ToArray();
    }
    internal static List<Cluster> Clusters(InteractionGeometry[] edges, SimulationSnapshot snapshot, ContestedSpaceParameters p)
    {
        var remaining = edges.OrderBy(g => g.CommonTimeSeconds).ThenBy(g => Math.Min(g.RiderA,g.RiderB))
            .ThenBy(g => Math.Max(g.RiderA,g.RiderB)).ToList(); var result = new List<Cluster>();
        while (remaining.Count > 0)
        {
            var first = remaining.OrderBy(g => g.CommonTimeSeconds).First(); remaining.Remove(first);
            var component = new List<InteractionGeometry> { first }; var riders = new HashSet<int> { first.RiderA, first.RiderB };
            bool added;
            do
            {
                added = false;
                foreach (var g in remaining.ToArray())
                    if ((riders.Contains(g.RiderA) || riders.Contains(g.RiderB))
                        && component.Any(e => Math.Abs(e.CommonTimeSeconds - g.CommonTimeSeconds) <= p.ClusterTimeWindowSeconds))
                    { component.Add(g); riders.Add(g.RiderA); riders.Add(g.RiderB); remaining.Remove(g); added = true; }
            } while (added);
            var corner = snapshot.Track.CornerTopology.CornerForSegment(snapshot.Step.SegmentIndex);
            var firstCorner = snapshot.Track.CornerTopology.CornerForSegment(snapshot.Track.Segments
                .Select((s, i) => (s, i)).FirstOrDefault(x => x.s.Type != SegmentType.Straight).i);
            var firstBend = snapshot.Step.LapIndex == 0 && snapshot.Track.Segments[0].IsStandingStartSegment
                && (snapshot.Step.SegmentIndex == 0 || corner?.CornerId == firstCorner?.CornerId);
            var context = firstBend ? InteractionContext.FirstBendCluster : snapshot.Segment.Type switch
            {
                SegmentType.TurnEntry => InteractionContext.CornerEntryClosing,
                SegmentType.TurnMiddle => InteractionContext.MidCornerPressure,
                SegmentType.TurnExit => InteractionContext.CornerExitCross,
                _ => InteractionContext.StraightReattack,
            };
            if (context is InteractionContext.MidCornerPressure && component.Any(g => g.ForwardFootprintOverlapMeters > 0))
                context = InteractionContext.InsideOverlap;
            result.Add(new(riders.Order().ToArray(), component.ToArray(), context));
        }
        return result;
    }
    private static IEnumerable<InteractionAlternative> Alternatives(SimulationSnapshot snapshot, RiderSnapshot rider,
        InteractionAlternative original, Cluster cluster, InteractionEpisode episode, ContestedSpaceParameters p)
    {
        yield return original;
        var related = cluster.Edges.Where(g => g.RiderA == rider.RiderId || g.RiderB == rider.RiderId).ToArray();
        if (related.Length == 0) yield break; // A separated former member keeps its independent intent.
        var nearest = related.OrderBy(g => Math.Abs(g.ForwardBFromAMeters)).First();
        var orientation = nearest.RiderA == rider.RiderId ? 1 : -1;
        var otherForward = orientation * nearest.ForwardBFromAMeters;
        var otherOutward = orientation * nearest.OutwardBFromAMeters;
        var establishedInside = related.Any(g => g.ForwardFootprintOverlapMeters > 0
            && (g.RiderA == rider.RiderId ? g.OutwardBFromAMeters : -g.OutwardBFromAMeters) < 0);
        var imminent = related.Any(g => g.Space.HasConflict && g.TimeToConflictSeconds <= p.EmergencyTimeSeconds);
        var gameplay = rider.Profile.Gameplay; var abilities = gameplay.Abilities; var style = gameplay.InteractionStyle;
        var current = LaneModel.ClampLane((int)MathF.Round(rider.LateralPosition));
        InteractionAlternative? retainedCommitment = null;
        var plan = original.Intent; var response = InteractionResponse.Hold; var target = current;
        if (episode.Commitments.TryGetValue(rider.RiderId, out var committed) && episode.Context == cluster.Context
            && !imminent && !(committed.Response == InteractionResponse.CoverInside && establishedInside))
        { plan = committed.Intent; response = committed.Response; retainedCommitment = committed; }
        else if (otherForward < 0 && otherOutward < 0 && !establishedInside && snapshot.Segment.Type is SegmentType.Straight or SegmentType.TurnEntry
            && nearest.TimeToConflictSeconds < .35 + (abilities.Defense - 1) / 98d * 1.5)
        { response = InteractionResponse.CoverInside; target = Math.Max(0, current - 1); plan = new(target, target, original.Intent.ExitTarget); }
        else if (snapshot.Segment.Type is SegmentType.TurnMiddle or SegmentType.TurnExit && otherForward > 0 && otherOutward < 0)
        { response = InteractionResponse.CutInside; plan = new(original.Intent.EntryTarget, original.Intent.ApexTarget, Math.Max(0, current - 1)); }
        else if (otherOutward < 0)
        { response = establishedInside ? InteractionResponse.YieldOutward : InteractionResponse.ContinueOutside;
            target = Math.Min(4, current + 1); plan = new(target, target, target); }
        else { plan = new(current, current, original.Intent.ExitTarget); }
        var quality = (response == InteractionResponse.CoverInside ? abilities.Defense : abilities.Attack) / 99d;
        var tactical = -.18 * quality * style.Combativeness;
        // PreferredLine is a small tie preference, not a physical bonus.
        tactical += style.PreferredLine == PreferredLine.Inside ? .002 * plan.ExitTarget
            : style.PreferredLine == PreferredLine.Outside ? .002 * (4 - plan.ExitTarget) : 0;
        yield return retainedCommitment ?? new(rider.RiderId, response, plan, null, tactical, establishedInside
            ? "Established footprint overlap forbids covering through the inside rider" : "Local phase response; production feasibility required",
            response == InteractionResponse.Hold);
        yield return new(rider.RiderId, imminent ? InteractionResponse.EmergencyAvoid : InteractionResponse.BackOut,
            new(current, current, current), RiderDriveControl.LiftThrottle, .04 * style.Combativeness,
            "Hold available lateral position and lift positive drive; no direct speed change", true);
    }
    private static UnresolvedMechanicalContact Unresolved(InteractionEpisode episode, ContestedSpaceEvent conflict,
        IReadOnlyList<InteractionAlternative> chosen, string reason) => new(episode.Id, conflict.RiderA, conflict.RiderB,
            conflict.FirstTouchCommonTimeSeconds!.Value, conflict.ComponentA, conflict.ComponentB,
            conflict.MinimumSeparationMeters, episode.Context, chosen, reason);
}

internal static class InteractionStepExtensions
{
    internal static ResolvedSimulationStep withInteraction(this ResolvedSimulationStep step, InteractionResolution interaction,
        InteractionEpisodeTracker? owner, InteractionEpisodeTracker resolved)
        => new(step.Snapshot, step.Changes, step.Events, step.Diagnostics, step.Motions)
            { Interaction = interaction, CommitInteractionState = owner is null ? null : () => owner.CommitFrom(resolved) };
}
