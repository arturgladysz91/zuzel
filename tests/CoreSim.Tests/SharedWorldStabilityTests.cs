using SharedWorldSpeedway;
using Xunit;

namespace CoreSim.Tests;

[Trait("Shard", "core")]
public sealed class SharedWorldStabilityTests
{
    [Fact]
    public void OptionalForceAndContactObserversDoNotChangeAcceptedPhysics()
    {
        var s = Scenarios.DenseStart() with { DurationSeconds = 1.1 };
        var expected = Scenarios.Run(s, .01); var forces = 0; var points = 0;
        using var world = new WorldRunner(s.Riders, diagnostics: new() {
            Force = _ => forces++, ContactPoint = _ => points++, Consequence = _ => { } });
        var actual = world.Run(s.Name, .01, s.DurationSeconds);
        Assert.Equal(expected.Fingerprint, actual.Fingerprint);
        Assert.Equal(expected.Contacts, actual.Contacts);
        Assert.True(forces > 0); Assert.True(points > 0);
    }
}
