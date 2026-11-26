using SharedWorldSpeedway;
using SpeedwayHandling;
using Xunit;

namespace CoreSim.Tests;

[Trait("Shard", "core")]
public sealed class FrontContact221Tests
{
    private static readonly Geometry214 Dimensions = Geometry214.Fixture(147, 70,
        TwoContactBikeParameters.Default, RearWheelParameters.Default, 52.72097);

    [Fact]
    public void RotatingFrontAxleExchangesChassisMomentumWithAnAirborneRotor()
    {
        var geometry = new Geometry215(Dimensions, Axes215.All, frontContact: new(.45));
        var initial = geometry.WithRates(Seed(geometry) with { FrontWheel = 22 }, new(.19, -.08, .22), default);
        var steered = initial with { Steer = .24 };
        var motion = geometry.Motion(steered);
        var recovered = geometry.WithRates(steered, motion.BodyRate, motion.RiderRate);
        Assert.InRange((recovered.Angular - initial.Angular).Norm, 0, 1e-12);
        Assert.Equal(initial.FrontWheel, steered.FrontWheel);
        Assert.True((geometry.Motion(initial).BodyRate - motion.BodyRate).Norm > .02);
    }

    [Theory]
    [InlineData(.25)]
    [InlineData(.65)]
    public void FrontRotorContactIsPassiveAndFadesInFlight(double inertia)
    {
        var variant = new FrontContact221(inertia);
        var geometry = new Geometry215(Dimensions, Axes215.All, frontContact: variant);
        var state = Seed(geometry) with { FrontWheel = 13, Steer = .095, Momentum = new(2600, 35, 0) };
        var motion = geometry.Motion(state);
        var patch = geometry.Contact(state, motion, true, .6);
        var loaded = variant.Evaluate(state, motion, patch, 700, 0, 1100, 2, .6, out var result);
        Assert.InRange(loaded.Utilization, .1, 1);
        Assert.True(result.ContactLossWatts > 0);
        Assert.Equal(result.RotorTorqueNm, patch.Axle.Dot(patch.Radial.Cross(loaded.Force)));
        var free = variant.Evaluate(state, motion, patch, 0, 0, 0, 2, .6, out var air);
        Assert.Equal(default, free.Force);
        Assert.Equal(0, air.RotorTorqueNm);
        Assert.Equal(result.RelativeSpinRadiansPerSecond, air.RelativeSpinRadiansPerSecond);
    }

    [Theory]
    [InlineData(0, .35)]
    [InlineData(350, .35)]
    [InlineData(1300, .75)]
    public void FrontShearStorageAndDissipationMatchContactWorkAtAnyLoad(double capacity, double relaxation)
    {
        var variant = new FrontContact221(RelaxationMeters: relaxation);
        var environment = new Geometry215(Dimensions, Axes215.All, frontContact: variant);
        var s = Seed(environment) with { FrontShearMeters = -.021, Momentum = new(2300, 123, 0) };
        var motion = environment.Motion(s);
        var patch = environment.Contact(s, motion, true, 0);
        var f = variant.Evaluate(s, motion, patch, capacity / 1.6, 0, capacity, 2, 0, out var response);
        double rate = variant.ShearStiffness * s.FrontShearMeters * response.StrainRateMetersPerSecond;
        double tyreWork = -f.Lateral * patch.Velocity.Dot(patch.Side);
        Assert.InRange(Math.Abs(rate + response.RelaxationLossWatts - tyreWork), 0, 1e-9);
        Assert.InRange(f.Utilization, 0, 1);
        Assert.True(response.RelaxationLossWatts > 0);
    }

    private static State215 Seed(Geometry215 geometry) =>
        new(0, new(0, 0, 1.2), default, Attitude215.Pose(.13, -.03, -.05), default, geometry.Neutral,
            default, 0, 0, 0, default);
}
