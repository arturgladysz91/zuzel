using SpeedwayHandling;
using Xunit;

namespace CoreSim.Tests;

public sealed class CanonicalClock226Tests
{
    [Theory]
    [InlineData(2_500_000_000L)]
    [InlineData(1_250_000_000L)]
    [InlineData(625_000_000L)]
    public void EveryReviewAndReactionLiesOnTheDeclaredMesh(long step)
    {
        long root = Clock46.Tick(1.24);
        var clock = new Clock46(root, step);
        long review = root + 37 * Clock46.ReviewTicks;
        Assert.Equal(review, clock.AtOrAfter(review));
        Assert.Equal(review, clock.AtOrAfter(review - 1));
        Assert.Equal(review + step, clock.Next(review));
        var command = Clock46.Command(review, default);
        Assert.Equal(Clock46.ReactionTicks, Clock46.Tick(command.Time) - Clock46.Tick(command.Requested));
        Assert.Equal(Clock46.Tick(command.Time), clock.AtOrAfter(Clock46.Tick(command.Time)));
    }

    [Fact]
    public void ImportCanonicalizesSubPicosecondDifferencesAndRejectsInvertedCommands()
    {
        var first = Clock46.Import(new(2.06, 2.11, default));
        var second = Clock46.Import(new(Math.BitIncrement(2.06), Math.BitDecrement(2.11), default));
        Assert.Equal(first, second);
        Assert.Throws<ArgumentException>(() => Clock46.Import(new(2.11, 2.06, default)));
    }
}
