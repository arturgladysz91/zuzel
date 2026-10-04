using CoreSim;
using CoreSim.Setup;
using Xunit;

namespace CoreSim.Tests;

[Trait("Shard", "core")]
public sealed class SetupResolverTests
{
    [Fact]
    public void TrustedHighConfidenceAdviceIsAcceptedWithDeterministicSeed()
    {
        var profile = new RiderProfile(
            1,
            "Cooperative",
            RiderSkills.Balanced,
            new RiderStyle(0.5f, 0.5f, 0.5f, 0f));
        var rider = new RiderState(profile, lane: 0, managerTrust: 1f);
        var suggestion = new ManagerSetupSuggestion(new BikeSetup(0.7f, 0.8f), 1f);

        var result = new SetupResolver(seed: 1).Resolve(rider, suggestion, BikeSetup.Neutral);

        Assert.Equal(SetupResponse.Accepted, result.Response);
        Assert.Equal(suggestion.Setup, rider.ActiveSetup);
        Assert.Equal(0.9f, result.AcceptanceChance, 3);
    }
}
