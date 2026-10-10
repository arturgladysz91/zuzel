using CoreSim.Decisions;
using CoreSim.PhysicalSpace;
using CoreSim.Race;

namespace CoreSim.Interactions;

/// <summary>
/// Bounded simultaneous traffic layer. #54 executes every alternative; #55 alone
/// determines mechanical overlap. This class changes requests, never rider state.
/// </summary>
internal sealed class ContestedSpaceInteractionCoordinator(InteractionEpisodeTracker tracker, bool reusePairResults = true,
    Func<SimulationSnapshot, RiderIntent, HeatSimulationOptions, ResolvedSimulationStep>? resolveOptionalSafety = null)
{
    private sealed record PairAlternativeResult(double MinimumSeparation, bool HasBoundaryAmbiguityOrCoverageGap,
        int IneligibleIntervals, IReadOnlyList<ContestedSpaceEvent> Intervals);
    private sealed record Projection(InteractionAlternative Alternative, TrajectoryTraversal Traversal,
        IReadOnlyList<PhysicalPoseInterval> Poses, bool WithinTrack);
    internal sealed record Cluster(int[] Riders, InteractionGeometry[] Edges, InteractionContext Context, bool Meaningful = true);
    private sealed record SafetyEvaluationScope(int[] Riders, InteractionGeometry[] Contacts);
    private static ProjectionKey Key(InteractionAlternative a)
        => ProjectionKey.From(a);

    public ResolvedSimulationStep Resolve(SimulationEngine engine, SimulationSnapshot snapshot,
        IReadOnlyList<RiderIntent> intents, HeatSimulationOptions options, InteractionEpisodeTracker? owner = null)
    {
        ArgumentNullException.ThrowIfNull(engine); ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(intents); options.Validate();
        if (snapshot.Riders.Count > 4) throw new ArgumentException("Contested space supports at most four riders per heat.");
        tracker.Bind(snapshot, options.EnablePhysicalContactConsequences);
        var p = options.ContestedSpaceParameters;
        TrackMetricEmbedding? targetEmbedding = null;
        Dictionary<PhysicalPoseInterval,PhysicalPoseInterval>? targetPoseCache = null;
        TrackMetricEmbedding TargetEmbedding() => targetEmbedding ??= tracker.Embedding!.DeterministicArithmetic
            ? tracker.Embedding : new(snapshot.Track,true);
        PhysicalPoseInterval TargetPose(PhysicalPoseInterval pose)
        {
            if(pose.DeterministicArithmetic) return pose;
            targetPoseCache??=new(ReferenceEqualityComparer.Instance);
            if (targetPoseCache.TryGetValue(pose,out var found)) return found;
            var result=ResolvedBikePoses.InEmbedding(pose,TargetEmbedding());
            targetPoseCache.Add(pose,result);return result;
        }
        // These adapters belong only to this immutable Resolve. Prefix sharing can
        // return the very same motion in several alternative horizons.
        var poseCache = new Dictionary<ResolvedRiderMotion, IReadOnlyList<PhysicalPoseInterval>>(ReferenceEqualityComparer.Instance);
        IReadOnlyList<PhysicalPoseInterval> MotionPoses(ResolvedRiderMotion motion)
        {
            if (poseCache.TryGetValue(motion, out var poses)) return poses;
            poses = ResolvedBikePoses.FromMotion(motion, snapshot.Track, embedding: tracker.Embedding);
            poseCache.Add(motion, poses);
            return poses;
        }
        PhysicalPoseInterval[] Poses(ResolvedSimulationStep step)
            => step.Motions.SelectMany(MotionPoses).ToArray();
        // Pair eligibility follows either member's current request, never an unrelated heat clock.
        var ridersById = snapshot.Riders.ToDictionary(r => r.RiderId);
        var requestTimes = snapshot.Riders.ToDictionary(r => r.RiderId,r => (double)r.ElapsedTimeSeconds);
        double Ready(int a,int b) => Math.Min(requestTimes[a],requestTimes[b]);
        bool Future(ContestedSpaceEvent row) => ridersById[row.RiderA].IsActive && ridersById[row.RiderB].IsActive
            && row.IntervalEndSeconds>Ready(row.RiderA,row.RiderB);
        bool FutureGap(FrameCoverageGap gap) => gap.EndCommonTimeSeconds>Ready(gap.RiderA,gap.RiderB);
        var independent = engine.ResolveProduction(snapshot, intents, options, legacyContacts: false);
        var currentPoses = Poses(independent);
        var direct = CommonTimePoseHistory.Observe(tracker.History.Concat(currentPoses));
        var now = snapshot.Riders.Where(r => r.IsActive).Select(r => (double)r.ElapsedTimeSeconds).DefaultIfEmpty(0).Min();
        var directRows = direct.Intervals.Where(Future).ToArray();
        // No planning or extra production replay for spatially unrelated riders.
        if (!directRows.Any(r => Eligible(r) && r.MinimumSeparationMeters <= p.CompetitiveReachMeters)
            && !tracker.Active.Any())
        {
            // Same snapshot, intents, options and disabled legacy contacts. The
            // direct observation also covers exactly History + these poses.
            if (options.EnablePhysicalContactConsequences) tracker.ObservePhysicalClearance(direct, p);
            tracker.RetainVerified(independent, currentPoses, direct);
            return independent.withInteraction(new(tracker.Closed.ToArray(),
                new(0, 1, direct.Work.NarrowPhaseEvaluations, 0, 0) { ActualProductionVerifications = 1 }), owner, tracker);
        }

        var original = intents.ToDictionary(i => i.RiderId, i => Original(i));
        var evaluators = snapshot.Riders.Where(r => r.IsActive).ToDictionary(r => r.RiderId,
            r => new TrajectoryEvaluator(new RiderDecisionContext(snapshot, r)));
        var cache = new Dictionary<ProjectionKey, Projection>();
        var failedProjections = new HashSet<ProjectionKey>();
        var production = 1; var narrow = direct.Work.NarrowPhaseEvaluations; var combinations = 0; var passes = 0;
        var pairChecks = 0;
        var outwardTargetTrials = 0;
        var continuousTargetSearch = tracker.Active.Any(e=>e.Commitments.Values.Any(a=>a.PhysicalTarget.HasValue));
        var pairReports = new Dictionary<(ProjectionKey A, ProjectionKey B), PairAlternativeResult>();
        PairAlternativeResult PairCheck(Projection a, Projection b)
        {
            var key = (Key(a.Alternative), Key(b.Alternative));
            if (reusePairResults && pairReports.TryGetValue(key, out var found)) return found;
            var poses=tracker.History.Where(i => i.RiderId == key.Item1.RiderId || i.RiderId == key.Item2.RiderId)
                .Concat(a.Poses).Concat(b.Poses);
            if(continuousTargetSearch) poses=poses.Select(TargetPose);
            var report = CommonTimePoseHistory.Compatibility(poses, Ready(key.Item1.RiderId, key.Item2.RiderId));
            pairChecks++; narrow += report.Work.NarrowPhaseEvaluations;
            var result = new PairAlternativeResult(report.EligibleIntervals == 0 ? p.CompetitiveReachMeters : report.MinimumSeparation,
                report.EligibleIntervals == 0 || report.HasBoundaryAmbiguity || report.HasCoverageGap,
                report.IneligibleIntervals, report.Contacts.ToArray());
            pairReports[key] = result;
            return result;
        }
        PairAlternativeResult Verify(IEnumerable<Projection> projected, int[]? affectedRiders = null)
        {
            var riders = projected.OrderBy(x => x.Alternative.RiderId).ToArray();
            var minimum = double.PositiveInfinity; var ambiguous = false; var ineligible = 0;
            var contacts = new List<ContestedSpaceEvent>();
            for (var a = 0; a < riders.Length; a++) for (var b = a + 1; b < riders.Length; b++)
            {
                if (affectedRiders is not null && !affectedRiders.Contains(riders[a].Alternative.RiderId)
                    && !affectedRiders.Contains(riders[b].Alternative.RiderId)) continue;
                var pair = PairCheck(riders[a], riders[b]);
                minimum = Math.Min(minimum, pair.MinimumSeparation);
                ambiguous |= pair.HasBoundaryAmbiguityOrCoverageGap; ineligible += pair.IneligibleIntervals;
                contacts.AddRange(pair.Intervals);
            }
            return new(double.IsPositiveInfinity(minimum) ? p.CompetitiveReachMeters : minimum, ambiguous, ineligible, contacts.ToArray());
        }
        Projection Project(InteractionAlternative alternative)
        {
            var key = Key(alternative);
            if (cache.TryGetValue(key, out var existing)) return existing.Alternative == alternative ? existing : existing with { Alternative = alternative };
            var evaluator = alternative.DriveControl.HasValue || alternative.HoldLateralPosition || alternative.PhysicalTarget.HasValue
                ? new TrajectoryEvaluator(new RiderDecisionContext(snapshot, snapshot.Rider(alternative.RiderId)),
                    driveControl: alternative.DriveControl, holdLateralPosition: alternative.HoldLateralPosition,
                    physicalTarget: alternative.PhysicalTarget) : evaluators[alternative.RiderId];
            var before = evaluator.ProductionResolutionCount;
            if (failedProjections.Contains(key)) throw new InvalidOperationException("No finite production traversal");
            TrajectoryTraversal traversal;
            try { traversal = evaluator.Evaluate(alternative.Intent, retainResolvedMotions: true); }
            catch (InvalidOperationException) { failedProjections.Add(key); production += evaluator.ProductionResolutionCount - before; throw; }
            production += evaluator.ProductionResolutionCount - before;
            var projectedPoses = traversal.ResolvedMotions.SelectMany(MotionPoses).ToArray();
            var boundsPoses=alternative.PhysicalTarget.HasValue?projectedPoses.Select(TargetPose):projectedPoses;
            var projection = new Projection(alternative, traversal, projectedPoses,
                WithinTrack(boundsPoses, snapshot.Track, alternative.PhysicalTarget.HasValue?TargetEmbedding():tracker.Embedding!));
            cache.Add(key, projection); return projection;
        }
        var baseline = original.Values.Select(Project).ToDictionary(x => x.Alternative.RiderId);
        InteractionAlternative MinimalOutward(InteractionAlternative response, InteractionGeometry[] related)
        {
            if(!continuousTargetSearch) { continuousTargetSearch=true;pairReports.Clear(); }
            var rider = snapshot.Rider(response.RiderId);
            var lower = LaneModel.PhysicalLateralOffsetFromInnerReferenceMeters(rider.LateralPosition,
                snapshot.Segment.Type, snapshot.Track.Geometry);
            var upper = evaluators[rider.RiderId].Horizon.Min(part => LaneModel.UsableRacingWidthMeters(
                snapshot.Track.Segments[part.SegmentIndex].Type, snapshot.Track.Geometry));
            // Inner opponents' entire executed requests constrain the destination, not
            // just their initial lateral gap. Every final joint pair is certified again.
            var inside = related.Where(g => (g.RiderA == rider.RiderId ? g.OutwardBFromAMeters : -g.OutwardBFromAMeters) < 0)
                .Select(g => g.RiderA == rider.RiderId ? g.RiderB : g.RiderA).Distinct().Order().ToArray();
            var margin = InteractionGeometryModel.ExecutionMarginMeters(rider, p)
                + .10 * (1 - rider.Profile.Gameplay.InteractionStyle.Combativeness);
            var certifiedMargins = new Dictionary<ProjectionKey, double>();
            InteractionAlternative At(float offset) => response with { PhysicalTarget = new(offset) };
            bool Clear(InteractionAlternative candidate)
            {
                outwardTargetTrials++;
                Projection projection;
                try { projection = Project(candidate); } catch (InvalidOperationException) { return false; }
                if (!projection.WithinTrack || !projection.Traversal.CompletedHorizon) return false;
                var achievedMargin = double.PositiveInfinity;
                foreach (var id in inside)
                {
                    var pair = rider.RiderId < id ? PairCheck(projection, baseline[id]) : PairCheck(baseline[id], projection);
                    if (pair.HasBoundaryAmbiguityOrCoverageGap || pair.Intervals.Count > 0) return false;
                    achievedMargin = Math.Min(achievedMargin, pair.MinimumSeparation);
                }
                certifiedMargins[Key(candidate)] = achievedMargin;
                return achievedMargin >= margin;
            }
            if (lower > upper) return response with { HoldLateralPosition = true };
            var held = At(lower);
            if (Clear(held)) return held;
            var high = upper;
            if (cache.TryGetValue(Key(held), out var heldProjection))
            {
                var shortfall = inside.Max(id => Math.Max(0, margin - (rider.RiderId < id
                    ? PairCheck(heldProjection, baseline[id]) : PairCheck(baseline[id], heldProjection)).MinimumSeparation));
                high = Math.Min(upper, lower + (float)shortfall + OutwardTargetToleranceMeters);
            }
            var best = At(high);
            var seeded = best;
            var seededHigh = high;
            // Failure remains a physical attempt, with no certificate. Joint filters,
            // actual replay and existing safety/contact consequences retain authority.
            var clear = Clear(best);
            if (!clear && high < upper) { high = upper; best = At(high); clear = Clear(best); }
            if (!clear)
            {
                if (!certifiedMargins.TryGetValue(Key(best), out var attainableMargin))
                {
                    // A physical outer endpoint can fail the footprint bound even
                    // though the seed was mechanically clear. Keep that certificate.
                    if (!certifiedMargins.TryGetValue(Key(seeded), out attainableMargin)) return held;
                    best = seeded; high = seededHigh;
                }
                // A tight initial gap may make the preferred buffer unattainable
                // even with ample later room. Mechanical certification still holds;
                // never discard a genuinely clear outward response for that reason.
                margin = attainableMargin;
                if (certifiedMargins.TryGetValue(Key(held), out var heldMargin) && heldMargin >= margin) return held;
            }
            upper = high;
            for (var trial = 0; trial < OutwardTargetRefinements && upper - lower > OutwardTargetToleranceMeters; trial++)
            {
                var middle = (lower + upper) * .5f;
                var candidate = At(middle);
                if (Clear(candidate)) { upper = middle; best = candidate; } else lower = middle;
            }
            return best;
        }
        var forecast = CommonTimePoseHistory.Observe(tracker.History.Concat(baseline.Values.SelectMany(x => x.Poses)));
        narrow += forecast.Work.NarrowPhaseEvaluations;
        var edges = Threats(forecast, tracker.History.Concat(baseline.Values.SelectMany(x => x.Poses)).ToArray(), now, p,requestTimes);
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
        void ReconcileComponents(IEnumerable<Cluster> components)
        {
            foreach (var component in components)
            {
                var episode = tracker.Reconcile(component, component.Riders.Min(id => requestTimes[id]));
                var previous = diagnostics.Where(d => d.EpisodeId == episode.Id || episode.MergedEpisodeIds.Contains(d.EpisodeId)).ToArray();
                diagnostics.RemoveAll(d => d.EpisodeId == episode.Id || episode.MergedEpisodeIds.Contains(d.EpisodeId));
                var ids = episode.Riders.Where(selected.ContainsKey).Order().ToArray();
                var prior = previous.FirstOrDefault(d => d.EpisodeId == episode.Id) ?? previous.FirstOrDefault();
                var geometry = component.Edges.Concat(previous.SelectMany(d => d.Geometry))
                    .GroupBy(g => Pair(g.RiderA,g.RiderB)).Select(g => g.First())
                    .OrderBy(g => Pair(g.RiderA,g.RiderB)).ToArray();
                var diagnostic = prior is null ? new InteractionEpisodeDiagnostic(episode.Id, episode.Riders, component.Context,
                    episode.Start, episode.End, component.Edges.Min(g => g.Space.MinimumSeparationMeters),
                    component.Edges.Aggregate(SpaceConflictKind.None,(kind,g) => kind | g.Space.Kind), geometry,
                    ids.Select(id => original[id]).ToArray(), Array.Empty<InteractionAlternative>(), ids.Select(id => selected[id]).ToArray(),
                    Array.Empty<InteractionCandidateDiagnostic>(), 1, component.Edges.Min(g => g.Space.MinimumSeparationMeters),
                    false, Array.Empty<UnresolvedMechanicalContact>(), false, episode.ResponseChanges, episode.LastActive-episode.Start)
                    : prior with
                    {
                        EpisodeId = episode.Id, RiderIds = episode.Riders, StartTimeSeconds = episode.Start, EndTimeSeconds = episode.End,
                        Geometry = geometry, OriginalIntents = ids.Select(id => original[id]).ToArray(),
                        SelectedResponses = ids.Select(id => selected[id]).ToArray(),
                        ResponseAlternatives = previous.SelectMany(d => d.ResponseAlternatives).Distinct().ToArray(),
                        Candidates = previous.SelectMany(d => d.Candidates).ToArray(),
                        PassCount = previous.Max(d => d.PassCount), ResponseChanges = episode.ResponseChanges,
                        Pass1ActualMechanicalContact = previous.Any(d => d.Pass1ActualMechanicalContact),
                        Pass1SelectedResponses = previous.SelectMany(d => d.Pass1SelectedResponses).DistinctBy(a => a.RiderId).OrderBy(a => a.RiderId).ToArray(),
                    };
                diagnostics.Add(diagnostic);
            }
        }
        foreach (var cluster in clusters)
        {
            var episode = cluster.Meaningful
                ? tracker.Reconcile(cluster, cluster.Riders.Min(id=>requestTimes[id]))
                : tracker.Active.First(e => e.Riders.Intersect(cluster.Riders).Count() >= 2);
            if(cluster.Meaningful && episode.Commitments.Count==0)
                // Preserve the initial forecast window without postponing release on every reforecast.
                episode.ReleaseNotBefore=cluster.Edges.Max(g=>g.Space.FirstTouchCommonTimeSeconds??g.Space.MinimumSeparationCommonTimeSeconds);
            episode.PredictedMechanical |= cluster.Edges.Any(e => e.Space.HasConflict);
            var alternatives = cluster.Riders.ToDictionary(id => id,
                id => Alternatives(snapshot, snapshot.Rider(id), original[id], cluster, episode, p, MinimalOutward).ToArray());
            var candidates = new List<InteractionCandidateDiagnostic>();
            (InteractionAlternative[] Responses, InteractionCost Cost, double Minimum)? winner = null, retained = null, unresolvedSafety = null;
            var searched = 0;
            double TacticalTie(InteractionAlternative[] joint) => joint.Sum(a =>
            {
                var address = new[] { (int)cluster.Context, (int)a.Response,
                    a.Intent.EntryTarget, a.Intent.ApexTarget, a.Intent.ExitTarget,
                    BitConverter.SingleToInt32Bits(a.DriveControl?.PositiveDriveFraction ?? 1f), a.HoldLateralPosition ? 1 : 0 };
                if (a.PhysicalTarget is { } physical)
                    address = address.Append(BitConverter.SingleToInt32Bits(physical.OffsetFromInnerReferenceMeters)).ToArray();
                return DeterministicRandom.Sample01(snapshot.Step.Seed, snapshot.Step.HeatId, (int)episode.Id,
                    a.RiderId, RandomChannel.InteractionTacticalTie, address);
            });
            void Search()
            {
                var choices = cluster.Riders.Select(id => alternatives[id]).ToArray();
                var requestedCount = choices.Aggregate(1, (n, a) => n * a.Length);
                if (searched + requestedCount > p.MaximumJointCombinations) return;
                var all = selected.Keys.Order().ToArray();
                Projection? TryProject(InteractionAlternative alternative)
                { try { return Project(alternative); } catch (InvalidOperationException) { return null; } }
                var buffers = all.Select(id => cluster.Riders.Contains(id) ? alternatives[id].Select(TryProject).ToArray() : new[] { TryProject(selected[id]) }).ToArray();
                for (var a = 0; a < buffers.Length; a++) for (var b = a + 1; b < buffers.Length; b++)
                    if (cluster.Riders.Contains(all[a]) || cluster.Riders.Contains(all[b]))
                        foreach (var left in buffers[a]) foreach (var right in buffers[b])
                            if (left is not null && right is not null) PairCheck(left,right);
                foreach (var joint in Joint(choices))
                {
                    combinations++; searched++;
                    Projection[] projections;
                    try { projections = all.Select(id => Project(cluster.Riders.Contains(id) ? joint[Array.IndexOf(cluster.Riders,id)] : selected[id])).ToArray(); }
                    catch (InvalidOperationException)
                    {
                        if (options.InteractionDiagnostics == InteractionDiagnosticsLevel.FullAudit)
                            candidates.Add(new(searched - 1, false, "No finite production traversal", 0, new(0, 0, 0, 0), joint));
                        continue;
                    }
                    var space = Verify(projections, cluster.Riders);
                    var minimum = space.MinimumSeparation;
                    var mechanical = space.Intervals.Count > 0;
                    var ambiguous = space.HasBoundaryAmbiguityOrCoverageGap;
                    var bounds = projections.Any(x => cluster.Riders.Contains(x.Alternative.RiderId) && !x.WithinTrack);
                    var failed = projections.Any(x => cluster.Riders.Contains(x.Alternative.RiderId) && !x.Traversal.CompletedHorizon);
                    var cost = new InteractionCost(joint.Sum(a => Project(a).Traversal.PredictedTraversalTimeSeconds
                            - baseline[a.RiderId].Traversal.PredictedTraversalTimeSeconds),
                        joint.Sum(a => ResponseDeviation(a, original[a.RiderId].Intent, snapshot) * .025),
                        joint.Sum(a => a.TacticalPreference),
                        joint.Sum(a => Math.Max(0, InteractionGeometryModel.ExecutionMarginMeters(snapshot.Rider(a.RiderId), p)
                            + .10 * (1 - snapshot.Rider(a.RiderId).Profile.Gameplay.InteractionStyle.Combativeness) - minimum)));
                    var feasible = !mechanical && !ambiguous && !bounds && !failed;
                    if (options.InteractionDiagnostics == InteractionDiagnosticsLevel.FullAudit) candidates.Add(new(searched - 1, feasible, mechanical ? "Mechanical overlap (#55)" : ambiguous ? "Ineligible boundary/coverage"
                        : bounds ? "Mechanical footprint outside usable track" : failed ? "Incomplete/crashed production horizon" : "", minimum, cost, joint,
                        space.IneligibleIntervals));
                    if (feasible && joint.All(a => episode.Commitments.TryGetValue(a.RiderId, out var previous)
                        && previous.Response == a.Response && previous.Intent == a.Intent && previous.DriveControl == a.DriveControl
                        && previous.HoldLateralPosition == a.HoldLateralPosition && previous.PhysicalTarget == a.PhysicalTarget)) retained = (joint, cost, minimum);
                    if (unresolvedSafety is null && !feasible && mechanical && joint.All(a => a.Response is InteractionResponse.BackOut or InteractionResponse.EmergencyAvoid)
                        && minimum >= cluster.Edges.Min(g => g.Space.MinimumSeparationMeters) - GeometryNumerics.MinimumSeparationToleranceMeters)
                        unresolvedSafety = (joint, cost, minimum);
                    if (feasible && (winner is null || Better(cost, joint, winner.Value.Cost, winner.Value.Responses, TacticalTie)))
                        winner = (joint, cost, minimum);
                }
            }
            var clusterPasses = 1;
            Search(); passes++;
            if (episode.Context == cluster.Context && episode.Commitments.Count > 0
                && !cluster.Edges.Any(e => e.Space.HasConflict && e.TimeToConflictSeconds <= p.EmergencyTimeSeconds))
            {
                if (retained is not null) winner = retained;
            }
            var chosen = winner?.Responses ?? unresolvedSafety?.Responses ?? cluster.Riders.Select(id => selected[id]).ToArray();
            if (winner is null && unresolvedSafety is not null)
            {
                var attempted = new Dictionary<int, InteractionAlternative>(selected);
                foreach (var a in chosen) attempted[a.RiderId] = a;
                var projected = attempted.Values.Select(Project).ToArray();
                var originalConflicts = forecast.Intervals.Where(r => Future(r) && r.EligibleForFutureInteraction).Select(r => Pair(r.RiderA,r.RiderB)).ToHashSet();
                if (projected.Any(x => !x.WithinTrack || !x.Traversal.CompletedHorizon)
                    || Verify(projected).Intervals.Any(r => Future(r) && r.EligibleForFutureInteraction && !originalConflicts.Contains(Pair(r.RiderA,r.RiderB))))
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
            foreach (var conflict in projectedFinal.Intervals.Where(r => Future(r) && r.EligibleForFutureInteraction
                && (cluster.Riders.Contains(r.RiderA) || cluster.Riders.Contains(r.RiderB)))
                .GroupBy(r => Pair(r.RiderA, r.RiderB)).Select(g => g.OrderBy(r => r.FirstTouchCommonTimeSeconds).First()))
                unresolved.Add(Unresolved(episode, conflict, chosen, "No clear bounded joint alternative"));
            diagnostics.Add(new(episode.Id, episode.Riders, cluster.Context, episode.Start, episode.End,
                cluster.Edges.Min(g => g.Space.MinimumSeparationMeters), cluster.Edges.Aggregate(SpaceConflictKind.None, (k, g) => k | g.Space.Kind),
                cluster.Edges, cluster.Riders.Select(id => original[id]).ToArray(), alternatives.Values.SelectMany(a => a).ToArray(), chosen,
                options.InteractionDiagnostics == InteractionDiagnosticsLevel.FullAudit ? candidates : Array.Empty<InteractionCandidateDiagnostic>(), clusterPasses, winner?.Minimum ?? cluster.Edges.Min(g => g.Space.MinimumSeparationMeters),
                winner is not null && unresolved.Count == 0, unresolved, false, episode.ResponseChanges, Math.Max(0, episode.LastActive - episode.Start)));
        }
        var safetyProjectionCount = 0;
        var retainFailedSafetyActual = false;
        RiderIntent[] SelectedIntents() => intents.Select(i => new RiderIntent(i.RiderId, i.Decision with
        {
            TargetLane = CurrentTarget(selected[i.RiderId].Intent, snapshot),
            Trajectory = selected[i.RiderId].Intent, DriveControl = selected[i.RiderId].DriveControl,
            HoldLateralPosition = selected[i.RiderId].HoldLateralPosition,
            InteractionTarget = selected[i.RiderId].PhysicalTarget,
        })).ToArray();
        var finalIntents = SelectedIntents();
        // Re-resolve actual current paths with the real addressed incident options.
        var actual = engine.ResolveProduction(snapshot, finalIntents, options, legacyContacts: false);
        production++;
        var actualPoses = Poses(actual);
        var verification = CommonTimePoseHistory.Observe(tracker.History.Concat(actualPoses));
        narrow += verification.Work.NarrowPhaseEvaluations;
        var safetyPasses = 0;
        var actualVerifications = 1;
        // Freeze the actual verified motions once. Every safety alternative starts at
        // the same original request boundary; never commit or advance a rider twice.
        var safetyEdges = Contacts(verification, tracker.History.Concat(actualPoses).ToArray(), requestTimes);
        // A single joint safety correction also protects against a new conflict
        // between two independently corrected subclusters in this production step.
        var safetyComponents = Clusters(safetyEdges, snapshot, p);
        ReconcileComponents(safetyComponents);
        var safetyJointCombinations = 0;
        if (safetyEdges.Length > 0)
        {
            var scope = new SafetyEvaluationScope(snapshot.Riders.Where(r => r.IsActive).Select(r => r.RiderId).Order().ToArray(), safetyEdges);
            var ids = scope.Riders;
            var frozen = new Dictionary<int, InteractionAlternative>(selected);
            var safetyPairs = new Dictionary<(ProjectionKey, ProjectionKey), PairAlternativeResult>();
            Projection ResolveSafetyProjection(InteractionAlternative alternative)
            {
                var key = Key(alternative);
                var rider = snapshot.Rider(alternative.RiderId);
                var solo = new SimulationSnapshot(snapshot.Step, snapshot.Track, snapshot.TrackState, new[] { rider });
                var decision = new RiderDecision(CurrentTarget(alternative.Intent, snapshot))
                { Trajectory = alternative.Intent, DriveControl = alternative.DriveControl, HoldLateralPosition = alternative.HoldLateralPosition,
                    InteractionTarget = alternative.PhysicalTarget };
                ResolvedRiderMotion motion;
                RiderStateChange change;
                if (key == Key(frozen[rider.RiderId]))
                {
                    ProjectionCaptureAudit.Record(ProjectionMaterialization.SafetyFrozenReuse);
                    motion = actual.Motions.Single(m => m.RiderId == rider.RiderId);
                    change = actual.Changes.Single(c => c.RiderId == rider.RiderId);
                }
                else
                {
                    // Count attempts even when the optional solver cannot materialize a motion.
                    production++;
                    ProjectionCaptureAudit.Record(ProjectionMaterialization.SafetyProductionAttempt);
                    var intent = new RiderIntent(rider.RiderId, decision);
                    var step = resolveOptionalSafety is null
                        ? engine.ResolveProduction(solo, new[] { intent }, options, legacyContacts: false)
                        : resolveOptionalSafety(solo, intent, options);
                    motion = step.Motions[0]; change = step.Changes[0];
                }
                var poses = MotionPoses(motion);
                var traversal = new TrajectoryTraversal(alternative.Intent, motion.TotalTimeSeconds,
                    motion.TotalDistanceMeters, null, null, null, null, null, null, null, 0, 0,
                    change.Status != RiderRaceStatus.Crashed, Array.Empty<TrajectoryPhaseEndpoint>(), new[] { motion });
                var boundsPoses=alternative.PhysicalTarget.HasValue?poses.Select(TargetPose):poses;
                var projected = new Projection(alternative, traversal, poses,
                    WithinTrack(boundsPoses, snapshot.Track, alternative.PhysicalTarget.HasValue?TargetEmbedding():tracker.Embedding!));
                return projected;
            }
            var safetyCache = new OptionalSafetyProjectionCache<Projection>(ResolveSafetyProjection);
            SafetyProjectionResult<Projection> SafetyProject(InteractionAlternative alternative) => safetyCache.Get(alternative);
            PairAlternativeResult SafetyPair(Projection left, Projection right)
            {
                var key = (Key(left.Alternative), Key(right.Alternative));
                if (safetyPairs.TryGetValue(key, out var cached)) return cached;
                var poses=tracker.History.Where(i => i.RiderId == key.Item1.RiderId || i.RiderId == key.Item2.RiderId)
                    .Concat(left.Poses).Concat(right.Poses);
                if(continuousTargetSearch) poses=poses.Select(TargetPose);
                var report = CommonTimePoseHistory.Compatibility(poses, Ready(key.Item1.RiderId, key.Item2.RiderId));
                pairChecks++; narrow += report.Work.NarrowPhaseEvaluations;
                var result = new PairAlternativeResult(report.EligibleIntervals == 0 ? p.CompetitiveReachMeters : report.MinimumSeparation,
                    report.EligibleIntervals == 0 || report.HasBoundaryAmbiguity || report.HasCoverageGap,
                    report.IneligibleIntervals, report.Contacts.ToArray());
                safetyPairs.Add(key, result); return result;
            }
            InteractionAlternative[] SafetyChoices(int id)
            {
                var keep = frozen[id] with { Response = InteractionResponse.KeepIntent, TacticalPreference = 0,
                    Reason = "Retain the verified request only if the joint safety result is certified clear" };
                var current = LaneModel.ClampLane((int)MathF.Round(snapshot.Rider(id).LateralPosition));
                var back = new InteractionAlternative(id, InteractionResponse.BackOut, new(current, current, current),
                    RiderDriveControl.LiftThrottle, 0, "Safety pass: current-step lateral hold and lift", true);
                var yield = diagnostics.SelectMany(d => d.ResponseAlternatives).FirstOrDefault(a => a.RiderId == id
                    && a.Response is InteractionResponse.YieldOutward or InteractionResponse.Hold);
                var emergency = yield is null ? back with { Response = InteractionResponse.EmergencyAvoid }
                    : yield with { Response = InteractionResponse.EmergencyAvoid, DriveControl = RiderDriveControl.LiftThrottle,
                        TacticalPreference = 0, Reason = "Safety pass: already available lateral yield with current-step lift" };
                if (emergency.PhysicalTarget is { } physical)
                {
                    var lower = physical.OffsetFromInnerReferenceMeters;
                    var upper = LaneModel.UsableRacingWidthMeters(snapshot.Segment.Type, snapshot.Track.Geometry);
                    var inside = scope.Contacts.Where(g => (g.RiderA == id || g.RiderB == id)
                        && (g.RiderA == id ? g.OutwardBFromAMeters : -g.OutwardBFromAMeters) < 0)
                        .Select(g => g.RiderA == id ? g.RiderB : g.RiderA).Distinct().Order().ToArray();
                    bool Clear(InteractionAlternative alternative)
                    {
                        outwardTargetTrials++;
                        var result = SafetyProject(alternative);
                        if (result.Value is not { WithinTrack: true } projection || !projection.Traversal.CompletedHorizon) return false;
                        foreach (var other in inside)
                        {
                            var opponent = SafetyProject(frozen[other]);
                            if (opponent.Value is null) return false;
                            var pair = id < other ? SafetyPair(projection, opponent.Value) : SafetyPair(opponent.Value, projection);
                            if (pair.Intervals.Count > 0 || pair.HasBoundaryAmbiguityOrCoverageGap) return false;
                        }
                        return true;
                    }
                    // Actual incidents can require more room than the incident-free
                    // forecast. Derive a metre bracket from actual #55 shortfall;
                    // retain one safety choice, never enlarge the joint product.
                    if (inside.Length > 0 && !Clear(emergency))
                    {
                        var shortfall = scope.Contacts.Where(g => g.RiderA == id || g.RiderB == id)
                            .Max(g => Math.Max(0, p.PressureClearanceMeters - g.Space.MinimumSeparationMeters));
                        var high = Math.Min(upper, lower + (float)shortfall);
                        var best = emergency with { PhysicalTarget = new(high) };
                        var clear = Clear(best);
                        if (!clear && high < upper) { high = upper; best = emergency with { PhysicalTarget = new(high) }; clear = Clear(best); }
                        if (clear)
                        {
                            for (var trial = 0; trial < OutwardTargetRefinements && high - lower > OutwardTargetToleranceMeters; trial++)
                            {
                                var middle = (lower + high) * .5f;
                                var candidate = emergency with { PhysicalTarget = new(middle) };
                                if (Clear(candidate)) { high = middle; best = candidate; } else lower = middle;
                            }
                            emergency = best;
                        }
                    }
                }
                return new[] { keep, back, emergency };
            }
            var choices = ids.Select(SafetyChoices).ToArray();
            var allIds = selected.Keys.Order().ToArray();
            // Project every unique rider request once, and certify every pair of
            // alternatives once before scoring any joint combination.
            foreach (var alternativesForRider in choices) foreach (var alternative in alternativesForRider) SafetyProject(alternative);
            var buffers = allIds.Select(id => ids.Contains(id) ? choices[Array.IndexOf(ids,id)] : new[] { frozen[id] }).ToArray();
            for (var a = 0; a < buffers.Length; a++) for (var b = a + 1; b < buffers.Length; b++)
                if (ids.Contains(allIds[a]) || ids.Contains(allIds[b]))
                    foreach (var left in buffers[a]) foreach (var right in buffers[b])
                        if (SafetyProject(left).Value is { } lp && SafetyProject(right).Value is { } rp) SafetyPair(lp, rp);
            InteractionAlternative[]? safetyWinner = null, emergencyAttempt = null;
            InteractionCost? bestCost = null, emergencyCost = null;
            var safetyCandidates = new List<InteractionCandidateDiagnostic>();
            var existingMechanicalPairs = safetyEdges.Select(g => Pair(g.RiderA,g.RiderB)).ToHashSet();
            bool SafetyBetter(InteractionCost cost, InteractionAlternative[] joint, InteractionCost priorCost, InteractionAlternative[] priorJoint)
            {
                int UnrelatedChanges(InteractionAlternative[] responses) => responses.Count(a =>
                    !scope.Contacts.Any(g => g.RiderA == a.RiderId || g.RiderB == a.RiderId) && Key(a) != Key(frozen[a.RiderId]));
                var changes = UnrelatedChanges(joint); var previousChanges = UnrelatedChanges(priorJoint);
                return changes != previousChanges ? changes < previousChanges : Better(cost,joint,priorCost,priorJoint,_ => 0);
            }
            var number = 0;
            foreach (var joint in Joint(choices))
            {
                combinations++; number++;
                var jointById = allIds.Select(id => ids.Contains(id) ? joint[Array.IndexOf(ids,id)] : frozen[id]).ToArray();
                var outcomes = jointById.Select(SafetyProject).ToArray();
                if (outcomes.Any(result => result.Outcome == SafetyProjectionOutcome.InfeasibleProductionProjection))
                {
                    if (options.InteractionDiagnostics == InteractionDiagnosticsLevel.FullAudit)
                        safetyCandidates.Add(new(number - 1, false,
                            "Infeasible optional production safety projection: " + string.Join(", ",
                                outcomes.Select((result, index) => (result, index)).Where(x => x.result.Value is null)
                                    .Select(x => $"rider {jointById[x.index].RiderId} ({x.result.FailureType})")),
                            null, null, joint));
                    continue;
                }
                var projected = outcomes.Select((result, index) => result.Value! with { Alternative = jointById[index] }).ToArray();
                var minimum = p.CompetitiveReachMeters; var mechanical = false; var ambiguous = false; var newMechanicalPair = false;
                for (var a = 0; a < jointById.Length; a++) for (var b = a + 1; b < jointById.Length; b++)
                    if (ids.Contains(allIds[a]) || ids.Contains(allIds[b]))
                    {
                        var pair = SafetyPair(projected[a], projected[b]);
                        minimum = Math.Min(minimum, pair.MinimumSeparation); mechanical |= pair.Intervals.Count > 0;
                        newMechanicalPair |= pair.Intervals.Count > 0 && !existingMechanicalPairs.Contains(Pair(allIds[a],allIds[b]));
                        ambiguous |= pair.HasBoundaryAmbiguityOrCoverageGap;
                    }
                var outside = projected.Any(a => !a.WithinTrack);
                var incomplete = projected.Any(a => !a.Traversal.CompletedHorizon);
                var physical = !outside && !incomplete;
                var feasible = physical && !mechanical && !ambiguous;
                var cost = new InteractionCost(projected.Where(a => ids.Contains(a.Alternative.RiderId)).Sum(a => a.Traversal.PredictedTraversalTimeSeconds), 0, 0,
                    joint.Sum(a => Math.Max(0, InteractionGeometryModel.ExecutionMarginMeters(snapshot.Rider(a.RiderId), p) - minimum)));
                if (options.InteractionDiagnostics == InteractionDiagnosticsLevel.FullAudit)
                    safetyCandidates.Add(new(number - 1, feasible, mechanical ? "Mechanical overlap (#55)" : ambiguous ? "Ineligible boundary/coverage"
                        : outside ? "Mechanical footprint outside usable track" : incomplete ? "Incomplete/crashed production horizon" : "", minimum, cost, joint));
                if (feasible && (bestCost is null || SafetyBetter(cost, joint, bestCost, safetyWinner!)))
                { safetyWinner = joint; bestCost = cost; }
                if (physical && !ambiguous && !newMechanicalPair
                    && joint.Where(a => scope.Contacts.Any(g => g.RiderA == a.RiderId || g.RiderB == a.RiderId))
                        .All(a => a.Response == InteractionResponse.EmergencyAvoid)
                    && minimum >= scope.Contacts.Min(g => g.Space.MinimumSeparationMeters) - GeometryNumerics.MinimumSeparationToleranceMeters
                    && (emergencyCost is null || SafetyBetter(cost,joint,emergencyCost,emergencyAttempt!)))
                { emergencyAttempt = joint; emergencyCost = cost; }
            }
            var chosen = safetyWinner ?? emergencyAttempt ?? ids.Select(id => frozen[id]).ToArray();
            // No Commit has occurred. Exact frozen requests already have successful actual motions,
            // addressed incidents and #55 verification; retain those when failed optional corrections
            // leave no replacement. This preserves contacts, wear and consequence ownership.
            retainFailedSafetyActual = safetyCache.FailedCount > 0 && safetyWinner is null && emergencyAttempt is null
                && chosen.All(a => Key(a) == Key(frozen[a.RiderId]));
            foreach (var choice in chosen)
            {
                selected[choice.RiderId] = choice;
            }
            foreach (var episode in tracker.Active.Where(e => e.Riders.Any(ids.Contains)))
            {
                var diagnosticIndex = diagnostics.FindIndex(d => d.EpisodeId == episode.Id);
                if (diagnosticIndex < 0) continue;
                var prior = diagnostics[diagnosticIndex];
                var ownedChoices = chosen.Where(a => episode.Riders.Contains(a.RiderId)).ToArray();
                foreach (var choice in ownedChoices)
                {
                    if (episode.Commitments.TryGetValue(choice.RiderId,out var previous) && previous.Response != choice.Response) episode.ResponseChanges++;
                    episode.Commitments[choice.RiderId] = choice;
                }
                diagnostics[diagnosticIndex] = prior with
                {
                    SelectedResponses = ownedChoices, PassCount = 2, ResponseChanges = episode.ResponseChanges,
                    Candidates = options.InteractionDiagnostics == InteractionDiagnosticsLevel.FullAudit
                        ? prior.Candidates.Concat(safetyCandidates.Select(c => c with
                            { Responses = c.Responses.Where(a => episode.Riders.Contains(a.RiderId)).ToArray() })).ToArray() : prior.Candidates,
                    Pass1ActualMechanicalContact = safetyEdges.Any(g => episode.Riders.Contains(g.RiderA) && episode.Riders.Contains(g.RiderB)),
                    Pass1SelectedResponses = prior.SelectedResponses,
                };
            }
            safetyPasses++; passes++;
            safetyJointCombinations = number;
            safetyProjectionCount += safetyCache.Count;
        }
        if (safetyPasses > 0 && !retainFailedSafetyActual)
        {
            finalIntents = SelectedIntents();
            // This is required execution, not an optional projection. Any failure remains fatal.
            actual = engine.ResolveProduction(snapshot, finalIntents, options, legacyContacts: false);
            production++; actualVerifications++;
            actualPoses = Poses(actual);
            verification = CommonTimePoseHistory.Observe(tracker.History.Concat(actualPoses));
            narrow += verification.Work.NarrowPhaseEvaluations;
        }
        var finalContactPoses = tracker.History.Concat(actualPoses).ToArray();
        var finalEdges = Contacts(verification, finalContactPoses, requestTimes);
        var finalComponents = Clusters(finalEdges, snapshot, p);
        ReconcileComponents(finalComponents);
        if (safetyPasses > 0)
            foreach (var episode in tracker.Active)
            {
                var index = diagnostics.FindIndex(d => d.EpisodeId == episode.Id);
                if (index < 0) continue;
                diagnostics[index] = diagnostics[index] with { PassCount = 2 };
                foreach (var id in episode.Riders.Where(selected.ContainsKey)) episode.Commitments[id] = selected[id];
            }
        // Only final actual #55 contact, after the single safety correction, can
        // authorize an episode's one legacy fallback attempt.
        var currentContactDiagnostics = options.EnablePhysicalContactConsequences ? new List<InteractionEpisodeDiagnostic>() : null;
        foreach (var episode in tracker.Active)
        {
            var index = diagnostics.FindIndex(d => d.EpisodeId == episode.Id);
            if (index < 0) continue;
            var d = diagnostics[index];
            var rows = verification.Intervals.Where(r => Future(r) && episode.Riders.Contains(r.RiderA) && episode.Riders.Contains(r.RiderB)).ToArray();
            var contacts = rows.Where(r => r.EligibleForFutureInteraction).GroupBy(r => Pair(r.RiderA, r.RiderB))
                .Select(g => g.OrderBy(r => r.FirstTouchCommonTimeSeconds).First()).ToArray();
            var boundaryOrGap = rows.Any(r => r.Kind.HasFlag(SpaceConflictKind.BoundaryAmbiguous))
                || verification.FrameCoverageGaps.Any(g => FutureGap(g) && (episode.Riders.Contains(g.RiderA) || episode.Riders.Contains(g.RiderB)));
            // #55's horizon certificate also covers an exactly equal current
            // production prefix. A short observation can lack minimum precision
            // despite that certificate; actual changed/ambiguous paths cannot use it.
            var certifiedReplayPrefix = d.PassCount == 1 && d.ResolvedWithoutMechanicalContact && !boundaryOrGap
                && episode.Riders.Where(id => selected.ContainsKey(id)).All(id =>
                    Project(selected[id]).Traversal.ResolvedMotions[0].Equals(actual.Motions.Single(m => m.RiderId == id)));
            var authorized = options.EnablePhysicalContactConsequences
                ? contacts.Select(c => new FallbackDecision(c, false, "Physical consequence ownership; legacy fallback disabled")).ToArray()
                : tracker.AuthorizeFallbacks(episode, contacts, safetyPasses == 1);
            foreach (var decision in authorized.Where(a => a.Authorized))
                fallbackPairs.Add(Pair(decision.Contact.RiderA,decision.Contact.RiderB));
            diagnostics[index] = d with
            {
                WithinCompetitiveReach = rows.Any(r => Eligible(r) && r.MinimumSeparationMeters <= p.CompetitiveReachMeters),
                CompetitiveReachCoverageCertified = rows.Any(Eligible) && rows.All(Eligible)
                    && !verification.FrameCoverageGaps.Any(g => FutureGap(g) && (episode.Riders.Contains(g.RiderA) || episode.Riders.Contains(g.RiderB))),
                MergedOtherRiders = episode.MergedOtherRiders,
                MergedEpisodeIds = episode.MergedEpisodeIds.ToArray(),
                FallbackProvenance = episode.FallbackOrigins.Values.OrderBy(o => o.OriginEpisodeId).ToArray(),
                FinalMinimumSeparationMeters = rows.Where(Eligible).Select(r => r.MinimumSeparationMeters).DefaultIfEmpty(d.FinalMinimumSeparationMeters).Min(),
                ResolvedWithoutMechanicalContact = contacts.Length == 0 && !boundaryOrGap
                    && (rows.Any(Eligible) && rows.All(Eligible) || certifiedReplayPrefix),
                ActualClearanceCertifiedByReplay = certifiedReplayPrefix,
                UnresolvedMechanicalContacts = authorized.Select(c => Unresolved(episode,c.Contact,d.SelectedResponses,
                    retainFailedSafetyActual ? "No feasible production safety correction; retaining previously executed actual motion. " + c.Reason : c.Reason)
                    with { LegacyFallbackAuthorized = c.Authorized }).ToArray(),
            };
            currentContactDiagnostics?.Add(diagnostics[index]);
        }
        foreach (var pair in fallbackPairs)
        {
            var index = diagnostics.FindIndex(d => d.RiderIds.Contains(pair.Item1) && d.RiderIds.Contains(pair.Item2));
            if (index >= 0) diagnostics[index] = diagnostics[index] with { LegacyFallbackUsed = true };
        }
        foreach (var episode in tracker.Active)
            episode.LastDiagnostic = diagnostics.LastOrDefault(d => d.EpisodeId == episode.Id) ?? episode.LastDiagnostic;
        PhysicalContactAnalysis? physicalContact = null;
        PhysicalContactConsequencePlan? consequencePlan = null;
        if (options.EnablePhysicalContactConsequences)
        {
            var physicalObservation = new PhysicalPairObservation(verification, p);
            bool Fresh(PhysicalContactPairAnalysis pair) => tracker.CanApplyPhysical(pair, physicalObservation);
            bool Applicable(PhysicalContactPairAnalysis pair) => safetyPasses == 1 && Fresh(pair);
            physicalContact = PhysicalContactSnapshotAdapter.Analyze(verification, finalContactPoses, snapshot,
                tracker.Embedding!, currentContactDiagnostics!, Array.Empty<SimulationStepEvent>(), options.PhysicalContactParameters,
                PhysicalContactDiagnosticsLevel.FullAudit, Applicable);
            var pairs = physicalContact.AuditPairs.Where(Applicable).ToArray();
            var directions = new Dictionary<int, MeterPoint>();
            foreach (var rider in physicalContact.ApplicationRiders)
            {
                var first = pairs.Where(p => p.RiderA == rider.RiderId || p.RiderB == rider.RiderId).Min(p => p.FirstTouchCommonTimeSeconds);
                var pose = CommonTimePoseHistory.Stitch(finalContactPoses).Where(i => i.RiderId == rider.RiderId
                    && i.StartTimeSeconds <= first && i.EndTimeSeconds >= first).OrderByDescending(i => i.StartTimeSeconds).First().Sample(first);
                directions[rider.RiderId] = ContactFrameArithmetic.Direction(pose.Attitude.TravelHeadingRadians, pose.DeterministicArithmetic);
            }
            consequencePlan = PhysicalContactConsequenceResolver.Build(physicalContact, physicalContact.ApplicationRiders,
                pairs, snapshot, actual.Changes, directions, options.PhysicalContactParameters,
                options.PhysicalContactConsequenceParameters,
                physicalContact.AuditPairs.Count(p => p.Status == PhysicalContactStatus.Analyzed && !Fresh(p)));
            tracker.ConsumePhysical(consequencePlan, physicalObservation);
        }
        // Reuse the verified step unless fallback or an applied consequence changes its output.
        var reuseActual = fallbackPairs.Count == 0 && (consequencePlan is null || consequencePlan.Riders.Count == 0);
        var final = reuseActual ? actual : engine.ResolveProduction(snapshot, finalIntents, options, legacyContacts: !options.EnablePhysicalContactConsequences,
            consequencePlan: consequencePlan, contactFilter: (a, b) =>
            fallbackPairs.Contains(Pair(a, b)), unresolvedPairs: fallbackPairs.Select(pair =>
                actual.Changes.Single(c => c.RiderId == pair.Item1).ElapsedTimeSeconds
                    <= actual.Changes.Single(c => c.RiderId == pair.Item2).ElapsedTimeSeconds
                    ? (pair.Item1, pair.Item2) : (pair.Item2, pair.Item1)).ToArray());
        if (!ReferenceEquals(final, actual)) production++;
        var finalPoses = ReferenceEquals(final, actual) ? actualPoses : Poses(final);
        var executedVerification = ReferenceEquals(final, actual) ? verification
            : CommonTimePoseHistory.Observe(tracker.History.Concat(finalPoses));
        physicalContact ??= options.PhysicalContactDiagnostics == PhysicalContactDiagnosticsLevel.None ? null
            : PhysicalContactSnapshotAdapter.Analyze(verification,finalContactPoses,snapshot,tracker.Embedding!,diagnostics,
                final.Events,options.PhysicalContactParameters,options.PhysicalContactDiagnostics);
        if (!ReferenceEquals(final, actual)) narrow+=executedVerification.Work.NarrowPhaseEvaluations;
        tracker.ObserveClearance(now,executedVerification.Intervals,p,snapshot,executedVerification);
        foreach(var closed in tracker.Closed)
        {
            var index=diagnostics.FindIndex(d=>d.EpisodeId==closed.EpisodeId);
            if(index>=0) diagnostics[index]=diagnostics[index] with {EndTimeSeconds=closed.EndTimeSeconds,ActiveDurationSeconds=closed.ActiveDurationSeconds};
            else diagnostics.Add(closed);
        }
        foreach(var episode in tracker.Active)
        {
            var index=diagnostics.FindIndex(d=>d.EpisodeId==episode.Id);
            if(index>=0) diagnostics[index]=diagnostics[index] with {ActiveDurationSeconds=Math.Max(0,episode.LastActive-episode.Start)};
            episode.LastDiagnostic=diagnostics.LastOrDefault(d=>d.EpisodeId==episode.Id)??episode.LastDiagnostic;
        }
        tracker.RetainVerified(final, finalPoses, executedVerification);
        return final.withInteraction(new(options.InteractionDiagnostics == InteractionDiagnosticsLevel.None ? Array.Empty<InteractionEpisodeDiagnostic>()
                : diagnostics.OrderBy(d => d.EpisodeId).ToArray(),
            new(combinations, production, narrow, passes, clusters.Count)
            {
                UniqueRiderAlternativeProjections = cache.Count + failedProjections.Count + safetyProjectionCount, PairAlternativeChecks = pairChecks,
                ActualProductionVerifications = actualVerifications + (ReferenceEquals(final, actual) ? 0 : 1), SafetyPasses = safetyPasses, LegacyFallbackAttempts = fallbackPairs.Count,
                SafetyContactComponents = safetyComponents.Count, FinalContactComponents = finalComponents.Count,
                SafetyJointCombinations = safetyJointCombinations,
                OutwardTargetTrials = outwardTargetTrials,
            }) { PhysicalContactAnalysis = options.PhysicalContactDiagnostics == PhysicalContactDiagnosticsLevel.None ? null
                : options.PhysicalContactDiagnostics == PhysicalContactDiagnosticsLevel.Summary && physicalContact is not null
                    ? physicalContact with { Level = PhysicalContactDiagnosticsLevel.Summary, AuditPairs = Array.Empty<PhysicalContactPairAnalysis>(), AuditRiders = Array.Empty<RiderContactAnalysis>() }
                    : physicalContact, PhysicalContactConsequences = consequencePlan }, owner, tracker);
    }

    private static bool Eligible(ContestedSpaceEvent r) => r.NumericallyResolved && !r.Kind.HasFlag(SpaceConflictKind.BoundaryAmbiguous);
    private static (int, int) Pair(int a, int b) => (Math.Min(a, b), Math.Max(a, b));
    private static InteractionAlternative Original(RiderIntent i) => new(i.RiderId, InteractionResponse.KeepIntent,
        i.Decision.Trajectory ?? new(i.Decision.TargetLane, i.Decision.TargetLane, i.Decision.TargetLane), null, 0, "Independent #54 intent");
    private static int CurrentTarget(TrajectoryIntent intent, SimulationSnapshot snapshot)
        => intent.TargetFor(snapshot.Segment.Type);
    private static double Deviation(TrajectoryIntent a, TrajectoryIntent b)
        => Math.Abs(a.EntryTarget - b.EntryTarget) + Math.Abs(a.ApexTarget - b.ApexTarget) + Math.Abs(a.ExitTarget - b.ExitTarget);
    private static double ResponseDeviation(InteractionAlternative a, TrajectoryIntent b, SimulationSnapshot snapshot)
    {
        if (a.PhysicalTarget is not { } target) return Deviation(a.Intent, b);
        var position = target.Position(snapshot.Segment.Type, snapshot.Track.Geometry);
        return Math.Abs(position - b.EntryTarget) + Math.Abs(position - b.ApexTarget) + Math.Abs(position - b.ExitTarget);
    }
    internal const int OutwardTargetRefinements = 8;
    internal const float OutwardTargetToleranceMeters = .02f;
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
    private static IEnumerable<InteractionAlternative[]> Joint(InteractionAlternative[][] choices)
    {
        var count = 1;
        foreach (var choice in choices) count *= choice.Length;
        for (var number = 0; number < count; number++)
        {
            var joint = new InteractionAlternative[choices.Length]; var index = number;
            for (var rider = 0; rider < choices.Length; rider++)
            { joint[rider] = choices[rider][index % choices[rider].Length]; index /= choices[rider].Length; }
            yield return joint;
        }
    }
    internal static bool WithinTrack(IEnumerable<PhysicalPoseInterval> poses, Track track, TrackMetricEmbedding embedding)
    {
        foreach (var interval in poses)
        {
            if (interval.DeterministicArithmetic != embedding.DeterministicArithmetic
                || embedding.DeterministicArithmetic && interval.FrameId != embedding.FrameId)
                throw new ArgumentException("Clearance poses must use the embedding's arithmetic mode and geometry frame.");
            if (!Certified(interval, interval.StartTimeSeconds, interval.EndTimeSeconds, 0)) return false;
        }
        return true;

        bool Certified(PhysicalPoseInterval interval, double start, double end, int depth)
        {
            var middle = (start + end) / 2;
            var clearance = Math.Min(Clearance(interval, interval.SampleValue(start)),
                Math.Min(Clearance(interval, interval.SampleValue(middle)), Clearance(interval, interval.SampleValue(end))));
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
        double Clearance(PhysicalPoseInterval interval, BikePoseValue pose)
        {
            var source = interval.Source!;
            var tangent = pose.ReferenceTangentHeadingRadians;
            MeterPoint outward;
            if (embedding.DeterministicArithmetic)
            {
                var forward = ContactFrameArithmetic.Direction(tangent, true);
                outward = new(forward.Y, -forward.X);
            }
            else outward = new(Math.Sin(tangent), -Math.Cos(tangent));
            var projected = InteractionGeometryModel.Project(pose.Footprint, outward);
            var center = MeterPoint.Dot(pose.Position, outward);
            var span = LaneModel.UsableRacingWidthMeters(source.SegmentType, track.Geometry);
            // Find physical offset from the exact common #55 embedding, not requested Lane.
            var segment = embedding.Segments[source.SegmentIndex];
            var offset = source.SegmentType == SegmentType.Straight
                ? MeterPoint.Dot(pose.Position - segment.StartReferencePosition, outward)
                : (pose.Position - (embedding.DeterministicArithmetic
                    ? embedding.DeterministicCornerCentre(source.SegmentIndex)
                    : segment.StartReferencePosition + new MeterPoint(-Math.Sin(segment.StartTangentHeadingRadians),
                        Math.Cos(segment.StartTangentHeadingRadians)) * segment.InnerRadiusMeters)).Length - track.Geometry.InnerRadiusMeters;
            var radialBulge = source.SegmentType == SegmentType.Straight ? 0
                : pose.Dimensions.BoundingRadiusMeters * pose.Dimensions.BoundingRadiusMeters
                    / (2 * Math.Max(1, track.Geometry.InnerRadiusMeters - pose.Dimensions.BoundingRadiusMeters));
            return Math.Min(offset + projected.Min - center + TrackGeometry.InnerReferenceOffsetFromTrackEdgeMeters,
                span + TrackGeometry.ProvisionalOuterReferenceOffsetFromTrackEdgeMeters - offset - projected.Max + center - radialBulge);
        }
    }
    internal static InteractionGeometry[] Threats(ContestedSpaceReport report, PhysicalPoseInterval[] poses,
        double now, ContestedSpaceParameters p,IReadOnlyDictionary<int,double>? requestTimes=null)
    {
        var edges = new List<InteractionGeometry>();
        foreach (var pair in report.Intervals.Where(r => r.IntervalEndSeconds > now && Eligible(r)
            && r.MinimumSeparationMeters <= p.CompetitiveReachMeters).GroupBy(r => Pair(r.RiderA, r.RiderB)))
        {
            var pairNow=requestTimes is null?now:Math.Min(requestTimes[pair.Key.Item1],requestTimes[pair.Key.Item2]);
            var future=pair.Where(r=>r.IntervalEndSeconds>pairNow).ToArray();
            if (future.Length==0) continue;
            var row = future.OrderBy(r => r.MinimumSeparationMeters).First();
            var common = report.Intervals.Where(r => Pair(r.RiderA, r.RiderB) == pair.Key && r.IntervalEndSeconds > pairNow && Eligible(r))
                .OrderBy(r => r.IntervalStartSeconds).First();
            var time = Math.Max(pairNow, common.IntervalStartSeconds);
            var a = poses.Where(i => i.RiderId == row.RiderA && i.StartTimeSeconds <= time && i.EndTimeSeconds > time)
                .OrderByDescending(i => i.StartTimeSeconds).FirstOrDefault();
            var b = poses.Where(i => i.RiderId == row.RiderB && i.StartTimeSeconds <= time && i.EndTimeSeconds > time)
                .OrderByDescending(i => i.StartTimeSeconds).FirstOrDefault();
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
    internal static InteractionGeometry[] Contacts(ContestedSpaceReport report, PhysicalPoseInterval[] poses,
        IReadOnlyDictionary<int,double> requestTimes)
    {
        var edges = new List<InteractionGeometry>();
        foreach (var pair in report.Intervals.Where(r => r.EligibleForFutureInteraction
                && r.IntervalEndSeconds > Math.Min(requestTimes[r.RiderA],requestTimes[r.RiderB]))
            .GroupBy(r => Pair(r.RiderA,r.RiderB)).OrderBy(g => g.Key))
        {
            var row = pair.OrderBy(r => r.FirstTouchCommonTimeSeconds).ThenBy(r => r.IntervalStartSeconds).First();
            var time = Math.Max(Math.Min(requestTimes[row.RiderA],requestTimes[row.RiderB]),row.FirstTouchCommonTimeSeconds!.Value);
            PhysicalPoseInterval At(int id) => poses.Where(i => i.RiderId == id && i.StartTimeSeconds <= time && i.EndTimeSeconds >= time)
                .OrderByDescending(i => i.StartTimeSeconds).First();
            // #55's eligible interval proves common-frame coverage at touch. Use
            // that actual common time, not the earlier forecast/request boundary.
            var a = At(row.RiderA); var b = At(row.RiderB);
            edges.Add(InteractionGeometryModel.Describe(row,a.Sample(time),b.Sample(time),a.RateBounds(time,time),b.RateBounds(time,time)));
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
        InteractionAlternative original, Cluster cluster, InteractionEpisode episode, ContestedSpaceParameters p,
        Func<InteractionAlternative, InteractionGeometry[], InteractionAlternative> minimalOutward)
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
            plan = new(current, current, current); }
        else { plan = new(current, current, original.Intent.ExitTarget); }
        var domain = InteractionGeometryModel.SkillDomain(response, InteractionGeometryModel.Role(rider.RiderId, nearest));
        var quality = domain switch
        {
            InteractionSkillDomain.Offensive => abilities.Attack / 99d,
            InteractionSkillDomain.Defensive => abilities.Defense / 99d,
            _ => 0d,
        };
        const double maneuverQualityWeight = .09;
        const double contestWillingnessWeight = .09;
        var tactical = -maneuverQualityWeight * quality
            - (domain == InteractionSkillDomain.Safety ? 0 : contestWillingnessWeight * style.Combativeness);
        // PreferredLine is a small tie preference, not a physical bonus.
        tactical += style.PreferredLine == PreferredLine.Inside ? .002 * plan.ExitTarget
            : style.PreferredLine == PreferredLine.Outside ? .002 * (4 - plan.ExitTarget) : 0;
        var local = new InteractionAlternative(rider.RiderId, response, plan, null, tactical, establishedInside
            ? "Established footprint overlap forbids covering through the inside rider" : "Local phase response; production feasibility required",
            response == InteractionResponse.Hold);
        yield return retainedCommitment ?? (response is InteractionResponse.YieldOutward or InteractionResponse.ContinueOutside
            ? minimalOutward(local, related) : local);
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
