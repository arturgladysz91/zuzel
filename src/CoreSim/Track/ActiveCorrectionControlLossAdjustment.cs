namespace CoreSim;

/// <summary>
/// Calibration-only gameplay abstraction for rhythm/control energy lost while
/// the existing corner correction is active. This is not a tyre coefficient.
/// </summary>
internal readonly record struct ActiveCorrectionControlLossAdjustment
{
    internal float MaxControlLossFraction { get; }

    internal ActiveCorrectionControlLossAdjustment(float maxControlLossFraction)
    {
        if (!float.IsFinite(maxControlLossFraction)
            || maxControlLossFraction < 0f
            || maxControlLossFraction > 1f)
            throw new ArgumentOutOfRangeException(nameof(maxControlLossFraction),
                "Maximum control-loss fraction must be finite and in [0,1].");

        MaxControlLossFraction = maxControlLossFraction;
    }
}

/// <summary>
/// Pure diagnostic formulas for #44. TrackReading and RiderStyle deliberately
/// cannot enter this boundary; Adaptability only moderates difficult surfaces.
/// </summary>
internal static class ActiveCorrectionControlLoss
{
    internal static float ControlLoad(
        float requiredCorrectionDistanceMeters,
        float availableStepDistanceMeters)
    {
        if (!float.IsFinite(requiredCorrectionDistanceMeters)
            || requiredCorrectionDistanceMeters < 0f)
            throw new ArgumentOutOfRangeException(nameof(requiredCorrectionDistanceMeters));
        if (!float.IsFinite(availableStepDistanceMeters)
            || availableStepDistanceMeters <= 0f)
            throw new ArgumentOutOfRangeException(nameof(availableStepDistanceMeters));

        return Math.Clamp(requiredCorrectionDistanceMeters / availableStepDistanceMeters, 0f, 1f);
    }

    internal static float SurfaceChallenge(float effectiveGrip)
    {
        if (!float.IsFinite(effectiveGrip) || effectiveGrip < 0f || effectiveGrip > 1f)
            throw new ArgumentOutOfRangeException(nameof(effectiveGrip));
        return Math.Clamp((.90f - effectiveGrip) / .30f, 0f, 1f);
    }

    internal static float SurfaceAdaptationPenalty(float effectiveGrip, float adaptability)
    {
        if (!float.IsFinite(adaptability) || adaptability < 0f || adaptability > 100f)
            throw new ArgumentOutOfRangeException(nameof(adaptability));
        return SurfaceChallenge(effectiveGrip) * (1f - adaptability / 100f);
    }

    internal static float ControlLossPressure(
        float controlLoad,
        float surfaceAdaptationPenalty)
    {
        if (!float.IsFinite(controlLoad) || controlLoad < 0f || controlLoad > 1f)
            throw new ArgumentOutOfRangeException(nameof(controlLoad));
        if (!float.IsFinite(surfaceAdaptationPenalty)
            || surfaceAdaptationPenalty < 0f
            || surfaceAdaptationPenalty > 1f)
            throw new ArgumentOutOfRangeException(nameof(surfaceAdaptationPenalty));
        return controlLoad * (.75f + .25f * surfaceAdaptationPenalty);
    }

    internal static double CorrectionEnergyRemovedJoules(
        float entrySpeedMetersPerSecond,
        float productionCorrectionExitSpeedMetersPerSecond)
    {
        ValidateSpeed(entrySpeedMetersPerSecond, nameof(entrySpeedMetersPerSecond));
        ValidateSpeed(productionCorrectionExitSpeedMetersPerSecond,
            nameof(productionCorrectionExitSpeedMetersPerSecond));
        return .5d * LongitudinalDynamics.ProvisionalNominalSystemMassKilograms
            * Math.Max(0d,
                (double)entrySpeedMetersPerSecond * entrySpeedMetersPerSecond
                - (double)productionCorrectionExitSpeedMetersPerSecond
                * productionCorrectionExitSpeedMetersPerSecond);
    }

    internal static double ControlLossEnergyJoules(
        double correctionEnergyRemovedJoules,
        ActiveCorrectionControlLossAdjustment adjustment,
        float controlLossPressure)
    {
        if (!double.IsFinite(correctionEnergyRemovedJoules)
            || correctionEnergyRemovedJoules < 0d)
            throw new ArgumentOutOfRangeException(nameof(correctionEnergyRemovedJoules));
        if (!float.IsFinite(controlLossPressure)
            || controlLossPressure < 0f
            || controlLossPressure > 1f)
            throw new ArgumentOutOfRangeException(nameof(controlLossPressure));
        return correctionEnergyRemovedJoules
            * adjustment.MaxControlLossFraction
            * controlLossPressure;
    }

    internal static float ApplyEnergyLoss(
        float productionCorrectionExitSpeedMetersPerSecond,
        double controlLossEnergyJoules)
    {
        ValidateSpeed(productionCorrectionExitSpeedMetersPerSecond,
            nameof(productionCorrectionExitSpeedMetersPerSecond));
        if (!double.IsFinite(controlLossEnergyJoules) || controlLossEnergyJoules < 0d)
            throw new ArgumentOutOfRangeException(nameof(controlLossEnergyJoules));
        if (controlLossEnergyJoules == 0d)
            return productionCorrectionExitSpeedMetersPerSecond;

        var squared = (double)productionCorrectionExitSpeedMetersPerSecond
            * productionCorrectionExitSpeedMetersPerSecond
            - 2d * controlLossEnergyJoules
            / LongitudinalDynamics.ProvisionalNominalSystemMassKilograms;
        return (float)Math.Sqrt(Math.Max(0d, squared));
    }

    private static void ValidateSpeed(float value, string name)
    {
        if (!float.IsFinite(value) || value < 0f)
            throw new ArgumentOutOfRangeException(name);
    }
}
