using CoreSim;
using Xunit;

namespace CoreSim.Tests;

public sealed class CornerSpeedCorrectionProfileTests
{
    [Fact]
    public void CorrectionReachesTargetWhenDistanceIsSufficient()
    {
        var profile = Profile(18f, 16f, 2.5f, 20f);

        Assert.True(profile.TargetReached);
        Assert.Equal(16f, profile.ExitSpeedMetersPerSecond);
        Assert.Equal(13.6f, profile.CorrectionDistanceMeters, 5);
    }

    [Fact]
    public void CorrectionDoesNotOvershootBelowTarget()
    {
        var profile = Profile(18f, 16f, 2.5f, 100f);

        Assert.Equal(profile.TargetSpeedMetersPerSecond, profile.ExitSpeedMetersPerSecond);
        Assert.True(profile.ExitSpeedMetersPerSecond >= profile.TargetSpeedMetersPerSecond);
    }

    [Fact]
    public void CorrectionRetainsResidualOverspeedWhenDistanceIsInsufficient()
    {
        var profile = Profile(18f, 16f, 2.5f, 5f);

        Assert.False(profile.TargetReached);
        Assert.True(profile.ExitSpeedMetersPerSecond > 16f);
    }

    [Fact]
    public void CorrectionUsesEntireDistanceWhenTargetCannotBeReached()
    {
        var profile = Profile(18f, 16f, 2.5f, 5f);

        Assert.Equal(5f, profile.CorrectionDistanceMeters);
        Assert.Equal(0f, profile.RemainingDistanceMeters);
    }

    [Fact]
    public void CorrectionLeavesRemainingDistanceWhenTargetReachedEarly()
    {
        var profile = Profile(18f, 16f, 2.5f, 20f);

        Assert.Equal(6.4f, profile.RemainingDistanceMeters, 5);
        Assert.Equal(
            20f,
            profile.CorrectionDistanceMeters + profile.RemainingDistanceMeters,
            5);
    }

    [Fact]
    public void CorrectionTravelTimeUsesStartAndEndSpeed()
    {
        var profile = Profile(18f, 16f, 2.5f, 20f);
        var expected = 2f * profile.CorrectionDistanceMeters
            / (profile.EntrySpeedMetersPerSecond + profile.ExitSpeedMetersPerSecond);

        Assert.Equal(expected, profile.TravelTimeSeconds, 6);
        Assert.NotEqual(
            profile.CorrectionDistanceMeters / profile.ExitSpeedMetersPerSecond,
            profile.TravelTimeSeconds,
            5);
    }

    [Fact]
    public void ZeroDistancePreservesSpeed()
    {
        var profile = Profile(18f, 16f, 2.5f, 0f);

        Assert.Equal(18f, profile.ExitSpeedMetersPerSecond);
        Assert.Equal(0f, profile.CorrectionDistanceMeters);
        Assert.Equal(0f, profile.RemainingDistanceMeters);
        Assert.Equal(0f, profile.TravelTimeSeconds);
        Assert.False(profile.TargetReached);
    }

    [Fact]
    public void CorrectionIsDeterministic()
        => Assert.Equal(Profile(18f, 16f, 2.5f, 20f), Profile(18f, 16f, 2.5f, 20f));

    [Fact]
    public void MoreAvailableDistanceNeverIncreasesExitSpeed()
    {
        var distances = new[] { 0f, 1f, 5f, 10f, 20f, 100f };
        var exits = distances.Select(distance => Profile(18f, 16f, 2.5f, distance)
            .ExitSpeedMetersPerSecond).ToArray();

        for (var index = 1; index < exits.Length; index++)
            Assert.True(exits[index] <= exits[index - 1]);
    }

    [Fact]
    public void GreaterCorrectionDecelerationRequiresLessDistance()
    {
        var weak = Profile(18f, 16f, 2f, 100f);
        var strong = Profile(18f, 16f, 3.2f, 100f);

        Assert.True(strong.RequiredCorrectionDistanceMeters
            < weak.RequiredCorrectionDistanceMeters);
    }

    [Fact]
    public void ProfileRejectsNaNInfinityAndNegativeInputs()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Profile(float.NaN, 16f, 2f, 10f));
        Assert.Throws<ArgumentOutOfRangeException>(() => Profile(float.PositiveInfinity, 16f, 2f, 10f));
        Assert.Throws<ArgumentOutOfRangeException>(() => Profile(-1f, 16f, 2f, 10f));
        Assert.Throws<ArgumentOutOfRangeException>(() => Profile(18f, float.NaN, 2f, 10f));
        Assert.Throws<ArgumentOutOfRangeException>(() => Profile(18f, -1f, 2f, 10f));
        Assert.Throws<ArgumentOutOfRangeException>(() => Profile(18f, 16f, float.PositiveInfinity, 10f));
        Assert.Throws<ArgumentOutOfRangeException>(() => Profile(18f, 16f, 0f, 10f));
        Assert.Throws<ArgumentOutOfRangeException>(() => Profile(18f, 16f, 2f, float.NaN));
        Assert.Throws<ArgumentOutOfRangeException>(() => Profile(18f, 16f, 2f, -1f));
    }

    private static CornerSpeedCorrectionProfile Profile(
        float initial,
        float target,
        float deceleration,
        float distance)
        => LongitudinalDynamics.CalculateCornerSpeedCorrectionProfile(
            initial,
            target,
            deceleration,
            distance);
}
