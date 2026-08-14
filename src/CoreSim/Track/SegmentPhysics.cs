using CoreSim.Setup;

namespace CoreSim;

public enum SegmentOutcome
{
    Ok,
    Brake,
    RunWide,
    Crash,
}

public sealed record SegmentResolution(
    SegmentOutcome Outcome,
    int Lane,
    float Speed,
    float IncidentRisk = 0f);

public sealed record SegmentPhysicsContext(
    TrackSegment Segment,
    int Lane,
    float Speed,
    TrackSurfaceState Surface,
    RiderSkills Skills,
    float Morale,
    BikeSetup Setup,
    float DecisionRisk = 0f);

/// <summary>
/// Constraint model, not motorcycle dynamics. It guarantees that excessive
/// speed on a tight line must end in braking, running wide or a crash.
/// </summary>
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

    public static float MaxSafeTurnSpeed(
        int lane,
        TrackSurfaceState surface,
        RiderSkills skills,
        float morale,
        BikeSetup setup)
    {
        var baseSpeed = MaxSafeTurnSpeed(lane);
        var control = RiderSkills.Normalize(skills.SlideControl);
        var abilityMultiplier = 0.96f + control * 0.08f;
        var moraleMultiplier = 0.98f + Math.Clamp(morale, 0f, 1f) * 0.04f;

        // Traction bias helps on loose/wet surfaces but costs the same amount on
        // a grippy surface, keeping setup a trade-off instead of an upgrade.
        var neededTraction = 1f - surface.EffectiveGrip;
        var setupError = MathF.Abs(setup.TractionBias - neededTraction);
        var setupMultiplier = 1.03f - setupError * 0.06f;
        var surfaceMultiplier = 0.74f + surface.EffectiveGrip * 0.26f;

        return baseSpeed * abilityMultiplier * moraleMultiplier * setupMultiplier * surfaceMultiplier;
    }

    /// <summary>Legacy neutral-surface contract retained for existing callers.</summary>
    public static SegmentResolution Apply(TrackSegment segment, int lane, float speed)
    {
        LaneModel.ValidateLane(lane);

        if (segment.Type == SegmentType.Straight)
            return new SegmentResolution(SegmentOutcome.Ok, lane, speed);

        return Resolve(segment, lane, speed, MaxSafeTurnSpeed(lane), BrakeSpeedFactor, RunWideSpeedFactor, 0f);
    }

    public static SegmentResolution Apply(SegmentPhysicsContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        LaneModel.ValidateLane(context.Lane);

        if (context.Segment.Type == SegmentType.Straight)
            return new SegmentResolution(SegmentOutcome.Ok, context.Lane, context.Speed, context.DecisionRisk);

        var control = RiderSkills.Normalize(context.Skills.SlideControl);
        var max = MaxSafeTurnSpeed(
            context.Lane,
            context.Surface,
            context.Skills,
            context.Morale,
            context.Setup);
        var brakeFactor = 1.06f + control * 0.08f;
        var runWideFactor = 1.18f + control * 0.16f;
        var surfaceRisk = (1f - context.Surface.EffectiveGrip) * 0.28f + context.Surface.Ruts * 0.18f;
        var moraleRisk = (1f - Math.Clamp(context.Morale, 0f, 1f)) * 0.08f;
        var incidentRisk = TrackSurfaceState.Clamp01(context.DecisionRisk + surfaceRisk + moraleRisk);

        return Resolve(context.Segment, context.Lane, context.Speed, max, brakeFactor, runWideFactor, incidentRisk);
    }

    private static SegmentResolution Resolve(
        TrackSegment segment,
        int lane,
        float speed,
        float max,
        float brakeFactor,
        float runWideFactor,
        float incidentRisk)
    {
        if (speed <= max)
            return new SegmentResolution(SegmentOutcome.Ok, lane, speed, incidentRisk);

        if (speed <= max * brakeFactor)
            return new SegmentResolution(SegmentOutcome.Brake, lane, max, incidentRisk);

        if (speed <= max * runWideFactor)
        {
            if (lane == LaneModel.MaxLane)
                return new SegmentResolution(SegmentOutcome.Crash, lane, 0f, incidentRisk);

            return new SegmentResolution(SegmentOutcome.RunWide, lane + 1, speed, incidentRisk);
        }

        return new SegmentResolution(SegmentOutcome.Crash, lane, 0f, incidentRisk);
    }
}
