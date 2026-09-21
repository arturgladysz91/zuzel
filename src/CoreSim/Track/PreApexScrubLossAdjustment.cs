namespace CoreSim;

/// <summary>
/// Calibration-only diagnostic pre-apex energy loss. The value is a peak
/// deceleration, not a tyre coefficient or a gameplay tuning surface.
/// </summary>
internal readonly record struct PreApexScrubLossAdjustment
{
    internal float PeakScrubDecelerationMetersPerSecondSquared { get; }
    internal bool IsProductionBaseline => PeakScrubDecelerationMetersPerSecondSquared == 0f;

    internal PreApexScrubLossAdjustment(float peakScrubDecelerationMetersPerSecondSquared)
    {
        if (!float.IsFinite(peakScrubDecelerationMetersPerSecondSquared)
            || peakScrubDecelerationMetersPerSecondSquared < 0f)
            throw new ArgumentOutOfRangeException(nameof(peakScrubDecelerationMetersPerSecondSquared),
                "Peak scrub deceleration must be finite and non-negative.");

        PeakScrubDecelerationMetersPerSecondSquared = peakScrubDecelerationMetersPerSecondSquared;
    }

    internal static float Window(float progress)
    {
        if (!float.IsFinite(progress) || progress < 0f || progress > 1f)
            throw new ArgumentOutOfRangeException(nameof(progress),
                "Corner progress must be finite and in [0,1].");
        if (progress <= .10f || progress >= .50f) return 0f;
        if (progress < .30f) return SmoothStep((progress - .10f) / .20f);
        if (progress <= .40f) return 1f;
        return 1f - SmoothStep((progress - .40f) / .10f);
    }

    private static float SmoothStep(float value) => value * value * (3f - 2f * value);
}
