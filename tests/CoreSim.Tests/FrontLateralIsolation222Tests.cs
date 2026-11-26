using SharedWorldSpeedway;
using SpeedwayHandling;
using Xunit;

namespace CoreSim.Tests;

[Trait("Shard", "core")]
public sealed class FrontLateralIsolation222Tests
{
    [Theory]
    [InlineData(.35, .2, 980)]
    [InlineData(.35, 3.1, 970)]
    [InlineData(.75, -4.2, 800)]
    [InlineData(.75, .3, 0)]
    public void SteadyPairMatchesForceAndWorkAfterEliminatingTheStorageState(double length, double lateralSpeed, double capacity)
    {
        var dynamic = new FrontContact221(RelaxationMeters: length);
        var steady = new FrontContact221(RelaxationMeters: length, SteadyLateral: true);
        var basis = Geometry214.Fixture(147, 70, TwoContactBikeParameters.Default, RearWheelParameters.Default, 52.72097);
        var g = new Geometry215(basis, Axes215.All);
        const double u = 23;
        double rate = Math.Sqrt(u * u + .75 * .75) / length;
        double q = capacity > 0 ? lateralSpeed / rate : 0;
        if (dynamic.ShearStiffness * Math.Abs(q) > capacity)
            q = Math.CopySign(Math.Sqrt(capacity * Math.Abs(q) / dynamic.ShearStiffness), q);
        var s = new State215(0, new(0, 0, 1), new(147 * u, 147 * lateralSpeed, 0), Attitude215.Pose(0, 0, 0),
            default, g.Neutral, default, 0, 0, 0, default) { FrontShearMeters = q };
        var m = g.Motion(s);
        var patch = g.Contact(s, m, true, 0);
        var a = dynamic.Evaluate(s, m, patch, capacity / 1.6, 0, capacity, 1, 0, out var workA);
        var b = steady.Evaluate(s, m, patch, capacity / 1.6, 0, capacity, 1, 0, out var workB);
        Assert.InRange(Math.Abs(a.Lateral - b.Lateral), 0, 1e-10);
        Assert.InRange(Math.Abs(workA.StrainRateMetersPerSecond), 0, 1e-12);
        Assert.InRange(Math.Abs(workA.RelaxationLossWatts - workB.ContactLossWatts), 0, 1e-8);
        Assert.False(steady.HasLateralMemory);
    }
}
