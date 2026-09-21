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
        => TraverseCore(entrySpeedMetersPerSecond, startProgress, distanceMeters,
            reducedDriveResistanceExposure, null, allowDrive, allowCorrection,
            retainedOverspeedMetersPerSecond);

    internal ContinuousCornerTraversalProfile TraverseWithPreApexScrubLoss(
        float entrySpeedMetersPerSecond,
        float startProgress,
        float distanceMeters,
        PreApexScrubLossAdjustment adjustment,
        bool allowDrive = true,
        bool allowCorrection = true,
        float retainedOverspeedMetersPerSecond = 0f)
    {
        // An exact zero bypasses every #43 knot split and arithmetic operation.
        if (adjustment.IsProductionBaseline)
            return Traverse(entrySpeedMetersPerSecond, startProgress, distanceMeters,
                allowDrive, allowCorrection, retainedOverspeedMetersPerSecond);
        return TraverseCore(entrySpeedMetersPerSecond, startProgress, distanceMeters,
            0f, adjustment, allowDrive, allowCorrection, retainedOverspeedMetersPerSecond);
    }

    internal ContinuousCornerTraversalProfile TraverseWithZeroForcePreApexScrubLossExperimentPath(
        float entrySpeedMetersPerSecond,
        float startProgress,
        float distanceMeters,
        bool allowDrive = true,
        bool allowCorrection = true,
        float retainedOverspeedMetersPerSecond = 0f)
        => TraverseCore(entrySpeedMetersPerSecond, startProgress, distanceMeters,
            0f, new PreApexScrubLossAdjustment(0f), allowDrive, allowCorrection,
            retainedOverspeedMetersPerSecond);

    private ContinuousCornerTraversalProfile TraverseCore(
        float entrySpeedMetersPerSecond,
        float startProgress,
        float distanceMeters,
        float reducedDriveResistanceExposure,
        PreApexScrubLossAdjustment? scrubAdjustment,
        bool allowDrive,
        bool allowCorrection,
        float retainedOverspeedMetersPerSecond)
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
        double scrubDistance = 0d, scrubWhileDriveZeroDistance = 0d;
        double scrubWhileDrivePositiveDistance = 0d, scrubWork = 0d;
        float? firstCorrectionProgress = null, lastCorrectionProgress = null;
        float? firstScrubProgress = null, lastScrubProgress = null;
        float? firstScrubDistanceMeters = null;
        float correctionDistanceBeforeFirstScrubMeters = 0f;
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
            var appliedScrubWindow = 0f;
            var appliedScrubAcceleration = 0f;
            var appliedScrubDistance = 0f;
            if (allowCorrection && speed > target)
            {
                var correction = LongitudinalDynamics.CalculateCornerSpeedCorrectionProfile(
                    speed, target, CorrectionCapabilityMetersPerSecondSquared, (float)step);
                nextSpeed = correction.ExitSpeedMetersPerSecond;
                var carry = Math.Max(0d, step - correction.CorrectionDistanceMeters);
                var correctionEndProgress = (float)Math.Clamp(
                    (absolute + correction.CorrectionDistanceMeters) / TotalLengthMeters, 0d, 1d);
                firstCorrectionProgress ??= (float)(absolute / TotalLengthMeters);
                lastCorrectionProgress = correctionEndProgress;
                var carryAvailability = availability;
                var scrubWindow = 0f;
                var scrubAcceleration = 0f;
                if (carry > 0d && scrubAdjustment is { } activeScrub)
                {
                    var carryMidpointProgress = (float)Math.Clamp(
                        (absolute + correction.CorrectionDistanceMeters + carry * .5d) / TotalLengthMeters,
                        0d, 1d);
                    carryAvailability = allowDrive ? DriveAvailability(carryMidpointProgress) : 0f;
                    scrubWindow = PreApexScrubLossAdjustment.Window(carryMidpointProgress);
                    scrubAcceleration = activeScrub.PeakScrubDecelerationMetersPerSecondSquared
                        * scrubWindow;
                    if (scrubAcceleration > 0f)
                    {
                        nextSpeed = CalculateScrubEndSpeedMetersPerSecond(
                            nextSpeed, (float)carry, carryAvailability,
                            scrubAcceleration);
                        appliedScrubWindow = scrubWindow;
                        appliedScrubAcceleration = scrubAcceleration;
                        appliedScrubDistance = (float)carry;
                    }
                }
                var carrySeconds = carry == 0d ? 0d : 2d * carry / (correction.ExitSpeedMetersPerSecond + nextSpeed);
                correctionDistance += correction.CorrectionDistanceMeters;
                correctionTime += correction.TravelTimeSeconds;
                time += correction.TravelTimeSeconds + carrySeconds;
                if (carry > 0d && scrubAcceleration > 0f)
                {
                    scrubDistance += carry;
                    scrubWork += LongitudinalDynamics.ProvisionalNominalSystemMassKilograms
                        * scrubAcceleration * carry;
                    if (firstScrubProgress is null)
                    {
                        firstScrubProgress = correctionEndProgress;
                        firstScrubDistanceMeters = (float)carry;
                        correctionDistanceBeforeFirstScrubMeters = (float)correctionDistance;
                    }
                    lastScrubProgress = nextProgress;
                    if (carryAvailability > 0f)
                    {
                        driveDistance += carry;
                        driveTime += carrySeconds;
                        scrubWhileDrivePositiveDistance += carry;
                        phaseClassification = ContinuousCornerPhaseClassification.SignedDrivePlusScrub;
                    }
                    else
                    {
                        carryDistance += carry;
                        carryTime += carrySeconds;
                        zeroAvailabilityUncorrectedDistance += carry;
                        scrubWhileDriveZeroDistance += carry;
                        phaseClassification = ContinuousCornerPhaseClassification.ScrubOnly;
                    }
                    if (nextSpeed < correction.ExitSpeedMetersPerSecond)
                        naturalDecelerationDistance += carry;
                }
                else
                {
                    carryDistance += carry;
                    carryTime += carrySeconds;
                    neutralCarryDistance += carry;
                    if (carryAvailability == 0f)
                        zeroAvailabilityUncorrectedDistance += carry;
                    phaseClassification = ContinuousCornerPhaseClassification.Correction;
                }
            }
            else
            {
                // A rising envelope is an upper constraint, never an instruction
                // to increase speed. Signed drive may naturally decelerate.
                var scrubWindow = scrubAdjustment is null
                    ? 0f
                    : PreApexScrubLossAdjustment.Window(midpoint);
                var scrubAcceleration = scrubAdjustment is null
                    ? 0f
                    : scrubAdjustment.Value.PeakScrubDecelerationMetersPerSecondSquared * scrubWindow;
                nextSpeed = scrubAcceleration > 0f
                    ? CalculateScrubEndSpeedMetersPerSecond(
                        speed, (float)step, availability, scrubAcceleration)
                    : candidate;
                var seconds = 2d * step / (speed + nextSpeed);
                if (scrubAcceleration > 0f)
                {
                    appliedScrubWindow = scrubWindow;
                    appliedScrubAcceleration = scrubAcceleration;
                    appliedScrubDistance = (float)step;
                    scrubDistance += step;
                    scrubWork += LongitudinalDynamics.ProvisionalNominalSystemMassKilograms
                        * scrubAcceleration * step;
                    if (firstScrubProgress is null)
                    {
                        firstScrubProgress = (float)(absolute / TotalLengthMeters);
                        firstScrubDistanceMeters = (float)step;
                        correctionDistanceBeforeFirstScrubMeters = (float)correctionDistance;
                    }
                    lastScrubProgress = nextProgress;
                    if (availability > 0f)
                    {
                        driveDistance += step;
                        driveTime += seconds;
                        scrubWhileDrivePositiveDistance += step;
                        phaseClassification = ContinuousCornerPhaseClassification.SignedDrivePlusScrub;
                    }
                    else
                    {
                        carryDistance += step;
                        carryTime += seconds;
                        zeroAvailabilityUncorrectedDistance += step;
                        scrubWhileDriveZeroDistance += step;
                        phaseClassification = ContinuousCornerPhaseClassification.ScrubOnly;
                    }
                    if (nextSpeed < speed) naturalDecelerationDistance += step;
                }
                else if (availability > 0f)
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
            nodes.Add(Node(nextProgress, speed, (float)time, phaseClassification,
                appliedScrubWindow, appliedScrubAcceleration, appliedScrubDistance));
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
            ScrubDistanceMeters = (float)scrubDistance,
            ScrubWhileDriveZeroDistanceMeters = (float)scrubWhileDriveZeroDistance,
            ScrubWhileDrivePositiveDistanceMeters = (float)scrubWhileDrivePositiveDistance,
            ScrubWorkJoules = scrubWork,
            FirstCorrectionProgress = firstCorrectionProgress,
            LastCorrectionProgress = lastCorrectionProgress,
            FirstScrubProgress = firstScrubProgress,
            LastScrubProgress = lastScrubProgress,
            FirstScrubDistanceMeters = firstScrubDistanceMeters,
            CorrectionDistanceBeforeFirstScrubMeters = correctionDistanceBeforeFirstScrubMeters,
        };

        ContinuousCornerNode Node(float p, float v, float elapsed,
            ContinuousCornerPhaseClassification classification,
            float appliedScrubWindow = 0f,
            float appliedScrubAcceleration = 0f,
            float appliedScrubDistance = 0f)
        {
            var observation = ObserveReducedDriveForce(v, DriveAvailability(p), reducedDriveResistanceExposure);
            var scrubWindow = scrubAdjustment is null || scrubAdjustment.Value.IsProductionBaseline
                ? 0f
                : PreApexScrubLossAdjustment.Window(p);
            var scrubApplied = appliedScrubAcceleration > 0f && appliedScrubDistance > 0f;
            return new ContinuousCornerNode(p, v, SpeedMetersPerSecond(p),
                DriveAvailability(p), observation.NetAccelerationMetersPerSecondSquared)
            {
                ElapsedTimeSeconds = elapsed,
                AvailableDriveForceNewtons = observation.AvailableDriveForceNewtons,
                ResistanceForceNewtons = observation.ResistanceForceNewtons,
                ExposedResistanceForceNewtons = observation.ExposedResistanceForceNewtons,
                NetForceNewtons = observation.NetForceNewtons,
                PhaseClassification = classification,
                ScrubWindow = scrubWindow,
                AppliedScrubWindow = appliedScrubWindow,
                ScrubApplied = scrubApplied,
                ScrubAppliedDistanceMeters = appliedScrubDistance,
                ScrubAccelerationMetersPerSecondSquared = appliedScrubAcceleration,
                ScrubForceNewtons = LongitudinalDynamics.ProvisionalNominalSystemMassKilograms
                    * appliedScrubAcceleration,
                ProductionSignedDriveAccelerationMetersPerSecondSquared =
                    observation.NetAccelerationMetersPerSecondSquared,
                FinalAccelerationMetersPerSecondSquared =
                    observation.NetAccelerationMetersPerSecondSquared - appliedScrubAcceleration,
                EnvelopeApexSpeedMetersPerSecond = ApexSpeedMetersPerSecond,
                EnvelopeCorrectionCapabilityMetersPerSecondSquared =
                    CorrectionCapabilityMetersPerSecondSquared,
                EnvelopeTotalLengthMeters = TotalLengthMeters,
            };
        }

    }

    private float CalculateScrubEndSpeedMetersPerSecond(
        float currentSpeedMetersPerSecond,
        float stepDistanceMeters,
        float driveAvailability,
        float scrubDecelerationMetersPerSecondSquared)
    {
        var atStart = ObserveReducedDriveForce(currentSpeedMetersPerSecond, driveAvailability, 0f);
        var predicted = LongitudinalDynamics.ApplySignedAccelerationOverDistance(
            currentSpeedMetersPerSecond,
            atStart.NetAccelerationMetersPerSecondSquared - scrubDecelerationMetersPerSecondSquared,
            stepDistanceMeters);
        var midpointSpeed = (float)(((double)currentSpeedMetersPerSecond + predicted) * .5d);
        var atMidpoint = ObserveReducedDriveForce(midpointSpeed, driveAvailability, 0f);
        return LongitudinalDynamics.ApplySignedAccelerationOverDistance(
            currentSpeedMetersPerSecond,
            atMidpoint.NetAccelerationMetersPerSecondSquared - scrubDecelerationMetersPerSecondSquared,
            stepDistanceMeters);
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
    ScrubOnly,
    SignedDrivePlusScrub,
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
    internal float ScrubWindow { get; init; }
    internal float AppliedScrubWindow { get; init; }
    internal bool ScrubApplied { get; init; }
    internal float ScrubAppliedDistanceMeters { get; init; }
    internal float ScrubAccelerationMetersPerSecondSquared { get; init; }
    internal float ScrubForceNewtons { get; init; }
    internal float ProductionSignedDriveAccelerationMetersPerSecondSquared { get; init; }
    internal float FinalAccelerationMetersPerSecondSquared { get; init; }
    internal float EnvelopeApexSpeedMetersPerSecond { get; init; }
    internal float EnvelopeCorrectionCapabilityMetersPerSecondSquared { get; init; }
    internal float EnvelopeTotalLengthMeters { get; init; }
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
    internal float ScrubDistanceMeters { get; init; }
    internal float ScrubWhileDriveZeroDistanceMeters { get; init; }
    internal float ScrubWhileDrivePositiveDistanceMeters { get; init; }
    internal double ScrubWorkJoules { get; init; }
    internal float? FirstCorrectionProgress { get; init; }
    internal float? LastCorrectionProgress { get; init; }
    internal float? FirstScrubProgress { get; init; }
    internal float? LastScrubProgress { get; init; }
    internal float? FirstScrubDistanceMeters { get; init; }
    internal float CorrectionDistanceBeforeFirstScrubMeters { get; init; }
}

internal readonly record struct ReducedDriveForceObservation(
    float AvailableDriveForceNewtons,
    float ResistanceForceNewtons,
    float ExposedResistanceForceNewtons,
    float NetForceNewtons,
    float NetAccelerationMetersPerSecondSquared);
