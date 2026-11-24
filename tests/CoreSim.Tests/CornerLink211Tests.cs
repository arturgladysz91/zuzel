using SharedWorldSpeedway;
using SpeedwayHandling;
using WholeCorner171;
using Xunit;

namespace CoreSim.Tests;

[Trait("Shard", "core")]
public sealed class CornerLink211Tests
{
    [Fact]
    public void AHighSpeedPrefixNeedsTheIncomingBendAndItsStabilization()
    {
        Assert.False(LinkFeedback211.Eligible(105, null, .80, 200));
        Assert.False(LinkFeedback211.Eligible(210, "wide-direction", .80, 200));
        Assert.False(LinkFeedback211.Eligible(210, null, .499, 200));
        Assert.True(LinkFeedback211.Eligible(205, null, .50, 200));
        Assert.True(LinkFeedback211.Eligible(205, "goal", .55, 200));
    }

    [Fact]
    public void ALinkReleasesThrottleBeforeTheIncomingBendAndLeavesTheQueueIntact()
    {
        var frame = Cyclic172.TrafficWorld191.Frame();
        var map = new BendMap(frame);
        var outgoing = map.At(0, 0);
        var incoming = map.At(0, 1);
        var link = new LinkFeedback211(map, [new(0, 0, .6, 24, -.4, 0, .01, -.005, 0, 0, 0)]);
        var original = new FreeRacingAction(0, .4f);
        var early = At(outgoing.Exit + 12);
        var later = At(incoming.Entry - 10);
        var originalQueue = early.Pending[0];
        Assert.True(link.Apply(early, original).Drive > original.Drive);
        Assert.True(link.Apply(later, original).Drive < original.Drive);
        Assert.Equal(originalQueue, Assert.Single(early.Pending));
        Assert.Equal(.1f, early.Active.Drive);
        Assert.Equal(original, link.Apply(At(LinkFeedback211.Begin(outgoing)), original));

        Start197 At(double s)
        {
            var point = frame.At(s, 8);
            var body = new RiderView(1, point.Position, 24 * point.Road.Tangent, (float)point.Road.Heading, 0,
                point.Road, s, 0, RiderActivity.Racing, 0, 8, 0);
            return new(new(body, 5, 65, 0, 0, 500), new(0, .1f), [new(5, 5.05, new(0, .3f))], 5);
        }
    }

    [Fact]
    public void TheSameRootStatusCanDescribeDistinctRootsOrAnIndeterminateEnclosure()
    {
        ContactResidual64<int> Polynomial(double x) => new(x, (x - .2) * (x - .7), 0);
        var multiple = Assert.Throws<ContactRootException>(() => ContactLoadSolver64.SolveCertified(0d, 1d, 1e-8,
            Polynomial, (a, b, _) =>
            {
                ContactInterval64 interval = new(a, b);
                return new((interval - .2) * (interval - .7), 2 * interval - .9);
            }));
        Assert.Equal(ContactRootStatus.AmbiguousOrMultipleRoots, multiple.Status);
        Assert.Contains("distinct isolated", multiple.Detail);

        ContactResidual64<int> Linear(double x) => new(x, x - .12345, 0);
        var unresolved = Assert.Throws<ContactRootException>(() => ContactLoadSolver64.SolveCertified(0d, 1d, 1e-8,
            Linear, (a, b, _) => new(new ContactInterval64(a - .12345, b - .12345), new ContactInterval64(-1, 2))));
        Assert.Equal(ContactRootStatus.AmbiguousOrMultipleRoots, unresolved.Status);
        Assert.Contains("cannot isolate", unresolved.Detail);
        var unique = ContactLoadSolver64.SolveCertified(0d, 1d, 1e-8, Linear,
            (a, b, _) => new(new ContactInterval64(a - .12345, b - .12345), 1));
        Assert.InRange(Math.Abs(unique.Ax - .12345), 0, 1e-8);
    }
}
