using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using CoreSim.Decisions;
using CoreSim.Logging;
using CoreSim.Race;
using Intent = CoreSim.Decisions.TrajectoryIntent;

namespace CoreSim.Analysis;

public sealed record TrajectoryFixedControl(string Scenario, int Lane, float HistoricalStaticProjectionSeconds,
    TrajectoryTraversal Projection, double IndependentProductionTimeSeconds);
public sealed record TrajectoryChoiceControl(string Scenario, Intent SelectedIntent, int CurrentTarget,
    int CandidateTraversals, int ProductionResolutions, TrajectoryIntentEvaluation Selected);
public sealed record TrajectoryGateControl(StartingGate Gate, float GateCenterLateralPosition,
    Intent SelectedIntent, float ReactionTimeSeconds, RiderMotionSample DuringReaction,
    TrajectoryTraversal Traversal);
public sealed record TrajectoryIntentEvidenceData(string BaseHead,
    IReadOnlyList<TrajectoryFixedControl> FixedControls, IReadOnlyList<TrajectoryChoiceControl> Choices,
    IReadOnlyList<TrajectoryTraversal> MovingExamples, IReadOnlyList<TrajectoryGateControl> StandingStarts);

/// <summary>Current solo evidence, separate from immutable #39–#53 reports.</summary>
public static class TrajectoryIntentEvidence
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true, Converters = { new JsonStringEnumConverter() },
        NumberHandling = JsonNumberHandling.AllowNamedFloatingPointLiterals,
    };

    public static TrajectoryIntentEvidenceData Run()
    {
        var scenarios = FourRiderBehaviorSuite.CreateScenarios();
        var fixedControls = new List<TrajectoryFixedControl>();
        foreach (var id in new[] { "B", "C", "H" })
        {
            var source = scenarios.Single(s => s.Id == id);
            var track = id == "H" ? source.Track : CornerFirst(source.Track.Geometry);
            var focal = source.Riders.Single(r => r.Id == 2);
            for (var lane = 0; lane <= 4; lane++)
            {
                var rider = (focal with { Lane = lane, SegmentProgress = 0f, ArrivalOffsetSeconds = 0f }).Create(track);
                var state = new TrackState(track.Segments.Count, 5, (_, l) => source.LaneSurfaces[l]);
                var context = Context(track, state, rider);
                var intent = new Intent(lane, lane, lane);
                var predicted = new TrajectoryEvaluator(context).Evaluate(intent);
                var indices = id == "H" ? new[] { 0, 1 } : new[] { 0, 1, 2, 3 };
                var actual = ReplayFixed(context, lane, indices);
                // Historical analysis transcription only; never consulted by AdaptiveDecisionModel.
                var safe = SegmentPhysics.MaxSafeTurnSpeed(lane, track.Geometry, source.LaneSurfaces[lane], focal.Skills, rider.ActiveSetup);
                var old = (LaneModel.TurnArcLengthMeters(lane, track.Geometry) * 3f
                    + track.Geometry.StraightLengthMeters) / MathF.Max(safe, 1f);
                fixedControls.Add(new(id, lane, old, predicted, actual));
            }
        }
        var choices = new List<TrajectoryChoiceControl>();
        var model = new AdaptiveDecisionModel();
        foreach (var id in new[] { "B", "C", "H", "F", "G", "J-tight", "J-wide" })
        {
            var source = scenarios.Single(s => s.Id == id); var rider = source.Riders.Single(r => r.Id == 2).Create(source.Track);
            var result = model.Evaluate(Context(source.Track, source.CreateSurface(), rider));
            choices.Add(new(id, result.Selected.Intent, result.Decision.TargetLane,
                result.CandidateTraversals, result.ProductionResolutions, result.Selected));
        }
        var motoarena = MatchedVenueProfiles.CreateMotoarenaStandingStartTrack();
        var movingRider = new RiderState(RiderProfile.CreateDefault(17), 3) { Speed = 22f };
        movingRider.RestorePosition(RiderPosition.Create(1, 1, 0f, motoarena.Segments.Count));
        var movingContext = Context(motoarena, TrackState.CreateDefault(motoarena, new(1f, 0f, .35f)), movingRider);
        var evaluator = new TrajectoryEvaluator(movingContext);
        var moving = new[] { new Intent(3, 2, 3), new(3, 3, 3), new(0, 1, 2), new(4, 3, 2) }
            .Select(i => evaluator.Evaluate(i)).ToArray();
        var gates = new List<TrajectoryGateControl>();
        foreach (var gate in new[] { StartingGate.A, StartingGate.D })
        {
            var rider = new RiderState(RiderProfile.CreateDefault(17), gate, motoarena);
            var context = Context(motoarena, TrackState.CreateDefault(motoarena, new(1f, 0f, .35f)), rider);
            var choice = model.Evaluate(context).Selected.Intent;
            var traversal = new TrajectoryEvaluator(context, model.PerceivedTrackState(context)).Evaluate(choice, true);
            var launch = traversal.ResolvedMotions[0];
            gates.Add(new(gate, rider.LateralPosition, choice, launch.ReactionTimeSeconds,
                launch.SampleAtTime(launch.ReactionTimeSeconds / 2f), traversal with { ResolvedMotions = Array.Empty<ResolvedRiderMotion>() }));
        }
        return new("3f4caf599f3e0539ab0c39f058b98d80b9798f54", fixedControls, choices, moving, gates);
    }

    public static string Json(TrajectoryIntentEvidenceData evidence)
        => JsonSerializer.Serialize(evidence, JsonOptions).Replace("\r\n", "\n", StringComparison.Ordinal) + "\n";

    public static string Markdown(TrajectoryIntentEvidenceData evidence)
    {
        var text = new StringBuilder();
        void Line(string value = "") => text.Append(value).Append('\n');
        string N(double value) => value.ToString("F6", CultureInfo.InvariantCulture);
        string Nullable(float? value) => value.HasValue ? N(value.Value) : "unobserved";
        Line("# Production-backed trajectory intent evaluation"); Line();
        Line($"Audited main: `{evidence.BaseHead}` (squash #53). Historical #39–#53 evidence is unchanged. This is current solo evidence, not a new physical calibration."); Line();
        Line("## Architecture and semantics"); Line();
        Line("Before: AdaptiveDecisionModel ranked one anchor with a static route-length / perceived settled-safe-speed ratio. Current speed, achievable path, correction/carry/drive and straight payoff were absent. After: bounded Entry/Apex/Exit intents replay exact current rider and raw-cell surface copies through the ordinary SimulationEngine CaptureSnapshot → Decide → Resolve → Commit. Scripted targets prevent recursion. Elapsed time and distance are sums of canonical ResolvedRiderMotion totals, including heat-clock float reconciliation. Endpoints, speeds, outcomes and anchor arrival are read from actual production resolutions. No prediction physics or static fallback exists."); Line();
        Line("Entry applies on every pre-corner Straight piece and TurnEntry. Apex is the TurnMiddle intent, not a guaranteed position at CornerProgress 0.5. Exit applies on TurnExit and is held on all pieces of the immediately following logical straight. Horizon uses CornerTopology membership plus topological lap progress; it stops before a second logical corner or at race finish. The split Motoarena home straight crosses lap wrap correctly without segment-id special cases. A straight-only track continues to finish; absent future/past phases have null observations. There is no persistent RiderState plan and only the current phase target executes."); Line();
        Line("Grammar: Entry 0..4, Apex in clamped/deduplicated Entry±1, Exit in Apex±1: exactly 35 full candidates, 13 remaining Middle/Exit, 5 Exit candidates. All five holds and 3/2/3, 0/1/2, 4/3/2 occur. Completed phases collapse to canonical tuple placeholders; they are never claimed as achieved history. Anchors constrain planning, not continuous movement."); Line();
        Line("Each candidate gets fresh mutable rider and surface copies, a private disabled log and IncidentFrequency=0. Deterministic braking, RunWide, inability to reach targets and terminal crash remain active. Incomplete deterministic crash routes retain measured partial time/distance but have infinite selection cost. Own passage wear is committed privately; future weather/other riders' passages are not forecast. Equal scripted prefixes reuse immutable ResolvedSimulationStep results within one evaluator only; cold, repeated and reversed replay parity tests prove identical paths/outcomes/winners. There is no global mutable cache. Immutable Track/topology can be shared safely."); Line();
        Line("Perceived raw Grip/Ruts/Moisture cells use existing TrackObservation with seed, heat, decision step, rider, segment index, lane and model seed. Segment index is newly added to addressing; noise amplitude 0.09 and ruts factor −0.5 are unchanged, moisture remains observed unchanged. Perfect reading removes all noise. Normal production interpolation follows over those isolated raw cells. Every candidate sees the same observations."); Line();
        Line("Total cost = measured production time + style + behavioral lane-change reluctance + provisional current-target occupancy + existing surface/risk preference. Style, occupancy and risk coefficients remain 0.035, 0.30, 0.06/0.12/0.08. Style/risk/occupancy judge the current target (risk samples the upcoming corner when present); behavioral demand sums remaining requested phase changes at existing 0.025 × (1−LaneChangeTendency). The old independent 0.015 movement surcharge is removed because movement/path distance already costs physical time. OutsidePreference supplies no speed bonus. Selection orders total cost, physical time, requested change, then canonical E/A/X; enumeration order has no role."); Line();
        Line("## Fixed controls B/C/H"); Line();
        Line("Same rider/setup/surface/22 or 19 m/s entry within each group; fixed lanes start at their own anchor as in #51 C. Each row independently runs the normal production engine for the same corner and following straight, with private wear and no weather change/incidents/traffic. The historical static formula is analysis-only; H's coarse single TurnEntry bend deliberately retains its historical topology difference."); Line();
        Line("| control | lane | old static s | production s | independent s | distance m | corner exit m/s | following straight s | straight end m/s |");
        Line("| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |");
        foreach (var row in evidence.FixedControls)
            Line($"| {row.Scenario} | {row.Lane} | {N(row.HistoricalStaticProjectionSeconds)} | {N(row.Projection.PredictedTraversalTimeSeconds)} | {N(row.IndependentProductionTimeSeconds)} | {N(row.Projection.PhysicalDistanceMeters)} | {Nullable(row.Projection.CornerExitSpeedMetersPerSecond)} | {N(row.Projection.FollowingStraightTimeSeconds)} | {Nullable(row.Projection.FollowingStraightEndSpeedMetersPerSecond)} |");
        Line();
        var c = evidence.FixedControls.Where(r => r.Scenario == "C").ToArray();
        Line($"C old rank: {string.Join(" → ", c.OrderBy(r => r.HistoricalStaticProjectionSeconds).Select(r => r.Lane))}. Production-backed rank: {string.Join(" → ", c.OrderBy(r => r.Projection.PredictedTraversalTimeSeconds).Select(r => r.Lane))}. Independent actual rank: {string.Join(" → ", c.OrderBy(r => r.IndependentProductionTimeSeconds).Select(r => r.Lane))}. Rankings are computed, not forced. Wider exits are faster but their longer path does not win these matched controls."); Line();
        Line("## B/H/F/G/J remaining-horizon decisions"); Line();
        Line("Solo R2 uses the original audit input and TrackReading; occupancy is absent. Choices include unchanged external personality/risk terms. H starts at 0.60 of its single TurnEntry-labelled corner; the current Entry intent and following Straight Exit intent are observed, while no TurnMiddle target executes in that coarse fixture. F/G reverse lane surface quality; J changes geometry. No winner is an acceptance target."); Line();
        Line("| control | chosen intent | current target | physical s | distance m | exit speed m/s | straight payoff s | straight end m/s | candidates / actual resolutions |");
        Line("| --- | --- | ---: | ---: | ---: | ---: | ---: | ---: | --- |");
        foreach (var row in evidence.Choices)
            Line($"| {row.Scenario} | {row.SelectedIntent} | {row.CurrentTarget} | {N(row.Selected.PredictedTraversalTimeSeconds)} | {N(row.Selected.PhysicalDistanceMeters)} | {Nullable(row.Selected.Traversal.CornerExitSpeedMetersPerSecond)} | {N(row.Selected.Traversal.FollowingStraightTimeSeconds)} | {Nullable(row.Selected.Traversal.FollowingStraightEndSpeedMetersPerSecond)} | {row.CandidateTraversals} / {row.ProductionResolutions} |");
        Line(); Line("## Requested versus achieved Motoarena endpoints"); Line();
        Line("All examples start at actual lateral 3 and 22 m/s. Middle end and actual 0.5-region sample are separate observations; null means unobserved. Arrival uses the existing physical 0.05 m lane-arrival tolerance."); Line();
        Line("| requested E/A/X | Entry end | Middle end | actual apex region | Exit end | total s | anchors reached at phase endpoints |");
        Line("| --- | ---: | ---: | ---: | ---: | ---: | --- |");
        foreach (var row in evidence.MovingExamples)
            Line($"| {row.Intent} | {Nullable(row.EntryLateralPosition)} | {Nullable(row.MiddleLateralPosition)} | {Nullable(row.ApexRegionLateralPosition)} | {Nullable(row.ExitLateralPosition)} | {N(row.PredictedTraversalTimeSeconds)} | {string.Join(", ", row.PhaseEndpoints.Where(p => p.Phase != TrajectoryPhase.FollowingStraight).Select(p => $"{p.Phase}={p.AnchorReached}"))} |");
        Line(); Line("Intent is not guaranteed achieved path. No fictitious waypoint or boundary displacement is stored."); Line();
        Line("## Standing starts A/D"); Line();
        foreach (var row in evidence.StandingStarts)
            Line($"- {row.Gate}: exact gate center {N(row.GateCenterLateralPosition)}, chosen {row.SelectedIntent}; reaction {N(row.ReactionTimeSeconds)} s. Mid-reaction lateral {N(row.DuringReaction.LateralPosition)}, physical distance {N(row.DuringReaction.TravelledMeters)} m, progress {N(row.DuringReaction.CanonicalProgress)}, speed {N(row.DuringReaction.SpeedMetersPerSecond)} m/s. Full horizon {N(row.Traversal.PredictedTraversalTimeSeconds)} s.");
        Line(); Line("A/D normal-engine parity tests compare all resolved motions exactly, including reaction, achieved phase endpoints, exit and straight speeds. No gate-specific bonus or integer gate-position replacement occurs."); Line();
        Line("## Determinism, isolation and validation"); Line();
        Line("Tests compare fixed/moving/partial/standing/lap-wrap projections with independently scripted normal production, including exact total time/distance, every motion node, phase endpoints, speeds and outcomes. Other tests cover reversed rider/candidate collections, equal-cost canonical ties, no live state/surface/time/log mutation, private wear, repeated/cold prefix replay, perfect/imperfect readers and seed/model-seed observations. B chooses a faster available route under neutral conditions; F/G and J change measured physics/choice without forced winners. Regenerate this report and JSON with `dotnet run --project src/Sandbox -c Release -- trajectory-intent-evaluation-report <scratch-directory>` and compare bytes. Historical blobs are separately protected."); Line();
        Line("## Performance"); Line();
        Line("Broader replay exposed a #53 correction/drive switching-boundary step with no converged time root (A, seed 3, step 15). The shared production solver now halves that physical step and resolves the same primitives, up to 16 subdivisions, retaining the original tolerance and geometric node bound. Typed TimeSolveSubdivisions records recovery. No force, speed, correction or movement coefficient changes. Existing accepted #53 fixed-line hashes and moving evidence are checked separately."); Line();
        Line("35/35/13/5 candidate traversals per Straight/Entry/Middle/Exit decision; repeated exact scripted prefixes reduce production resolutions (counts above). Every candidate still starts from the same immutable snapshot and executes private commits. Runtime/allocation measurements are recorded in the PR; they are observations, with no CI timing gate. The bounded replay remains materially more expensive than the old static projection."); Line();
        Line("Desktop Release, .NET 8.0.30, 5 warm-ups then 15 four-lap runs per control: one rider median 531.656 ms, mean 540.513 ms, approximately 221.090 MB allocated/run; four-rider heat median 2151.049 ms, mean 2157.824 ms, approximately 879.587 MB allocated/run. Solo executes 844 candidate traversals / 2339 production segment resolutions; four riders execute 3248 / 9119 in this existing-contact fixture. All measurements use incidents OFF and logs OFF; existing contact still operates. These are cumulative allocations rather than retained/peak memory. Prefix reuse and immutable topology sharing reduced the initial approximate 313/1241 MB observations to 221/880 MB; no candidates, physics or winners were pruned for speed. Allocation cost remains material and warrants follow-up if batch throughput is insufficient."); Line();
        Line("## Remaining boundaries"); Line();
        Line("production-backed trajectory evaluation is now solo-physical; traffic feasibility and contested-space constraints remain the next subsystem."); Line();
        Line("No common-time contested-space, traffic feasibility, motorcycle/rider footprint, passing/yielding/blocking/defence or global XY is implemented. Existing occupancy/contact remains provisional. MotionBoundaryTransition still reinterprets segment-local width coordinates; no offset sweep, transition spline or fake boundary distance is added. Combined grip/slip remains uncalibrated and outside production; no q/friction-circle tuning. Longitudinal constants remain provisional; no banking, asymmetric detailed Motoarena shape, RPM/clutch/wheelspin or real-data drive recalibration. The major trajectory-decision consistency blocker is resolved within current production physics; these remaining geometry/interaction/calibration limits remain.");
        return text.ToString();
    }

    private static RiderDecisionContext Context(Track track, TrackState state, RiderState rider)
    {
        var snapshot = new SimulationEngine(new Fixed(2)).CaptureSnapshot(track, state, new[] { rider },
            new(54, rider.SegmentIndex, rider.LapsCompleted, rider.SegmentIndex, 0, 4));
        return new(snapshot, snapshot.Rider(rider.RiderId));
    }
    private static Track CornerFirst(TrackGeometry geometry) => new(new[]
    {
        new TrackSegment(0, SegmentType.TurnEntry), new TrackSegment(1, SegmentType.TurnMiddle),
        new TrackSegment(2, SegmentType.TurnExit), new TrackSegment(3, SegmentType.Straight),
        new TrackSegment(4, SegmentType.TurnEntry), new TrackSegment(5, SegmentType.TurnMiddle),
        new TrackSegment(6, SegmentType.TurnExit), new TrackSegment(7, SegmentType.Straight),
    }, geometry);
    private static double ReplayFixed(RiderDecisionContext context, int lane, int[] indices)
    {
        var rider = context.Rider.ToMutableCopy(); var riders = new[] { rider };
        var state = new TrackState(context.TrackState.SegmentCount, 5, context.TrackState.GetSurface);
        var engine = new SimulationEngine(new Fixed(lane)); double time = 0;
        foreach (var index in indices)
        {
            var input = engine.CaptureSnapshot(context.Snapshot.Track, state, riders,
                context.Snapshot.Step with { SegmentIndex = index, StepNumber = index });
            var resolved = engine.Resolve(input, engine.Decide(input), new HeatSimulationOptions { Laps = 4, IncidentFrequency = 0 });
            time += resolved.Motions.Single().TotalTimeSeconds;
            engine.Commit(resolved, riders, state, new SimLog(false));
        }
        return time;
    }
    private sealed class Fixed(int lane) : IRiderDecisionModel
    {
        public RiderDecision Decide(TrackSegment segment, RiderState rider) => new(lane);
    }
}
