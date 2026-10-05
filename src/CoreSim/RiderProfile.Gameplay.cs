using System.Text.Json.Serialization;

namespace CoreSim;

public sealed partial record RiderProfile
{
    private RiderGameplayProfile? ExplicitGameplay { get; init; }

    /// <summary>Explicit canonical data, or a one-way fallback for an old profile.</summary>
    [JsonIgnore]
    public RiderGameplayProfile Gameplay => ExplicitGameplay ?? RiderAbilityCompatibility.FromLegacy(Skills, Style);

    [JsonIgnore]
    public bool HasExplicitGameplay => ExplicitGameplay is not null;

    /// <summary>Modern creation requires both canonical data and the current engine's explicit legacy payload.</summary>
    public static RiderProfile CreateCanonical(int id, string name, RiderGameplayProfile gameplay,
        RiderSkills legacySkills, RiderStyle legacyStyle)
    {
        ArgumentNullException.ThrowIfNull(gameplay);
        ArgumentNullException.ThrowIfNull(legacySkills);
        ArgumentNullException.ThrowIfNull(legacyStyle);
        return new(id, name, legacySkills, legacyStyle) { ExplicitGameplay = gameplay };
    }
}
