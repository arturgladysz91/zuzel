
using SharedWorldSpeedway;
using SpeedwayHandling;
using Xunit;

namespace CoreSim.Tests;

[Trait("Shard", "core")]
public sealed class PassiveSupport216Tests
{
    private const double Gravity = LoadBasedTyreGrip.GravityMetersPerSecondSquared;
    private static readonly Geometry214 Base = Geometry214.Fixture(147, 70,
        TwoContactBikeParameters.Default, RearWheelParameters.Default, 52.72097);

    [Fact]
    public void SeatedRiderSupportsWeightWithTheMuscularDriveDisabled()
    {
        var g = new Geometry215(Base, Axes215.All);
        var support = new RiderSupport216();
        var r = support.SeatedReference(g, Gravity);
        var state = new State215(0, new(0, 0, .55), default, Attitude215.Pose(0, 0, 0),
            default, r, default, 0, 0, 0, default);
        state = support.PassiveEquilibrium(g, state, Gravity);
        var model = Build(g, support, Gravity, Actuator215.Limited with { MaxForce = 0, MaxPower = 0 });
        var row = model.Step(state, [], .0025);
        var pads = Assert.IsType<SupportLoads216>(row.Forces.PassiveSupport);
        Near(Base.RiderMass * Gravity, row.Midpoint.Pose.Apply(pads.Force).Z, 1e-7);
        Near(0, row.Forces.Rider.Norm, 1e-10);
        Near(0, row.After.RiderMomentum.Norm, 1e-8);
        Near(0, row.After.Momentum.Norm, 1e-8);
        Assert.True(pads.Seat.Contact.NormalForce > pads.LeftPeg.Contact.NormalForce);
    }

    [Fact]
    public void PadsAreUnilateralAndFrictionDissipatesWithinTheCone()
    {
        var g = new Geometry215(Base, Axes215.All);
        var support = new RiderSupport216();
        var separated = support.Evaluate(g.Neutral + new Vec215(0, 0, .10), new(0, 0, -1), 0, 0);
        Assert.Equal(default, separated.Force);
        Assert.Equal(0, separated.Loss);
        var r = support.SeatedReference(g, Gravity);
        var loaded = support.Evaluate(r + new Vec215(.025, 0, -.05), new(.8, -.2, -.15), .12, .7);
        var c = loaded.LeftGrip.Contact;
        Assert.True(c.NormalForce > 0);
        Assert.True(c.Dissipation > 0);
        var tangential = c.Force - c.Normal * c.NormalForce;
        Assert.InRange(tangential.Norm, 0, .56 * c.NormalForce + 1e-10);
    }

    [Fact]
    public void SteeringServoAccountsForWorkAtTheGrips()
    {
        var g = new Geometry215(Base, Axes215.All);
        var support = new RiderSupport216();
        var r = support.SeatedReference(g, Gravity) + new Vec215(.032, .023, -.055);
        const double angle = .105, h = 1e-7;
        var middle = support.Evaluate(r, default, angle, 1);
        double elasticSlope = (support.Evaluate(r, default, angle + h, 0).Energy -
            support.Evaluate(r, default, angle - h, 0).Energy) / (2 * h);
        Near(elasticSlope + middle.Loss, middle.SteerPower, 1e-4);
    }

    [Fact]
    public void CompressedSeatAndActiveRiderDoNotCreateAnExternalImpulse()
    {
        var g = new Geometry215(Base, Axes215.All);
        var support = new RiderSupport216();
        var r = support.SeatedReference(g, Gravity);
        var initial = g.WithRates(new State215(0, new(0, 0, 3), new(24, -5, 9), Attitude215.Pose(.12, .17, -.29),
            default, r, default, 1.2, 10, 0, default), new(.4, -.17, .27), new(.2, -.1, -.15));
        var queue = new Activation215[] { new(0, .01, new(new(.15f, 0), new(.09, .11, -.09))) };
        var model = Build(g, support, 0, Actuator215.Limited);
        State215 s = initial;
        double residual = 0;
        for (int i = 0; i < 140; i++)
        {
            var row = model.Step(s, queue, .0025);
            s = row.After;
            Assert.Equal(default, row.Forces.Front.Force);
            Assert.Equal(default, row.Forces.Rear.Force);
            Near(0, (s.Momentum - initial.Momentum).Norm, 1e-9);
            Near(0, (s.Angular - initial.Angular).Norm, 1e-9);
            Assert.InRange(row.Forces.Rider.Norm, 0, 700 + 1e-9);
            Assert.InRange(Math.Abs(row.ActuatorPower), 0, 350 + 1e-9);
            residual += row.EnergyResidual;
        }
        Near(0, residual, .04);
    }

    [Fact]
    public void ArticulatedLegMovesMomentumInternallyAndAppliesNoAirborneGroundLoad()
    {
        var leg = new Leg216();
        var g = new Geometry215(Base, Axes215.All, leg);
        var support = new RiderSupport216();
        var baseState = new State215(0, new(0, 0, 3), new(25, 7, -2), Attitude215.Pose(.4, .23, -.20),
            default, support.SeatedReference(g, Gravity), default, 2, 9, 0, default)
        {
            Leg = new(.16, 0)
        };
        var angular = new Vec215(.422, -.25, .19);
        var relative = new Vec215(.13, .08, -.12);
        var s = g.WithRates(baseState, angular, relative, .32);
        var k = g.Motion(s);
        Near(0, (k.BodyRate - angular).Norm, 1e-12);
        Near(0, (k.RiderRate - relative).Norm, 1e-12);
        Near(.32, k.LegRate, 1e-12);

        var first = s;
        var model = Build(g, support, 0, Actuator215.Limited);
        var queue = Deployment(.80);
        double error = 0;
        while (s.Time < .36)
        {
            var row = model.Step(s, queue, .0025);
            Assert.Equal(default, row.Forces.LeftFoot!.Contact.Force);
            Near(0, (row.After.Angular - first.Angular).Norm, 1e-10);
            Near(0, (row.After.Momentum - first.Momentum).Norm, 1e-10);
            Assert.True(row.Forces.Rider.Norm + Math.Abs(row.Forces.LeftFoot.MuscleForce) <= 700.000001);
            Assert.True(row.Forces.LeftFoot.AbsoluteMusclePower <= 350.000001);
            error += row.EnergyResidual;
            s = row.After;
        }
        Assert.True(s.Leg.Extension > .50);
        Near(0, error, .05);
    }

    [Fact]
    public void RealBootContactEntersTheSharedImpulseAndFrictionLedger()
    {
        var g = new Geometry215(Base, Axes215.All, new Leg216());
        var support = new RiderSupport216();
        var s = new State215(0, new(0, 0, .55), new(Base.Mass * 5, 0, 0), Attitude215.Pose(0, 0, -.36),
            default, support.SeatedReference(g, Gravity), default, 0, 0, 0, default);
        s = g.Supported(s, Gravity, UnifiedModel215.Stiffness);
        var model = Build(g, support, Gravity, Actuator215.Limited);
        var queue = Deployment(.92);
        bool landed = false, loaded = false;
        while (s.Time < .30)
        {
            var row = model.Step(s, queue, .0025);
            var foot = Assert.IsType<LegLoads216>(row.Forces.LeftFoot);
            landed |= row.Contacts.Any(c => c.Foot && c.Touchdown);
            loaded |= foot.Contact.NormalForce > 100;
            var hf = row.Plan.Foot;
            Near(0, (hf.Force - foot.Force.Plane).Norm, 1e-10);
            double dt = row.After.Time - row.Before.Time;
            var net = row.Forces.Front.Force + row.Forces.Rear.Force + foot.Force - Vec215.Up * (Base.Mass * Gravity);
            Near(0, (net * dt - (row.After.Momentum - row.Before.Momentum)).Norm, 1e-9);
            var tangent = foot.Force - foot.Contact.Normal * foot.Contact.NormalForce;
            Assert.True(tangent.Norm <= .32 * foot.Contact.NormalForce + 1e-9);
            s = row.After;
        }
        Assert.True(landed);
        Assert.True(loaded);
    }

    private static Activation215[] Deployment(double target) =>
        [new(0, .05, new Command215(default, default) { LeftFoot = target })];

    private static void Near(double expected, double measured, double tolerance) =>
        Assert.InRange(measured, expected - tolerance, expected + tolerance);

    private static UnifiedModel215 Build(Geometry215 g, RiderSupport216 support, double gravity, Actuator215 actuator) =>
        new(g, StandingWorld144.Rider(StartingGate.C),
            new HandlingSettings { H26 = H26Configuration.For("H26-LE") }, new WorldParameters(),
            MultiActionExperiment135.Surface(StandingWorld144.Frame()).SampleSurface(0, 2),
            actuator, 0, false, gravity, support: support);
}
