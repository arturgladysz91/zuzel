using CoreSim.Setup;

namespace CoreSim;

/// <summary>One actual endpoint. Offsets are segment-local, from the inner reference.</summary>
public sealed record ExecutedPathNode(float SegmentProgress, float? CornerProgress,
    float ElapsedTimeSeconds, float DistanceMeters, float LateralPosition,
    float PhysicalOffsetMeters, float? RadiusMeters, float CurvaturePerMeter,
    TrackSurfaceState Surface, float SpeedMetersPerSecond,
    float? LocalSafeSpeedMetersPerSecond, float? EnvelopeSpeedMetersPerSecond,
    float NetDriveAccelerationMetersPerSecondSquared);

/// <summary>The exact geometry, quadrature sample and physical buckets consumed by a step.</summary>
public sealed record ExecutedPathStep(float DistanceMeters, float TimeSeconds,
    float SampledLateralPosition, TrackSurfaceState SampledSurface,
    float CorrectionDistanceMeters, float CarryDistanceMeters, float DriveDistanceMeters,
    float CorrectionTimeSeconds, float CarryTimeSeconds, float DriveTimeSeconds,
    float ReferenceDriveForceNewtons, int SolverIterations, bool UsedBisectionFallback)
{
    /// <summary>Bounded physical-step subdivisions at a correction/drive time discontinuity.</summary>
    public int TimeSolveSubdivisions { get; init; }
}

/// <summary>Immutable production path; telemetry and wear consume these same steps.</summary>
public sealed class ExecutedSegmentPath : IEquatable<ExecutedSegmentPath>
{
    public IReadOnlyList<ExecutedPathNode> Nodes { get; }
    public IReadOnlyList<ExecutedPathStep> Steps { get; }
    public float ReactionTimeSeconds { get; }
    public float DistanceMeters => Nodes[^1].DistanceMeters;
    public float MovementTimeSeconds => Nodes[^1].ElapsedTimeSeconds - ReactionTimeSeconds;
    public float TravelTimeSeconds => Nodes[^1].ElapsedTimeSeconds;
    public float ExitSpeedMetersPerSecond => Nodes[^1].SpeedMetersPerSecond;
    public float PeakSpeedMetersPerSecond => Nodes.Max(n => n.SpeedMetersPerSecond);
    public float MinimumSpeedMetersPerSecond => Nodes.Min(n => n.SpeedMetersPerSecond);
    public ExecutedPathNode? ApexNode => Nodes.FirstOrDefault(n => n.CornerProgress == ContinuousCornerEnvelope.ApexProgress);
    public float? MinimumRadiusMeters => Nodes.Min(n => n.RadiusMeters);
    public float? MaximumRadiusMeters => Nodes.Max(n => n.RadiusMeters);
    public float MaximumCurvaturePerMeter => Nodes.Max(n => n.CurvaturePerMeter);
    public float FullDriveEquilibriumSpeedMetersPerSecond { get; }

    internal ExecutedSegmentPath(IEnumerable<ExecutedPathNode> nodes, IEnumerable<ExecutedPathStep> steps,
        float reactionTimeSeconds, BikeSetup setup)
    {
        Nodes = Array.AsReadOnly(nodes.ToArray());
        Steps = Array.AsReadOnly(steps.ToArray());
        ReactionTimeSeconds = reactionTimeSeconds;
        FullDriveEquilibriumSpeedMetersPerSecond = LongitudinalDynamics.CalculateFullDriveEquilibriumSpeedMetersPerSecond(
            Steps[0].ReferenceDriveForceNewtons, setup);
    }

    public bool Equals(ExecutedSegmentPath? other) => other is not null
        && ReactionTimeSeconds == other.ReactionTimeSeconds && Nodes.SequenceEqual(other.Nodes) && Steps.SequenceEqual(other.Steps);
    public override bool Equals(object? obj) => obj is ExecutedSegmentPath other && Equals(other);
    public override int GetHashCode() => HashCode.Combine(DistanceMeters, TravelTimeSeconds, Nodes.Count);

    internal StraightSpeedProfile StraightProfile()
    {
        float acceleration = 0f, cruise = 0f, deceleration = 0f;
        for (var i = 0; i < Steps.Count; i++)
        {
            var delta = Nodes[i + 1].SpeedMetersPerSecond - Nodes[i].SpeedMetersPerSecond;
            if (delta > 0f) acceleration += Steps[i].DistanceMeters;
            else if (delta < 0f) deceleration += Steps[i].DistanceMeters;
            else cruise += Steps[i].DistanceMeters;
        }
        return new(ExitSpeedMetersPerSecond, PeakSpeedMetersPerSecond, MovementTimeSeconds,
            acceleration, cruise, deceleration, FullDriveEquilibriumSpeedMetersPerSecond);
    }

    internal StandingStartLaunchProfile LaunchProfile(BikeSetup setup)
    {
        var straight = StraightProfile();
        float? timeTo70 = null, speedAtTwo = null;
        for (var i = 0; i < Steps.Count; i++)
        {
            var start = Nodes[i]; var end = Nodes[i + 1]; var step = Steps[i];
            var acceleration = ((double)end.SpeedMetersPerSecond * end.SpeedMetersPerSecond
                - (double)start.SpeedMetersPerSecond * start.SpeedMetersPerSecond) / (2d * step.DistanceMeters);
            if (timeTo70 is null && start.SpeedMetersPerSecond < LongitudinalDynamics.StandingStartTelemetry70KphMetersPerSecond
                && end.SpeedMetersPerSecond >= LongitudinalDynamics.StandingStartTelemetry70KphMetersPerSecond)
                timeTo70 = (float)(start.ElapsedTimeSeconds
                    + (LongitudinalDynamics.StandingStartTelemetry70KphMetersPerSecond - start.SpeedMetersPerSecond) / acceleration);
            if (speedAtTwo is null && start.ElapsedTimeSeconds <= 2f && end.ElapsedTimeSeconds >= 2f)
                speedAtTwo = (float)Math.Clamp(start.SpeedMetersPerSecond + acceleration * (2d - start.ElapsedTimeSeconds),
                    Math.Min(start.SpeedMetersPerSecond, end.SpeedMetersPerSecond), Math.Max(start.SpeedMetersPerSecond, end.SpeedMetersPerSecond));
        }
        return new(ReactionTimeSeconds, MovementTimeSeconds, TravelTimeSeconds, ExitSpeedMetersPerSecond,
            PeakSpeedMetersPerSecond, straight.AccelerationDistanceMeters, straight.CruiseDistanceMeters,
            straight.DecelerationDistanceMeters, LongitudinalDynamics.CalculateNetDriveAccelerationMetersPerSecondSquared(
                0f, Steps[0].ReferenceDriveForceNewtons, setup), timeTo70, speedAtTwo, FullDriveEquilibriumSpeedMetersPerSecond);
    }

    internal ContinuousCornerTraversalProfile CornerProfile()
    {
        var first = Nodes[0]; var last = Nodes[^1];
        var peak = Nodes.MaxBy(n => n.SpeedMetersPerSecond)!;
        var minimum = Nodes.MinBy(n => n.SpeedMetersPerSecond)!;
        var correction = Steps.Sum(s => s.CorrectionDistanceMeters);
        var carry = Steps.Sum(s => s.CarryDistanceMeters);
        var drive = Steps.Sum(s => s.DriveDistanceMeters);
        return new(first.SpeedMetersPerSecond, last.SpeedMetersPerSecond, peak.SpeedMetersPerSecond,
            peak.CornerProgress!.Value, minimum.SpeedMetersPerSecond, minimum.CornerProgress!.Value,
            MovementTimeSeconds, correction, carry, drive,
            Steps.Where((_, i) => Nodes[i + 1].SpeedMetersPerSecond < Nodes[i].SpeedMetersPerSecond).Sum(s => s.DistanceMeters),
            Steps.Sum(s => s.CorrectionTimeSeconds), Steps.Sum(s => s.CarryTimeSeconds), Steps.Sum(s => s.DriveTimeSeconds),
            first.EnvelopeSpeedMetersPerSecond!.Value, last.EnvelopeSpeedMetersPerSecond!.Value,
            (ApexNode ?? last).LocalSafeSpeedMetersPerSecond!.Value, ContinuousCornerEnvelope.ApexProgress,
            MathF.Max(0f, last.SpeedMetersPerSecond - last.EnvelopeSpeedMetersPerSecond.Value),
            last.SpeedMetersPerSecond <= last.EnvelopeSpeedMetersPerSecond.Value,
            FullDriveEquilibriumSpeedMetersPerSecond, new ContinuousCornerNodes(Nodes.Select(n => new ContinuousCornerNode(
                n.CornerProgress!.Value, n.SpeedMetersPerSecond, n.EnvelopeSpeedMetersPerSecond!.Value,
                ContinuousCornerEnvelope.DriveAvailability(n.CornerProgress.Value),
                n.NetDriveAccelerationMetersPerSecondSquared) { ElapsedTimeSeconds = n.ElapsedTimeSeconds })));
    }
}
