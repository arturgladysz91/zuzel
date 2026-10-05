namespace CoreSim;

/// <summary>One-way migration baseline, not empirical equivalence or a physics conversion.</summary>
public static class RiderAbilityCompatibility
{
    /// <summary>Uncalibrated legacy/default fixture only; never used by current physics.</summary>
    public const float LegacyCompatibilityMassKg = 70f;

    public static RiderGameplayProfile FromLegacy(RiderSkills skills, RiderStyle style)
        => new(ToAbilities(skills), new RiderPhysicalProfile(LegacyCompatibilityMassKg), ToInteractionStyle(style));

    public static RiderAbilities ToAbilities(RiderSkills skills)
    {
        ArgumentNullException.ThrowIfNull(skills);
        // Speed and Adaptability intentionally have no canonical mapping.
        return new(Convert(skills.Start), Convert(skills.Start), Convert(skills.SlideControl),
            Convert(skills.TrackReading), 50, 50, Convert(skills.PairRiding), 50);
    }

    public static RiderInteractionStyle ToInteractionStyle(RiderStyle style)
    {
        ArgumentNullException.ThrowIfNull(style);
        if (!float.IsFinite(style.OutsidePreference))
            throw new ArgumentOutOfRangeException(nameof(style), "Legacy outside preference must be finite.");
        var line = style.OutsidePreference < 1f / 3f ? PreferredLine.Inside
            : style.OutsidePreference > 2f / 3f ? PreferredLine.Outside : PreferredLine.Neutral;
        // RiskTolerance and LaneChangeTendency intentionally remain unmapped.
        return new(.5f, line, style.SetupIndependence);
    }

    private static int Convert(float value)
    {
        if (!float.IsFinite(value))
            throw new ArgumentOutOfRangeException(nameof(value), "Mapped legacy skills must be finite.");
        // Deterministic round-to-nearest, midpoint away from zero, then clamp.
        return (int)Math.Clamp(MathF.Round(value, MidpointRounding.AwayFromZero), 1f, 99f);
    }
}
