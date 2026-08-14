namespace CoreSim.Setup;

public sealed record ManagerSetupSuggestion(BikeSetup Setup, float Confidence)
{
    public float Confidence { get; init; } = Math.Clamp(Confidence, 0f, 1f);
}

public enum SetupResponse
{
    Accepted,
    Adjusted,
    Ignored,
}

public sealed record SetupResolution(BikeSetup AppliedSetup, SetupResponse Response, float AcceptanceChance);

/// <summary>Resolves the human decision: accept, modify or ignore manager advice.</summary>
public sealed class SetupResolver
{
    private readonly Random _random;

    public SetupResolver(int seed = 1234) => _random = new Random(seed);

    public SetupResolution Resolve(
        RiderState rider,
        ManagerSetupSuggestion suggestion,
        BikeSetup riderPreferredSetup)
    {
        ArgumentNullException.ThrowIfNull(rider);
        ArgumentNullException.ThrowIfNull(suggestion);
        ArgumentNullException.ThrowIfNull(riderPreferredSetup);

        var independence = rider.Profile.Style.SetupIndependence;
        var acceptanceChance = Math.Clamp(
            0.10f
            + rider.ManagerTrust * 0.55f
            + suggestion.Confidence * 0.25f
            - independence * 0.30f,
            0.05f,
            0.95f);

        var roll = (float)_random.NextDouble();
        SetupResolution resolution;

        if (roll <= acceptanceChance)
        {
            resolution = new SetupResolution(suggestion.Setup, SetupResponse.Accepted, acceptanceChance);
        }
        else if (roll <= acceptanceChance + 0.35f)
        {
            var reading = RiderSkills.Normalize(rider.Profile.Skills.TrackReading);
            var riderWeight = 0.35f + reading * 0.35f;
            resolution = new SetupResolution(
                BikeSetup.Blend(suggestion.Setup, riderPreferredSetup, riderWeight),
                SetupResponse.Adjusted,
                acceptanceChance);
        }
        else
        {
            resolution = new SetupResolution(riderPreferredSetup, SetupResponse.Ignored, acceptanceChance);
        }

        rider.ActiveSetup = resolution.AppliedSetup;
        return resolution;
    }
}
