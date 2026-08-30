using CoreSim.Setup;

namespace CoreSim;

/// <summary>
/// Deterministic longitudinal motion helpers. The current acceleration values
/// are provisional first-model parameters, not final motorcycle performance data.
/// </summary>
public static class LongitudinalDynamics
{
    public const float MinTurnExitAccelerationMetersPerSecondSquared = 0.60f;
    public const float MaxTurnExitAccelerationMetersPerSecondSquared = 1.40f;
    public const float LowGearingDriveMultiplier = 1.10f;
    public const float HighGearingDriveMultiplier = 0.90f;
    public const float MinSurfaceDriveMultiplier = 0.75f;
    public const float SurfaceDriveMultiplierRange = 0.25f;

    public static float AccelerateOverDistance(
        float initialSpeedMetersPerSecond,
        float accelerationMetersPerSecondSquared,
        float distanceMeters)
    {
        ValidateNonNegativeFinite(initialSpeedMetersPerSecond, nameof(initialSpeedMetersPerSecond));
        ValidateNonNegativeFinite(
            accelerationMetersPerSecondSquared,
            nameof(accelerationMetersPerSecondSquared));
        ValidateNonNegativeFinite(distanceMeters, nameof(distanceMeters));

        if (accelerationMetersPerSecondSquared == 0f || distanceMeters == 0f)
            return initialSpeedMetersPerSecond;

        var finalSpeedSquared = (double)initialSpeedMetersPerSecond * initialSpeedMetersPerSecond
            + 2d * accelerationMetersPerSecondSquared * distanceMeters;
        var finalSpeed = (float)Math.Sqrt(finalSpeedSquared);
        if (!float.IsFinite(finalSpeed))
            throw new OverflowException("Final speed exceeds the finite single-precision domain.");
        return finalSpeed;
    }

    public static float CalculateTurnExitAccelerationMetersPerSecondSquared(
        RiderSkills skills,
        BikeSetup setup,
        TrackSurfaceState surface)
    {
        ArgumentNullException.ThrowIfNull(skills);
        ArgumentNullException.ThrowIfNull(setup);

        var speedSkill = RiderSkills.Normalize(skills.Speed);
        var baseAcceleration = MinTurnExitAccelerationMetersPerSecondSquared
            + (MaxTurnExitAccelerationMetersPerSecondSquared
               - MinTurnExitAccelerationMetersPerSecondSquared) * speedSkill;
        var gearingDriveMultiplier = LowGearingDriveMultiplier
            + (HighGearingDriveMultiplier - LowGearingDriveMultiplier) * setup.Gearing;
        var surfaceDriveMultiplier = MinSurfaceDriveMultiplier
            + SurfaceDriveMultiplierRange * surface.EffectiveGrip;
        var acceleration = baseAcceleration * gearingDriveMultiplier * surfaceDriveMultiplier;
        ValidateNonNegativeFinite(acceleration, "result");
        return acceleration;
    }

    private static void ValidateNonNegativeFinite(float value, string parameterName)
    {
        if (!float.IsFinite(value) || value < 0f)
        {
            throw new ArgumentOutOfRangeException(
                parameterName,
                value,
                "Value must be finite and non-negative.");
        }
    }
}
