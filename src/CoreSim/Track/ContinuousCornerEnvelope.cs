using CoreSim.Setup;

namespace CoreSim;

/// <summary>
/// One geometric speed domain for a complete logical corner. No segment label
/// selects a longitudinal phase. Surface and lateral geometry are entry samples.
/// </summary>
public sealed class ContinuousCornerEnvelope
{
    // Provisional geometry assumption, not inferred from telemetry.
    public const float ApexProgress = 0.50f;
    // Behavior-derived exposure: integral availability over [0,1] is 1/3.
    public const float FullDriveProgress = 5f / 6f;

    public float ApexSpeedMetersPerSecond { get; }
    public float TotalLengthMeters { get; }
    public float CorrectionCapabilityMetersPerSecondSquared { get; }
    public float FullDriveReferenceForceNewtons { get; }
    public BikeSetup Setup { get; }
    private readonly List<float> postApexSpeeds;

    public ContinuousCornerEnvelope(float apexSpeedMetersPerSecond, float totalLengthMeters,
        float correctionCapabilityMetersPerSecondSquared, float fullDriveReferenceForceNewtons, BikeSetup setup)
    {
        RequirePositive(apexSpeedMetersPerSecond, nameof(apexSpeedMetersPerSecond));
        RequirePositive(totalLengthMeters, nameof(totalLengthMeters));
        RequirePositive(correctionCapabilityMetersPerSecondSquared, nameof(correctionCapabilityMetersPerSecondSquared));
        RequirePositive(fullDriveReferenceForceNewtons, nameof(fullDriveReferenceForceNewtons));
        ArgumentNullException.ThrowIfNull(setup);
        ApexSpeedMetersPerSecond = apexSpeedMetersPerSecond;
        TotalLengthMeters = totalLengthMeters;
        CorrectionCapabilityMetersPerSecondSquared = correctionCapabilityMetersPerSecondSquared;
        FullDriveReferenceForceNewtons = fullDriveReferenceForceNewtons;
        Setup = setup;
        postApexSpeeds = new List<float> { apexSpeedMetersPerSecond };
    }

    public static ContinuousCornerEnvelope Create(CornerPhaseContext phase, float lateralPosition,
        TrackGeometry geometry, TrackSurfaceState surface, RiderSkills skills, BikeSetup setup)
        => new(SegmentPhysics.MaxSafeTurnSpeed(lateralPosition, geometry, surface, skills, setup),
            phase.TotalCornerLengthMeters,
            LongitudinalDynamics.CalculateCornerCorrectionDecelerationMetersPerSecondSquared(skills, surface),
            LongitudinalDynamics.CalculateTurnExitAvailableDriveForceNewtons(skills, setup, surface), setup);

    public static float DriveAvailability(float progress)
    {
        ValidateProgress(progress);
        var t = Math.Clamp((progress - ApexProgress) / (FullDriveProgress - ApexProgress), 0f, 1f);
        return t * t * (3f - 2f * t);
    }

    public float NetDriveAccelerationMetersPerSecondSquared(float speed, float progress)
        => DriveAvailability(progress) * LongitudinalDynamics.CalculateNetDriveAccelerationMetersPerSecondSquared(
            speed, FullDriveReferenceForceNewtons, Setup);

    public float SpeedMetersPerSecond(float progress)
    {
        ValidateProgress(progress);
        if (progress <= ApexProgress)
            return (float)Math.Sqrt((double)ApexSpeedMetersPerSecond * ApexSpeedMetersPerSecond
                + 2d * CorrectionCapabilityMetersPerSecondSquared * (ApexProgress - progress) * TotalLengthMeters);

        // Post-apex reachability uses the same signed force, midpoint and 1 m
        // distance resolution as production traversal, anchored to the apex.
        double end = (progress - ApexProgress) * (double)TotalLengthMeters;
        var resolution = LongitudinalDynamics.ProvisionalLongitudinalIntegrationStepMeters;
        var completeSteps = checked((int)Math.Floor(end / resolution));
        // Memoize the canonical apex-anchored full metres. This is a cache of
        // the same integration, not another physical model or a speed table fit.
        lock (postApexSpeeds)
        {
            while (postApexSpeeds.Count <= completeSteps)
            {
                var midpoint = (float)(ApexProgress + (postApexSpeeds.Count - .5d) * resolution / TotalLengthMeters);
                postApexSpeeds.Add(LongitudinalDynamics.CalculateMidpointDriveEndSpeedMetersPerSecond(
                    postApexSpeeds[^1], resolution, FullDriveReferenceForceNewtons, Setup, DriveAvailability(midpoint)));
            }
            var speed = postApexSpeeds[completeSteps];
            var remainder = end - completeSteps * (double)resolution;
            if (remainder > 0d)
            {
                var midpoint = (float)(ApexProgress + (completeSteps * (double)resolution + remainder * .5d) / TotalLengthMeters);
                speed = LongitudinalDynamics.CalculateMidpointDriveEndSpeedMetersPerSecond(
                    speed, (float)remainder, FullDriveReferenceForceNewtons, Setup, DriveAvailability(midpoint));
            }
            return speed;
        }
    }

    public ContinuousCornerTraversalProfile Traverse(float entrySpeedMetersPerSecond, float startProgress,
        float distanceMeters, bool allowDrive = true, bool allowCorrection = true,
        float retainedOverspeedMetersPerSecond = 0f)
    {
        RequirePositive(entrySpeedMetersPerSecond, nameof(entrySpeedMetersPerSecond));
        ValidateProgress(startProgress);
        if (!float.IsFinite(distanceMeters) || distanceMeters < 0f
            || distanceMeters > (1f - startProgress) * TotalLengthMeters + TotalLengthMeters * 1e-6f)
            throw new ArgumentOutOfRangeException(nameof(distanceMeters));
        if (!float.IsFinite(retainedOverspeedMetersPerSecond) || retainedOverspeedMetersPerSecond < 0f)
            throw new ArgumentOutOfRangeException(nameof(retainedOverspeedMetersPerSecond));
        double travelled = 0d, correctionDistance = 0d, carryDistance = 0d, driveDistance = 0d, time = 0d;
        double correctionTime = 0d, carryTime = 0d, driveTime = 0d, naturalDecelerationDistance = 0d;
        var speed = entrySpeedMetersPerSecond;
        var peak = speed;
        var minimum = speed;
        var peakProgress = startProgress;
        var minimumProgress = startProgress;
        var startDistance = (double)startProgress * TotalLengthMeters;
        var nodes = new List<ContinuousCornerNode> { Node(startProgress, speed) };
        while (travelled < distanceMeters)
        {
            // Split at the apex: a metre may never straddle neutral/drive regions.
            var absolute = startDistance + travelled;
            var step = Math.Min(LongitudinalDynamics.ProvisionalLongitudinalIntegrationStepMeters, distanceMeters - travelled);
            var apexDistance = ApexProgress * (double)TotalLengthMeters;
            if (absolute < apexDistance && absolute + step > apexDistance)
                step = apexDistance - absolute;
            var nextProgress = (float)Math.Clamp((absolute + step) / TotalLengthMeters, 0d, 1d);
            var midpoint = (float)Math.Clamp((absolute + step * 0.5d) / TotalLengthMeters, 0d, 1d);
            var target = SpeedMetersPerSecond(nextProgress) + retainedOverspeedMetersPerSecond;
            var availability = allowDrive ? DriveAvailability(midpoint) : 0f;
            var candidate = LongitudinalDynamics.CalculateMidpointDriveEndSpeedMetersPerSecond(
                speed, (float)step, FullDriveReferenceForceNewtons, Setup, availability);
            float nextSpeed;
            if (allowCorrection && speed > target)
            {
                var correction = LongitudinalDynamics.CalculateCornerSpeedCorrectionProfile(
                    speed, target, CorrectionCapabilityMetersPerSecondSquared, (float)step);
                nextSpeed = correction.ExitSpeedMetersPerSecond;
                var carry = Math.Max(0d, step - correction.CorrectionDistanceMeters);
                var carrySeconds = carry / nextSpeed;
                correctionDistance += correction.CorrectionDistanceMeters;
                carryDistance += carry;
                correctionTime += correction.TravelTimeSeconds;
                carryTime += carrySeconds;
                time += correction.TravelTimeSeconds + carrySeconds;
            }
            else
            {
                // A rising envelope is an upper constraint, never an instruction
                // to increase speed. Signed drive may naturally decelerate.
                nextSpeed = candidate;
                var seconds = 2d * step / (speed + nextSpeed);
                if (availability > 0f)
                {
                    driveDistance += step;
                    driveTime += seconds;
                    if (nextSpeed < speed) naturalDecelerationDistance += step;
                }
                else { carryDistance += step; carryTime += seconds; }
                time += seconds;
            }
            travelled += step;
            speed = nextSpeed;
            if (speed > peak) { peak = speed; peakProgress = nextProgress; }
            if (speed < minimum) { minimum = speed; minimumProgress = nextProgress; }
            nodes.Add(Node(nextProgress, speed));
        }
        var endProgress = (float)Math.Clamp(startProgress + (double)distanceMeters / TotalLengthMeters, 0d, 1d);
        var endEnvelope = SpeedMetersPerSecond(endProgress);
        var residual = MathF.Max(0f, speed - endEnvelope - retainedOverspeedMetersPerSecond);
        return new(entrySpeedMetersPerSecond, speed, peak, peakProgress, minimum, minimumProgress,
            (float)time, (float)correctionDistance, (float)carryDistance, (float)driveDistance,
            (float)(correctionDistance + naturalDecelerationDistance), (float)correctionTime,
            (float)carryTime, (float)driveTime, SpeedMetersPerSecond(startProgress), endEnvelope,
            ApexSpeedMetersPerSecond, ApexProgress, residual, residual <= 1e-5f,
            LongitudinalDynamics.CalculateFullDriveEquilibriumSpeedMetersPerSecond(FullDriveReferenceForceNewtons, Setup),
            new ContinuousCornerNodes(nodes));

        ContinuousCornerNode Node(float p, float v) => new(p, v, SpeedMetersPerSecond(p),
            DriveAvailability(p), NetDriveAccelerationMetersPerSecondSquared(v, p));
    }

    private static void ValidateProgress(float value)
    {
        if (!float.IsFinite(value) || value < 0f || value > 1f)
            throw new ArgumentOutOfRangeException(nameof(value), "Corner progress must be finite and in [0,1].");
    }

    private static void RequirePositive(float value, string name)
    {
        if (!float.IsFinite(value) || value <= 0f) throw new ArgumentOutOfRangeException(name);
    }
}

public sealed record ContinuousCornerNode(float CornerProgress, float SpeedMetersPerSecond,
    float EnvelopeSpeedMetersPerSecond, float DriveAvailability, float NetDriveAccelerationMetersPerSecondSquared);

/// <summary>Immutable observations with value equality, including in diagnostic records.</summary>
public sealed class ContinuousCornerNodes(IEnumerable<ContinuousCornerNode> nodes)
    : IReadOnlyList<ContinuousCornerNode>, IEquatable<ContinuousCornerNodes>
{
    private readonly ContinuousCornerNode[] values = nodes.ToArray();
    public int Count => values.Length;
    public ContinuousCornerNode this[int index] => values[index];
    public IEnumerator<ContinuousCornerNode> GetEnumerator()
        => ((IEnumerable<ContinuousCornerNode>)values).GetEnumerator();
    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
    public bool Equals(ContinuousCornerNodes? other)
        => other is not null && values.SequenceEqual(other.values);
    public override bool Equals(object? obj) => obj is ContinuousCornerNodes other && Equals(other);
    public override int GetHashCode()
    {
        var hash = new HashCode();
        foreach (var node in values) hash.Add(node);
        return hash.ToHashCode(); // Collection identity only; never used for domain randomness.
    }
}

/// <summary>Distance buckets are disjoint: correction + carry + drive = travelled.
/// DecelerationDistance also observes negative signed drive and must not be added again.</summary>
public sealed record ContinuousCornerTraversalProfile(
    float EntrySpeedMetersPerSecond, float ExitSpeedMetersPerSecond, float PeakSpeedMetersPerSecond,
    float PeakCornerProgress, float MinimumSpeedMetersPerSecond, float MinimumSpeedCornerProgress,
    float TravelTimeSeconds, float CorrectionDistanceMeters, float CarryDistanceMeters, float DriveDistanceMeters,
    float DecelerationDistanceMeters, float CorrectionTimeSeconds, float CarryTimeSeconds, float DriveTimeSeconds,
    float EnvelopeAtEntryMetersPerSecond, float EnvelopeAtExitMetersPerSecond,
    float ApexSpeedMetersPerSecond, float ApexProgress, float ResidualOverspeedMetersPerSecond, bool TargetReached,
    float FullDriveEquilibriumSpeedMetersPerSecond, ContinuousCornerNodes Nodes);
