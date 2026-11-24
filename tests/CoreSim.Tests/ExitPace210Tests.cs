using CoreSim.PhysicalSpace;
using SharedWorldSpeedway;
using SpeedwayHandling;
using WholeCorner171;
using Xunit;

namespace CoreSim.Tests;

[Trait("Shard", "core")]
public sealed class ExitPace210Tests
{
    [Fact]
    public void ARecoveryPulseCanRaiseTheRequestWithoutChangingQueuedOrActiveControls()
    {
        var frame = Cyclic172.TrafficWorld191.Frame();
        var map = new BendMap(frame);
        var bend = map.At(0, 0);
        var pulses = new PacePulse210[ExitPace210.PulseCount];
        pulses[4] = new(.5, .02, 0, 0);
        var policy = new ExitPace210(map, [new(0, pulses)]);
        var gate = ExitPace210.Gates(bend)[4];
        var original = new FreeRacingAction(.03f, .2f);
        var before = Start(frame, gate.Start);
        var at = Start(frame, gate.Peak);
        Assert.Equal(original, policy.At(before, original, map));
        Assert.Equal(original, policy.At(Start(frame, gate.End), original, map));
        Assert.Equal(new FreeRacingAction(.05f, .6f), policy.At(at, original, map));
        Assert.Equal(.15f, at.Active.Drive);
        Assert.Equal(.4f, Assert.Single(at.Pending).Action.Drive);
    }

    [Fact]
    public void AlignmentUsesTheCurrentOutgoingVelocityAndYaw()
    {
        var frame = Cyclic172.TrafficWorld191.Frame();
        var map = new BendMap(frame);
        var bend = map.At(0, 0);
        var pulses = new PacePulse210[ExitPace210.PulseCount];
        pulses[4] = new(0, 0, 0, .3);
        var policy = new ExitPace210(map, [new(0, pulses)]);
        var at = Start(frame, ExitPace210.Gates(bend)[4].Peak);
        var rider = at.State.Rider;
        var wider = at with { State = at.State with { Rider = rider with {
            Velocity = rider.Velocity + 3 * rider.Road.Outward, AngularVelocity = .1f } } };
        Assert.True(policy.At(wider, default, map).SteeringRadians > policy.At(at, default, map).SteeringRadians);
    }

    private static Start197 Start(TrackFrame frame, double meters)
    {
        var pose = frame.At(meters, 9.5);
        var rider = new RiderView(1, pose.Position, 18 * pose.Road.Tangent, (float)pose.Road.Heading, 0,
            pose.Road, meters, 0, RiderActivity.Racing, 0, 9.5f, 0);
        return new(new(rider, 4, 53, 0, 0, 425), new(0, .15f), [new(4, 4.05, new(0, .4f))], 4);
    }
}
