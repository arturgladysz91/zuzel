using System.Globalization;

namespace CoreSim.Decisions;

/// <summary>Receding-horizon intent selection using isolated solo production traversal.</summary>
public sealed class AdaptiveDecisionModel : IRiderDecisionModel
{
    private readonly int _modelSeed;
    public AdaptiveDecisionModel(int seed = 1234) => _modelSeed = seed;
    public RiderDecision Decide(TrackSegment segment, RiderState rider)
        => new(rider.Lane, rider.Profile.Style.RiskTolerance * 0.05f, "no track context");
    public RiderDecision Decide(RiderDecisionContext context) => Evaluate(context).Decision;

    /// <summary>Typed offline diagnostics; candidate ordering never breaks ties.</summary>
    public TrajectoryDecisionDiagnostics Evaluate(RiderDecisionContext context,
        IEnumerable<TrajectoryIntent>? candidates = null)
    {
        ArgumentNullException.ThrowIfNull(context);
        var rider = context.Rider; var style = rider.Profile.Style; var geometry = context.Snapshot.Track.Geometry;
        var evaluator = new TrajectoryEvaluator(context, PerceivedTrackState(context));
        var evaluationIndex = evaluator.Horizon.FirstOrDefault(part =>
            context.Snapshot.Track.Segments[part.SegmentIndex].Type != SegmentType.Straight)?.SegmentIndex
            ?? context.SegmentIndex;
        var evaluations = new List<TrajectoryIntentEvaluation>();
        foreach (var intent in (candidates ?? TrajectoryCandidates.Generate(context.Segment.Type)).Distinct())
        {
            var target = intent.TargetFor(context.Segment.Type); var traversal = evaluator.Evaluate(intent);
            var requestedChange = Math.Abs(target - (double)rider.LateralPosition); var previous = target;
            foreach (var part in evaluator.Horizon)
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
            evaluations.Add(new(traversal, styleCost, reluctance, occupancyCost, riskCost, requestedChange, total));
        }
        if (evaluations.Count == 0) throw new ArgumentException("At least one candidate is required.", nameof(candidates));
        var winner = evaluations.OrderBy(e => e.TotalCost)
            .ThenBy(e => e.PredictedTraversalTimeSeconds).ThenBy(e => e.RequestedLaneChange)
            .ThenBy(e => e.Intent.EntryTarget).ThenBy(e => e.Intent.ApexTarget).ThenBy(e => e.Intent.ExitTarget).First();
        var bestLane = winner.Intent.TargetFor(context.Segment.Type);
        var chosen = context.TrackState.GetSurface(context.SegmentIndex, bestLane);
        var surfaceRisk = (1f - chosen.EffectiveGrip) * (0.15f + style.RiskTolerance * 0.20f);
        var decision = new RiderDecision(bestLane, TrackSurfaceState.Clamp01(style.RiskTolerance * 0.10f + surfaceRisk),
            string.Create(CultureInfo.InvariantCulture,
                $"intent {winner.Intent} time={winner.PredictedTraversalTimeSeconds:F3} total={winner.TotalCost:F3}"));
        var canonical = evaluations.OrderBy(e => e.Intent.EntryTarget).ThenBy(e => e.Intent.ApexTarget)
            .ThenBy(e => e.Intent.ExitTarget).Select(e => e with { Selected = e.Intent == winner.Intent }).ToArray();
        return new(decision, Array.AsReadOnly(canonical), evaluator.CandidateTraversalCount, evaluator.ProductionResolutionCount);
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
