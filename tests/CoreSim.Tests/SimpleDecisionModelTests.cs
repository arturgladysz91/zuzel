using CoreSim;
using CoreSim.Decisions;
using Xunit;

namespace CoreSim.Tests;

[Trait("Shard", "core")]
public class SimpleDecisionModelTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(4)]
    public void Decide_TargetLaneStaysWithinBounds_ForExtremeLanes(int startingLane)
    {
        var model = new SimpleDecisionModel(seed: 42);
        var segment = new TrackSegment(0, SegmentType.Straight);
        var rider = new RiderState(1, startingLane);

        for (var i = 0; i < 100; i++)
        {
            var decision = model.Decide(segment, rider);

            Assert.InRange(decision.TargetLane, 0, 4);

            rider.Lane = decision.TargetLane;
        }
    }
}
