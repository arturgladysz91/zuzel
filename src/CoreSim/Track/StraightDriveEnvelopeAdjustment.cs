namespace CoreSim;

/// <summary>
/// Calibration-only adjustment to the ordinary advanced Straight drive envelope.
/// It is deliberately internal and can only enter production resolution through
/// the internal HeatSimulationOptions hook used by CoreSim.Analysis.
/// </summary>
internal readonly record struct StraightDriveEnvelopeAdjustment
{
    internal float LowSpeedSuppression { get; }
    internal float HighSpeedRetention { get; }

    internal bool IsProductionBaseline =>
        LowSpeedSuppression == 0f && HighSpeedRetention == 0f;

    internal StraightDriveEnvelopeAdjustment(
        float lowSpeedSuppression,
        float highSpeedRetention)
    {
        if (!float.IsFinite(lowSpeedSuppression) || lowSpeedSuppression < 0f)
            throw new ArgumentOutOfRangeException(nameof(lowSpeedSuppression));
        if (!float.IsFinite(highSpeedRetention) || highSpeedRetention < 0f)
            throw new ArgumentOutOfRangeException(nameof(highSpeedRetention));

        LowSpeedSuppression = lowSpeedSuppression;
        HighSpeedRetention = highSpeedRetention;
    }

    internal float Delta(float speedMetersPerSecond)
    {
        if (!float.IsFinite(speedMetersPerSecond) || speedMetersPerSecond < 0f)
            throw new ArgumentOutOfRangeException(nameof(speedMetersPerSecond));

        if (IsProductionBaseline)
            return 0f;
        if (speedMetersPerSecond <= LongitudinalDynamics.ProvisionalPositiveDriveReferenceSpeedMetersPerSecond
            || speedMetersPerSecond >= 40f)
            return 0f;
        if (speedMetersPerSecond < 22f)
            return Lerp(0f, -LowSpeedSuppression,
                SmoothStep((speedMetersPerSecond
                    - LongitudinalDynamics.ProvisionalPositiveDriveReferenceSpeedMetersPerSecond) / 6f));
        if (speedMetersPerSecond < 24f)
            return Lerp(-LowSpeedSuppression, 0f,
                SmoothStep((speedMetersPerSecond - 22f) / 2f));
        if (speedMetersPerSecond < 30f)
            return Lerp(0f, HighSpeedRetention,
                SmoothStep((speedMetersPerSecond - 24f) / 6f));

        return Lerp(HighSpeedRetention, 0f,
            SmoothStep((speedMetersPerSecond - 30f) / 10f));
    }

    private static float SmoothStep(float value)
    {
        var t = Math.Clamp(value, 0f, 1f);
        return t * t * (3f - 2f * t);
    }

    private static float Lerp(float from, float to, float t) => from + (to - from) * t;
}

internal readonly record struct StraightDriveStepObservation(
    float StartDistanceMeters,
    float EndDistanceMeters,
    float EntrySpeedMetersPerSecond,
    float PredictorSpeedMetersPerSecond,
    float MidpointSpeedMetersPerSecond,
    float FullDriveExitSpeedMetersPerSecond,
    float ExitSpeedMetersPerSecond,
    float BaselineEnvelopeMultiplier,
    float ExperimentalEnvelopeMultiplier,
    float AvailableDriveForceNewtons,
    float ResistanceForceNewtons,
    float NetAccelerationMetersPerSecondSquared,
    float? AllowedEndSpeedMetersPerSecond,
    float PreparationReachableSpeedMetersPerSecond,
    bool PreparationApplied);
