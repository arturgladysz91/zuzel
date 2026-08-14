namespace CoreSim.Decisions;

/// <summary>
/// Data-driven lane selection. It reads the surface imperfectly according to the
/// rider's skill, respects style and penalizes lanes occupied by nearby riders.
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
        var bestLane = rider.Lane;
        var bestScore = float.NegativeInfinity;

        for (var lane = LaneModel.MinLane; lane <= LaneModel.MaxLane; lane++)
        {
            var surface = context.TrackState.GetSurface(context.SegmentIndex, lane);
            var effectiveGrip = surface.EffectiveGrip;

            // Weak readers see a noisier and more conservative approximation.
            var observationNoise = ((float)_random.NextDouble() - 0.5f) * (1f - reading) * 0.18f;
            var perceivedGrip = TrackSurfaceState.Clamp01(effectiveGrip + observationNoise);

            var distance = Math.Abs(lane - rider.Lane);
            var movementPenalty = distance * (0.24f - style.LaneChangeTendency * 0.14f);
            var outsidePreference = (lane / (float)LaneModel.MaxLane - 0.5f)
                                    * (style.OutsidePreference - 0.5f)
                                    * 0.24f;
            var occupancyPenalty = IsOccupied(context, lane) ? 0.35f : 0f;

            // Straights only position the rider; they never award a speed score.
            var surfaceWeight = context.Segment.Type == SegmentType.Straight ? 0.18f : 0.72f;
            var score = perceivedGrip * surfaceWeight
                        - surface.Ruts * 0.28f
                        - movementPenalty
                        + outsidePreference
                        - occupancyPenalty;

            if (score > bestScore)
            {
                bestScore = score;
                bestLane = lane;
            }
        }

        var baseRisk = style.RiskTolerance * 0.10f;
        var chosen = context.TrackState.GetSurface(context.SegmentIndex, bestLane);
        var surfaceRisk = (1f - chosen.EffectiveGrip) * (0.15f + style.RiskTolerance * 0.20f);

        return new RiderDecision(
            bestLane,
            TrackSurfaceState.Clamp01(baseRisk + surfaceRisk),
            $"lane score={bestScore:F3}");
    }

    private static bool IsOccupied(RiderDecisionContext context, int lane)
        => context.Riders.Any(other =>
            other.RiderId != context.Rider.RiderId
            && !other.IsCrashed
            && other.CurrentSegmentId == context.Segment.Id
            && Math.Abs(other.LateralPosition - lane) < 0.55f
            && Math.Abs(other.ElapsedTimeSeconds - context.Rider.ElapsedTimeSeconds) < 0.30f);
}
