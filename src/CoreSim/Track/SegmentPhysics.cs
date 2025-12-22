// Minimalny kontrakt dynamiki w segmencie: łuk ma limit prędkości zależny od linii, prosta nie dodaje prędkości.
namespace CoreSim;

public enum SegmentOutcome
{
    Ok,
    Brake,
    RunWide,
    Crash,
}

public sealed record SegmentResolution(SegmentOutcome Outcome, int Lane, float Speed);

public static class SegmentPhysics
{
    public const float BaseTurnMaxSpeed = 12.0f;
    public const float TurnMaxSpeedDeltaPerLane = 1.5f;
    public const float BrakeSpeedFactor = 1.10f;
    public const float RunWideSpeedFactor = 1.30f;

    public static float MaxSafeTurnSpeed(int lane)
    {
        LaneModel.ValidateLane(lane);
        return BaseTurnMaxSpeed + lane * TurnMaxSpeedDeltaPerLane;
    }

    public static SegmentResolution Apply(TrackSegment segment, int lane, float speed)
    {
        LaneModel.ValidateLane(lane);

        if (segment.Type == SegmentType.Straight)
        {
            return new SegmentResolution(SegmentOutcome.Ok, lane, speed);
        }

        var max = MaxSafeTurnSpeed(lane);
        if (speed <= max)
        {
            return new SegmentResolution(SegmentOutcome.Ok, lane, speed);
        }

        if (speed <= max * BrakeSpeedFactor)
        {
            return new SegmentResolution(SegmentOutcome.Brake, lane, max);
        }

        if (speed <= max * RunWideSpeedFactor)
        {
                        if (lane == LaneModel.MaxLane)
            {
                return new SegmentResolution(SegmentOutcome.Crash, lane, 0f);
            }

            var widerLane = Math.Min(lane + 1, LaneModel.MaxLane);
            return new SegmentResolution(SegmentOutcome.RunWide, widerLane, speed);
        }

        return new SegmentResolution(SegmentOutcome.Crash, lane, 0f);
    }
}
