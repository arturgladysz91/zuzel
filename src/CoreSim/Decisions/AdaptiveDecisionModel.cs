using System.Globalization;
using System.Runtime.ExceptionServices;

namespace CoreSim.Decisions;

/// <summary>Receding-horizon intent selection using isolated solo production traversal.</summary>
public sealed class AdaptiveDecisionModel : IRiderDecisionModel
{
    private readonly int _modelSeed;
    // Performance scheduling only; addressed RNG and canonical reduction are unchanged.
    internal int MaxDegreeOfParallelism { get; init; } = Math.Min(4, Environment.ProcessorCount);
    public AdaptiveDecisionModel(int seed = 1234) => _modelSeed = seed;
    public RiderDecision Decide(TrackSegment segment, RiderState rider)
        => new(rider.Lane, rider.Profile.Style.RiskTolerance * 0.05f, "no track context");
    public RiderDecision Decide(RiderDecisionContext context) => EvaluateDecision(context).Decision;

    internal TrajectoryDecisionDiagnostics EvaluateDecision(RiderDecisionContext context)
        => EvaluateCore(context, null, captureCandidates: false);

    /// <summary>Typed offline diagnostics; candidate ordering never breaks ties.</summary>
    public TrajectoryDecisionDiagnostics Evaluate(RiderDecisionContext context,
        IEnumerable<TrajectoryIntent>? candidates = null)
        => EvaluateCore(context, candidates, captureCandidates: true);

    private TrajectoryDecisionDiagnostics EvaluateCore(RiderDecisionContext context,
        IEnumerable<TrajectoryIntent>? candidates, bool captureCandidates)
    {
        ArgumentNullException.ThrowIfNull(context);
        var rider = context.Rider; var style = rider.Profile.Style; var geometry = context.Snapshot.Track.Geometry;
        var surfaceView = PerceivedTrackState(context);
        var evaluator = new TrajectoryEvaluator(context, surfaceView);
        var evaluationIndex = evaluator.Horizon.FirstOrDefault(part =>
            context.Snapshot.Track.Segments[part.SegmentIndex].Type != SegmentType.Straight)?.SegmentIndex
            ?? context.SegmentIndex;
        var tuples = (candidates ?? TrajectoryCandidates.Generate(context.Segment.Type)).Distinct().ToArray();
        List<TrajectoryIntentEvaluation> EvaluateGroup(TrajectoryIntent[] group, TrajectoryEvaluator local)
        {
            var rows = new List<TrajectoryIntentEvaluation>();
            foreach (var intent in group)
            {
                var target = intent.TargetFor(context.Segment.Type); var traversal = local.Evaluate(intent);
                var requestedChange = Math.Abs(target - (double)rider.LateralPosition); var previous = target;
                foreach (var part in local.Horizon)
                {
                    var next = TrajectoryEvaluator.Target(intent, part.Phase);
                    requestedChange += Math.Abs(next - previous); previous = next;
                }
                // Physical displacement already costs elapsed time. Retain only behavioral reluctance.
                var reluctance = (float)requestedChange * (1f - style.LaneChangeTendency) * 0.025f;
                var styleCost = MathF.Abs(target - style.OutsidePreference * LaneModel.MaxLane) * 0.035f;
                var occupancyCost = IsOccupied(context, target, geometry) ? 0.30f : 0f;
                var surface = context.TrackState.GetSurface(evaluationIndex, target);
                var riskCost = (1f - surface.EffectiveGrip) * (0.06f + (1f - style.RiskTolerance) * 0.12f) + surface.Ruts * 0.08f;
                // Terminal deterministic crashes are measured, but cannot be a feasible short route.
                var total = traversal.CompletedHorizon
                    ? traversal.PredictedTraversalTimeSeconds + styleCost + reluctance + occupancyCost + riskCost
                    : double.PositiveInfinity;
                rows.Add(new(traversal, styleCost, reluctance, occupancyCost, riskCost, requestedChange, total));
            }
            return rows;
        }
        var groups = tuples.GroupBy(intent => intent.TargetFor(context.Segment.Type)).OrderBy(g => g.Key)
            .Select(g => g.ToArray()).ToArray();
        var workers = new TrajectoryEvaluator[groups.Length];
        var results = new List<TrajectoryIntentEvaluation>[groups.Length];
        // The optional synchronous test observer counts the sequential graph.
        // Worker counts below independently prove the same unique prefixes in parallel.
        var degree = ProjectionCaptureAudit.Observer is null ? MaxDegreeOfParallelism : 1;
        if (degree == 1)
        {
            results = new[] { EvaluateGroup(tuples, evaluator) };
            workers = new[] { evaluator };
        }
        else
        {
            // Each first target owns its entire private prefix graph, rider/surface
            // states and local results. Workers share only immutable snapshot inputs.
            var failures = new Exception?[groups.Length];
            Parallel.For(0, groups.Length, new ParallelOptions { MaxDegreeOfParallelism = degree }, index =>
            {
                try
                {
                    var local = new TrajectoryEvaluator(context, surfaceView);
                    workers[index] = local;
                    results[index] = EvaluateGroup(groups[index], local);
                }
                catch (Exception error) { failures[index] = error; }
            });
            // Preserve the original domain exception type/message and stable
            // first-target precedence instead of exposing Parallel's wrapper.
            foreach (var failure in failures)
                if (failure is not null) ExceptionDispatchInfo.Capture(failure).Throw();
        }
        var evaluations = results.SelectMany(rows => rows).ToList();
        if (evaluations.Count == 0) throw new ArgumentException("At least one candidate is required.", nameof(candidates));
        var winner = evaluations.OrderBy(e => e.TotalCost)
            .ThenBy(e => e.PredictedTraversalTimeSeconds).ThenBy(e => e.RequestedLaneChange)
            .ThenBy(e => e.Intent.EntryTarget).ThenBy(e => e.Intent.ApexTarget).ThenBy(e => e.Intent.ExitTarget).First();
        var bestLane = winner.Intent.TargetFor(context.Segment.Type);
        var chosen = context.TrackState.GetSurface(context.SegmentIndex, bestLane);
        var surfaceRisk = (1f - chosen.EffectiveGrip) * (0.15f + style.RiskTolerance * 0.20f);
        var decision = new RiderDecision(bestLane, TrackSurfaceState.Clamp01(style.RiskTolerance * 0.10f + surfaceRisk),
            string.Create(CultureInfo.InvariantCulture,
                $"intent {winner.Intent} time={winner.PredictedTraversalTimeSeconds:F3} total={winner.TotalCost:F3}"))
            { Trajectory = winner.Intent };
        var canonical = captureCandidates ? evaluations.OrderBy(e => e.Intent.EntryTarget).ThenBy(e => e.Intent.ApexTarget)
            .ThenBy(e => e.Intent.ExitTarget).Select(e => e with { Selected = e.Intent == winner.Intent }).ToArray()
            : Array.Empty<TrajectoryIntentEvaluation>();
        return new(decision, Array.AsReadOnly(canonical), workers.Sum(w => w.CandidateTraversalCount), workers.Sum(w => w.ProductionResolutionCount));
    }
    /// <summary>Immutable raw-cell observations; normal continuous interpolation follows in replay.</summary>
    public TrackStateSnapshot PerceivedTrackState(RiderDecisionContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var reading = RiderSkills.Normalize(context.Rider.Profile.Skills.TrackReading);
        return new TrackState(context.TrackState.SegmentCount, context.TrackState.LinesCount, (segment, lane) =>
        {
            var surface = context.TrackState.GetSurface(segment, lane);
            var noise = DeterministicRandom.SampleSigned(context.Seed, context.HeatId, context.StepNumber,
                context.Rider.RiderId, RandomChannel.TrackObservation, segment, lane, _modelSeed) * (1f - reading) * 0.09f;
            return new TrackSurfaceState(TrackSurfaceState.Clamp01(surface.Grip + noise),
                TrackSurfaceState.Clamp01(surface.Ruts - noise * 0.5f), surface.Moisture);
        }).Snapshot();
    }
    private static bool IsOccupied(RiderDecisionContext context, int lane, TrackGeometry geometry)
        => context.Riders.Any(other => other.RiderId != context.Rider.RiderId && other.IsActive
            && other.SegmentIndex == context.SegmentIndex
            && LateralSpaceModel.IsWithinProvisionalOccupancyThreshold(other.LateralPosition, lane, context.Segment.Type, geometry)
            && Math.Abs(other.ElapsedTimeSeconds - context.Rider.ElapsedTimeSeconds) < 0.30f);
}
