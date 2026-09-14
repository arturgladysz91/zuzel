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
    float IncidentRisk = 0f,
    float? ContinuousCorrectionTargetSpeedMetersPerSecond = null);

public sealed record SegmentPhysicsContext(
    TrackSegment Segment,
    int Lane,
    float Speed,
    TrackGeometry Geometry,
    TrackSurfaceState Surface,
    RiderSkills Skills,
    float Morale,
    BikeSetup Setup,
    float DecisionRisk = 0f,
    float? LateralPosition = null,
    CornerPhaseContext? CornerPhase = null)
{
    /// <summary>Compatibility constructor using the example-track geometry.</summary>
    public SegmentPhysicsContext(
        TrackSegment Segment,
        int Lane,
        float Speed,
        TrackSurfaceState Surface,
        RiderSkills Skills,
        float Morale,
        BikeSetup Setup,
        float DecisionRisk = 0f,
        float? LateralPosition = null)
        : this(
            Segment,
            Lane,
            Speed,
            TrackGeometry.Default,
            Surface,
            Skills,
            Morale,
            Setup,
            DecisionRisk,
            LateralPosition,
            CornerPhase: null)
    {
    }
}

/// <summary>
/// Constraint model, not motorcycle dynamics. It guarantees that excessive
/// speed on a tight line must end in braking, running wide or a crash.
/// </summary>
public static class SegmentPhysics
{
    public const float ReferenceTurnRadiusMeters = 24.0f;
    public const float ReferenceTurnSpeedMetersPerSecond = 16.0f;
    // ADVANCED settled/apex capability only. Geometry-only/legacy API remains
    // at its original 16 m/s reference; this is not a longitudinal drive speed.
    public const float AdvancedReferenceTurnSpeedMetersPerSecond = 19.0f;
    public const float BrakeSpeedFactor = 1.10f;
    public const float RunWideSpeedFactor = 1.30f;
    public const float MinRunWideOverspeedRetention = 0.35f;
    public const float MaxRunWideOverspeedRetention = 0.65f;
    public const float NeutralRunWideOverspeedRetention = 0.50f;
    public const float AdvancedQuietCorrectionSpeedFactor = 1.015f;
    public const float MinAdvancedBrakeSpeedFactor = 1.06f;
    public const float MaxAdvancedBrakeSpeedFactor = 1.14f;
    public const float AdvancedBrakeSpeedFactorRange = 0.08f;
    public const float MinAdvancedRunWideSpeedFactor = 1.18f;
    public const float MaxAdvancedRunWideSpeedFactor = 1.34f;
    public const float AdvancedRunWideSpeedFactorRange = 0.16f;

    public static float MaxSafeTurnSpeed(int lane)
        => MaxSafeTurnSpeed(lane, TrackGeometry.Default);

    public static float MaxSafeTurnSpeed(int lane, TrackGeometry geometry)
    {
        ArgumentNullException.ThrowIfNull(geometry);
        LaneModel.ValidateLane(lane);
        return MaxSafeTurnSpeed((float)lane, geometry);
    }

    public static float MaxSafeTurnSpeed(float lateralPosition, TrackGeometry geometry)
    {
        // This is a curvature-based constraint, not a motorcycle dynamics model.
        var radiusMeters = LaneModel.TurnArcRadiusMeters(lateralPosition, geometry);
        return ReferenceTurnSpeedMetersPerSecond
            * MathF.Sqrt(radiusMeters / ReferenceTurnRadiusMeters);
    }

    public static float MaxSafeTurnSpeed(
        int lane,
        TrackGeometry geometry,
        TrackSurfaceState surface,
        RiderSkills skills,
        BikeSetup setup)
    {
        ArgumentNullException.ThrowIfNull(skills);
        ArgumentNullException.ThrowIfNull(setup);
        ArgumentNullException.ThrowIfNull(geometry);
        LaneModel.ValidateLane(lane);
        return MaxSafeTurnSpeed((float)lane, geometry, surface, skills, setup);
    }

    public static float MaxSafeTurnSpeed(
        float lateralPosition,
        TrackGeometry geometry,
        TrackSurfaceState surface,
        RiderSkills skills,
        BikeSetup setup)
    {
        ArgumentNullException.ThrowIfNull(skills);
        ArgumentNullException.ThrowIfNull(setup);

        var geometrySpeedMetersPerSecond = AdvancedReferenceTurnSpeedMetersPerSecond
            * MathF.Sqrt(LaneModel.TurnArcRadiusMeters(lateralPosition, geometry) / ReferenceTurnRadiusMeters);
        var control = RiderSkills.Normalize(skills.SlideControl);
        var speedAbility = RiderSkills.Normalize(skills.Speed);
        var controlMultiplier = 0.97f + control * 0.06f;
        var speedMultiplier = 0.94f + speedAbility * 0.12f;

        // Traction bias helps on loose/wet surfaces but costs the same amount on
        // a grippy surface, keeping setup a trade-off instead of an upgrade.
        var neededTraction = 1f - surface.EffectiveGrip;
        var setupError = MathF.Abs(setup.TractionBias - neededTraction);
        var setupMultiplier = 1.03f - setupError * 0.06f;
        var surfaceMultiplier = 0.74f + surface.EffectiveGrip * 0.26f;

        return geometrySpeedMetersPerSecond
            * controlMultiplier
            * speedMultiplier
            * setupMultiplier
            * surfaceMultiplier;
    }

    /// <summary>
    /// Compatibility overload. Morale is intentionally ignored by the physical
    /// speed limit and the example-track geometry is used.
    /// </summary>
    public static float MaxSafeTurnSpeed(
        int lane,
        TrackSurfaceState surface,
        RiderSkills skills,
        float morale,
        BikeSetup setup)
    {
        _ = morale;
        return MaxSafeTurnSpeed(lane, TrackGeometry.Default, surface, skills, setup);
    }

    /// <summary>Legacy neutral-surface contract retained for existing callers.</summary>
    public static SegmentResolution Apply(TrackSegment segment, int lane, float speed)
        => Apply(segment, lane, speed, TrackGeometry.Default);

    public static SegmentResolution Apply(
        TrackSegment segment,
        int lane,
        float speed,
        TrackGeometry geometry)
    {
        ArgumentNullException.ThrowIfNull(segment);
        ArgumentNullException.ThrowIfNull(geometry);
        LaneModel.ValidateLane(lane);

        if (segment.Type == SegmentType.Straight)
            return new SegmentResolution(SegmentOutcome.Ok, lane, speed);

        return ResolveLegacy(
            lane,
            speed,
            MaxSafeTurnSpeed(lane, geometry),
            BrakeSpeedFactor,
            RunWideSpeedFactor,
            0f,
            quietCorrectionFactor: 1f,
            runWideOverspeedRetention: NeutralRunWideOverspeedRetention);
    }

    public static SegmentResolution Apply(SegmentPhysicsContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(context.Geometry);
        LaneModel.ValidateLane(context.Lane);
        if (context.CornerPhase is { } cornerPhase
            && (context.Segment.Type == SegmentType.Straight
                || cornerPhase.CompatibilitySegmentType != context.Segment.Type))
        {
            throw new ArgumentException(
                "Corner phase context must describe the current turn segment.",
                nameof(context));
        }

        if (context.Segment.Type == SegmentType.Straight)
            return new SegmentResolution(SegmentOutcome.Ok, context.Lane, context.Speed, context.DecisionRisk);

        var control = RiderSkills.Normalize(context.Skills.SlideControl);
        var max = MaxSafeTurnSpeed(
            context.LateralPosition ?? (float)context.Lane,
            context.Geometry,
            context.Surface,
            context.Skills,
            context.Setup);
        // Context-free calls preserve the settled-capability compatibility API.
        // Production always supplies the complete logical-corner context.
        if (context.CornerPhase is { } phase)
            max = ContinuousCornerEnvelope.Create(phase, context.LateralPosition ?? context.Lane,
                context.Geometry, context.Surface, context.Skills, context.Setup)
                .SpeedMetersPerSecond(phase.CornerProgress);
        var brakeFactor = MinAdvancedBrakeSpeedFactor
            + control * AdvancedBrakeSpeedFactorRange;
        var runWideFactor = MinAdvancedRunWideSpeedFactor
            + control * AdvancedRunWideSpeedFactorRange;
        var runWideOverspeedRetention = MinRunWideOverspeedRetention
            + (MaxRunWideOverspeedRetention - MinRunWideOverspeedRetention) * control;
        var surfaceRisk = (1f - context.Surface.EffectiveGrip) * 0.28f + context.Surface.Ruts * 0.18f;
        var moraleRisk = (1f - Math.Clamp(context.Morale, 0f, 1f)) * 0.08f;
        var incidentRisk = TrackSurfaceState.Clamp01(context.DecisionRisk + surfaceRisk + moraleRisk);

        return ResolveAdvanced(
            context.Lane,
            context.Speed,
            max,
            brakeFactor,
            runWideFactor,
            incidentRisk,
            quietCorrectionFactor: AdvancedQuietCorrectionSpeedFactor,
            runWideOverspeedRetention: runWideOverspeedRetention);
    }

    private static SegmentResolution ResolveLegacy(
        int lane,
        float speed,
        float max,
        float brakeFactor,
        float runWideFactor,
        float incidentRisk,
        float quietCorrectionFactor,
        float runWideOverspeedRetention)
    {
        if (speed <= max * quietCorrectionFactor)
            return new SegmentResolution(SegmentOutcome.Ok, lane, MathF.Min(speed, max), incidentRisk);

        if (speed <= max * brakeFactor)
            return new SegmentResolution(SegmentOutcome.Brake, lane, max, incidentRisk);

        if (speed <= max * runWideFactor)
        {
            if (lane == LaneModel.MaxLane)
                return new SegmentResolution(SegmentOutcome.Crash, lane, 0f, incidentRisk);

            var overspeed = speed - max;
            var correctedSpeed = max + overspeed * runWideOverspeedRetention;
            return new SegmentResolution(SegmentOutcome.RunWide, lane + 1, correctedSpeed, incidentRisk);
        }

        return new SegmentResolution(SegmentOutcome.Crash, lane, 0f, incidentRisk);
    }

    private static SegmentResolution ResolveAdvanced(
        int lane,
        float speed,
        float max,
        float brakeFactor,
        float runWideFactor,
        float incidentRisk,
        float quietCorrectionFactor,
        float runWideOverspeedRetention)
    {
        if (speed <= max)
            return new SegmentResolution(SegmentOutcome.Ok, lane, speed, incidentRisk);

        if (speed <= max * quietCorrectionFactor)
        {
            return new SegmentResolution(
                SegmentOutcome.Ok,
                lane,
                speed,
                incidentRisk,
                max);
        }

        if (speed <= max * brakeFactor)
        {
            return new SegmentResolution(
                SegmentOutcome.Brake,
                lane,
                speed,
                incidentRisk,
                max);
        }

        if (speed <= max * runWideFactor)
        {
            if (lane == LaneModel.MaxLane)
                return new SegmentResolution(SegmentOutcome.Crash, lane, 0f, incidentRisk);

            var overspeed = speed - max;
            var correctionTarget = max + overspeed * runWideOverspeedRetention;
            return new SegmentResolution(
                SegmentOutcome.RunWide,
                lane + 1,
                speed,
                incidentRisk,
                correctionTarget);
        }

        return new SegmentResolution(SegmentOutcome.Crash, lane, 0f, incidentRisk);
    }
}
