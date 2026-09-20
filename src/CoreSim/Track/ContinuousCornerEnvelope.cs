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
        => TraverseWithReducedDriveResistanceExposure(entrySpeedMetersPerSecond, startProgress,
            distanceMeters, 0f, allowDrive, allowCorrection, retainedOverspeedMetersPerSecond);

    internal ContinuousCornerTraversalProfile TraverseWithReducedDriveResistanceExposure(
        float entrySpeedMetersPerSecond,
        float startProgress,
        float distanceMeters,
        float reducedDriveResistanceExposure,
        bool allowDrive = true,
        bool allowCorrection = true,
        float retainedOverspeedMetersPerSecond = 0f)
    {
        RequirePositive(entrySpeedMetersPerSecond, nameof(entrySpeedMetersPerSecond));
        ValidateProgress(startProgress);
        if (!float.IsFinite(distanceMeters) || distanceMeters < 0f
            || distanceMeters > (1f - startProgress) * TotalLengthMeters + TotalLengthMeters * 1e-6f)
            throw new ArgumentOutOfRangeException(nameof(distanceMeters));
        if (!float.IsFinite(retainedOverspeedMetersPerSecond) || retainedOverspeedMetersPerSecond < 0f)
            throw new ArgumentOutOfRangeException(nameof(retainedOverspeedMetersPerSecond));
        if (!float.IsFinite(reducedDriveResistanceExposure)
            || reducedDriveResistanceExposure < 0f
            || reducedDriveResistanceExposure > 1f)
            throw new ArgumentOutOfRangeException(nameof(reducedDriveResistanceExposure));
        double travelled = 0d, correctionDistance = 0d, carryDistance = 0d, driveDistance = 0d, time = 0d;
        double correctionTime = 0d, carryTime = 0d, driveTime = 0d, naturalDecelerationDistance = 0d;
        double passiveResistanceDistance = 0d, positiveDriveDistance = 0d;
        double negativeSignedDriveDistance = 0d, neutralCarryDistance = 0d;
        double zeroAvailabilityUncorrectedDistance = 0d;
        var speed = entrySpeedMetersPerSecond;
        var peak = speed;
        var minimum = speed;
        var peakProgress = startProgress;
        var minimumProgress = startProgress;
        var startDistance = (double)startProgress * TotalLengthMeters;
        var nodes = new List<ContinuousCornerNode>
        {
            Node(startProgress, speed, 0f, ContinuousCornerPhaseClassification.CarryPassive),
        };
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
            var candidate = CalculateExperimentalEndSpeedMetersPerSecond(
                speed, (float)step, availability, reducedDriveResistanceExposure);
            float nextSpeed;
            ContinuousCornerPhaseClassification phaseClassification;
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
                neutralCarryDistance += carry;
                if (availability == 0f)
                    zeroAvailabilityUncorrectedDistance += carry;
                phaseClassification = ContinuousCornerPhaseClassification.Correction;
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
                    if (nextSpeed < speed)
                    {
                        naturalDecelerationDistance += step;
                        negativeSignedDriveDistance += step;
                        phaseClassification = ContinuousCornerPhaseClassification.SignedDriveDeceleration;
                    }
                    else if (nextSpeed > speed)
                    {
                        positiveDriveDistance += step;
                        phaseClassification = ContinuousCornerPhaseClassification.SignedDriveAcceleration;
                    }
                    else
                    {
                        neutralCarryDistance += step;
                        phaseClassification = ContinuousCornerPhaseClassification.CarryPassive;
                    }
                }
                else
                {
                    carryDistance += step;
                    carryTime += seconds;
                    zeroAvailabilityUncorrectedDistance += step;
                    if (nextSpeed < speed)
                    {
                        passiveResistanceDistance += step;
                        naturalDecelerationDistance += step;
                    }
                    else
                    {
                        neutralCarryDistance += step;
                    }
                    phaseClassification = ContinuousCornerPhaseClassification.CarryPassive;
                }
                time += seconds;
            }
            travelled += step;
            speed = nextSpeed;
            if (speed > peak) { peak = speed; peakProgress = nextProgress; }
            if (speed < minimum) { minimum = speed; minimumProgress = nextProgress; }
            nodes.Add(Node(nextProgress, speed, (float)time, phaseClassification));
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
            new ContinuousCornerNodes(nodes))
        {
            PassiveResistanceDistanceMeters = (float)passiveResistanceDistance,
            PositiveDriveDistanceMeters = (float)positiveDriveDistance,
            NegativeSignedDriveDistanceMeters = (float)negativeSignedDriveDistance,
            NeutralCarryDistanceMeters = (float)neutralCarryDistance,
            ZeroAvailabilityUncorrectedDistanceMeters = (float)zeroAvailabilityUncorrectedDistance,
            ReducedDriveResistanceExposure = reducedDriveResistanceExposure,
        };

        ContinuousCornerNode Node(float p, float v, float elapsed,
            ContinuousCornerPhaseClassification classification)
        {
            var observation = ObserveReducedDriveForce(v, DriveAvailability(p), reducedDriveResistanceExposure);
            return new ContinuousCornerNode(p, v, SpeedMetersPerSecond(p),
                DriveAvailability(p), observation.NetAccelerationMetersPerSecondSquared)
            {
                ElapsedTimeSeconds = elapsed,
                AvailableDriveForceNewtons = observation.AvailableDriveForceNewtons,
                ResistanceForceNewtons = observation.ResistanceForceNewtons,
                ExposedResistanceForceNewtons = observation.ExposedResistanceForceNewtons,
                NetForceNewtons = observation.NetForceNewtons,
                PhaseClassification = classification,
            };
        }
    }

    private float CalculateExperimentalEndSpeedMetersPerSecond(
        float currentSpeedMetersPerSecond,
        float stepDistanceMeters,
        float driveAvailability,
        float resistanceExposure)
    {
        // This exact branch preserves the reviewed production operation order
        // and float results for null/R0, and for full drive in every candidate.
        if (resistanceExposure == 0f || driveAvailability == 1f)
        {
            return LongitudinalDynamics.CalculateMidpointDriveEndSpeedMetersPerSecond(
                currentSpeedMetersPerSecond,
                stepDistanceMeters,
                FullDriveReferenceForceNewtons,
                Setup,
                driveAvailability);
        }

        var atStart = ObserveReducedDriveForce(
            currentSpeedMetersPerSecond,
            driveAvailability,
            resistanceExposure);
        var predicted = LongitudinalDynamics.ApplySignedAccelerationOverDistance(
            currentSpeedMetersPerSecond,
            atStart.NetAccelerationMetersPerSecondSquared,
            stepDistanceMeters);
        var midpointSpeed = (float)(((double)currentSpeedMetersPerSecond + predicted) * .5d);
        var atMidpoint = ObserveReducedDriveForce(midpointSpeed, driveAvailability, resistanceExposure);
        return LongitudinalDynamics.ApplySignedAccelerationOverDistance(
            currentSpeedMetersPerSecond,
            atMidpoint.NetAccelerationMetersPerSecondSquared,
            stepDistanceMeters);
    }

    internal ReducedDriveForceObservation ObserveReducedDriveForce(
        float speedMetersPerSecond,
        float driveAvailability,
        float resistanceExposure)
    {
        var fullDriveForce = LongitudinalDynamics.CalculateAvailableDriveForceAtSpeedNewtons(
            FullDriveReferenceForceNewtons,
            speedMetersPerSecond,
            Setup);
        var availableDriveForce = (float)((double)driveAvailability * fullDriveForce);
        var resistanceForce = LongitudinalDynamics.CalculateLongitudinalResistanceForceNewtons(
            speedMetersPerSecond);
        var productionExposedResistance = (float)((double)driveAvailability * resistanceForce);
        var exposedResistance = resistanceExposure == 0f
            ? productionExposedResistance
            : (float)(productionExposedResistance
                + ((double)resistanceForce - productionExposedResistance) * resistanceExposure);
        var netForce = availableDriveForce - exposedResistance;
        var acceleration = netForce / LongitudinalDynamics.ProvisionalNominalSystemMassKilograms;
        return new ReducedDriveForceObservation(
            availableDriveForce,
            resistanceForce,
            exposedResistance,
            netForce,
            acceleration);
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

public enum ContinuousCornerPhaseClassification
{
    Correction,
    CarryPassive,
    SignedDriveAcceleration,
    SignedDriveDeceleration,
}

public sealed record ContinuousCornerNode(float CornerProgress, float SpeedMetersPerSecond,
    float EnvelopeSpeedMetersPerSecond, float DriveAvailability, float NetDriveAccelerationMetersPerSecondSquared)
{
    internal float ElapsedTimeSeconds { get; init; }
    internal float AvailableDriveForceNewtons { get; init; }
    internal float ResistanceForceNewtons { get; init; }
    internal float ExposedResistanceForceNewtons { get; init; }
    internal float NetForceNewtons { get; init; }
    internal ContinuousCornerPhaseClassification PhaseClassification { get; init; }
}

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
    float FullDriveEquilibriumSpeedMetersPerSecond, ContinuousCornerNodes Nodes)
{
    internal float PassiveResistanceDistanceMeters { get; init; }
    internal float PositiveDriveDistanceMeters { get; init; }
    internal float NegativeSignedDriveDistanceMeters { get; init; }
    internal float NeutralCarryDistanceMeters { get; init; }
    internal float ZeroAvailabilityUncorrectedDistanceMeters { get; init; }
    internal float ReducedDriveResistanceExposure { get; init; }
}

internal readonly record struct ReducedDriveForceObservation(
    float AvailableDriveForceNewtons,
    float ResistanceForceNewtons,
    float ExposedResistanceForceNewtons,
    float NetForceNewtons,
    float NetAccelerationMetersPerSecondSquared);
