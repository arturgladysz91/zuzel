namespace CoreSim.Decisions;

/// <summary>
/// Data-driven lane selection. It compares projected route time instead of raw
/// lane speed, reads the surface imperfectly and respects rider style/traffic.
/// </summary>
public sealed class AdaptiveDecisionModel : IRiderDecisionModel
{
    private readonly Random _random;

    public AdaptiveDecisionModel(int seed = 1234) => _random = new Random(seed);

    public RiderDecision Decide(TrackSegment segment, RiderState rider)
        => new(rider.Lane, rider.Profile.Style.RiskTolerance * 0.05f, "no track context");

    public RiderDecision Decide(RiderDecisionContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var rider = context.Rider;
        var style = rider.Profile.Style;
        var reading = RiderSkills.Normalize(rider.Profile.Skills.TrackReading);
        var (evaluationSegment, evaluationIndex) = ResolveEvaluationSegment(context);
        var bestLane = rider.Lane;
        var bestCost = float.PositiveInfinity;

        for (var lane = LaneModel.MinLane; lane <= LaneModel.MaxLane; lane++)
        {
            var surface = context.TrackState.GetSurface(evaluationIndex, lane);
            var effectiveGrip = surface.EffectiveGrip;

            // Weak readers see a noisier and more conservative approximation.
            var observationNoise = ((float)_random.NextDouble() - 0.5f) * (1f - reading) * 0.18f;
            var perceivedSurface = new TrackSurfaceState(
                TrackSurfaceState.Clamp01(surface.Grip + observationNoise),
                TrackSurfaceState.Clamp01(surface.Ruts - observationNoise * 0.5f),
                surface.Moisture);

            var distance = Math.Abs(lane - rider.Lane);
            var movementCost = distance * (0.015f + (1f - style.LaneChangeTendency) * 0.025f);
            var preferredLane = style.OutsidePreference * LaneModel.MaxLane;
            var styleCost = MathF.Abs(lane - preferredLane) * 0.035f;
            var occupancyCost = IsOccupied(context, lane) ? 0.30f : 0f;
            var surfaceRiskCost = (1f - effectiveGrip)
                                  * (0.06f + (1f - style.RiskTolerance) * 0.12f)
                                  + surface.Ruts * 0.08f;
            var projectedSpeed = SegmentPhysics.MaxSafeTurnSpeed(
                lane,
                perceivedSurface,
                rider.Profile.Skills,
                rider.Morale,
                rider.ActiveSetup);
            var projectedTime = ProjectedRouteTime(evaluationSegment, lane, projectedSpeed);
            var cost = projectedTime + movementCost + styleCost + occupancyCost + surfaceRiskCost;

            if (cost < bestCost)
            {
                bestCost = cost;
                bestLane = lane;
            }
        }

        var baseRisk = style.RiskTolerance * 0.10f;
        var chosen = context.TrackState.GetSurface(context.SegmentIndex, bestLane);
        var surfaceRisk = (1f - chosen.EffectiveGrip) * (0.15f + style.RiskTolerance * 0.20f);

        return new RiderDecision(
            bestLane,
            TrackSurfaceState.Clamp01(baseRisk + surfaceRisk),
            $"projected cost={bestCost:F3}");
    }

    private static (TrackSegment Segment, int Index) ResolveEvaluationSegment(RiderDecisionContext context)
    {
        if (context.Segment.Type != SegmentType.Straight || context.Track is null)
            return (context.Segment, context.SegmentIndex);

        var nextIndex = (context.SegmentIndex + 1) % context.Track.Segments.Count;
        return (context.Track.Segments[nextIndex], nextIndex);
    }

    private static float ProjectedRouteTime(TrackSegment segment, int lane, float speed)
    {
        // A lane is judged over a complete bend plus the following straight.
        // This balances the shorter inside route against the higher exit speed
        // available outside. A straight decision prepares the next bend.
        var bendLength = LaneModel.TurnArcLengthMeters(lane) * 3f;
        var routeLength = segment.Type == SegmentType.Straight
            ? LaneModel.StraightLengthMeters
            : bendLength + LaneModel.StraightLengthMeters;
        return routeLength / MathF.Max(speed, 1f);
    }

    private static bool IsOccupied(RiderDecisionContext context, int lane)
        => context.Riders.Any(other =>
            other.RiderId != context.Rider.RiderId
            && !other.IsCrashed
            && other.CurrentSegmentId == context.Segment.Id
            && Math.Abs(other.LateralPosition - lane) < 0.55f
            && Math.Abs(other.ElapsedTimeSeconds - context.Rider.ElapsedTimeSeconds) < 0.30f);
}
