
using SharedWorldSpeedway;
using SpeedwayHandling;
using Xunit;

namespace CoreSim.Tests;

[Trait("Shard", "core")]
public sealed class UnifiedRoll215Tests
{
    private const double Gravity = LoadBasedTyreGrip.GravityMetersPerSecondSquared;
    private static readonly Geometry214 Base = Geometry214.Fixture(147, 70,
        TwoContactBikeParameters.Default, RearWheelParameters.Default, 52.72097);

    [Fact]
    public void MovingRiderReactsInternallyAndConservesSpatialMomentum()
    {
        var g = new Geometry215(Base, Axes215.All);
        var initial = g.WithRates(Seed(g) with { Momentum = new(70, 12, -5), Crank = 1.6, Wheel = 10 },
            new(.433, -.16, .18), new(.2, -.13, .04));
        var cmd = new Command215(default, new(.12, .16, -.09));
        var engine = Make(g, Actuator215.Responsive, 0);
        var s = initial;
        double work = 0;
        for (int i = 0; i < 170; i++)
        {
            var r = engine.Step(s, [new(0, .007, cmd)], .0025);
            Assert.Equal(default, r.Forces.Front.Force);
            Assert.Equal(default, r.Forces.Rear.Force);
            Assert.Equal(initial.Angular, r.After.Angular);
            Assert.InRange((initial.Momentum - r.After.Momentum).Norm, 0, 1e-12);
            work += (r.After.Time - r.Before.Time) * r.ActuatorPower;
            s = r.After;
        }
        Assert.InRange(Math.Abs(g.Energy(s, 0, UnifiedModel215.Stiffness) -
            g.Energy(initial, 0, UnifiedModel215.Stiffness) - work), 0, .005);
        Assert.True((s.Rider - initial.Rider).Norm > .10);
        Assert.True(Math.Abs(s.Pose.Roll - initial.Pose.Roll) > .01);
    }

    [Fact]
    public void GroundContactCreatesBothYawAndCountersteeringRoll()
    {
        var g = new Geometry215(Base, Axes215.Fixed);
        var s = g.Supported(Seed(g), Gravity, UnifiedModel215.Stiffness) with
            { Momentum = new(10 * Base.Mass, 0, 0) };
        var model = Make(g, Actuator215.Responsive, Gravity);
        var plan = new Activation215[] { new(s.Time, s.Time + .0081, new(new(.10f, 0), default)) };
        bool found = false;
        for (int i = 0; i < 45; i++)
        {
            var row = model.Step(s, plan, .0025);
            if (row.Before.Time < .0081 - 1e-12)
            {
                Assert.Equal(0, row.Before.Command.Bike.SteeringRadians);
                Assert.InRange(row.After.Time, 0, .0081 + 1e-12);
            }
            if (!found && row.Forces.Front.Lateral > 10)
            {
                Assert.True(row.Forces.Front.Patch.Arm.Cross(row.Forces.Front.Force).X > 0);
                Assert.True(row.RollPitchYawMoment.Z > 0);
                found = true;
            }
            s = row.After;
        }
        Assert.True(found);
        Assert.True(s.Pose.Roll > .01);
    }

    [Fact]
    public void TiltedSteeredRingDerivativeAndDenseCurvatureAgree()
    {
        var g = new Geometry215(Base, Axes215.All);
        var s = Seed(g) with { Pose = Attitude215.Pose(.4, .16, -.5), Steer = .11, Momentum = new(700, -23, -6) };
        var w = new Vec215(1.3, -.8, .6);
        var m = new Kinematics215(s.Pose.Body(w), w, new(.13, -.28, .22), default);
        const double rate = -.84, h = .00015;
        foreach (bool front in new[] { true, false })
        {
            double bound = g.GapBound(s, m, front, rate, .01);
            for (int i = 1; i < 60; i++)
            {
                double t = i * h;
                var a = At(t - h, front);
                var b = At(t, front);
                var c = At(t + h, front);
                Assert.InRange(Math.Abs((c.Gap - a.Gap) / (2 * h) - b.NormalSpeed), 0, 1e-7);
                Assert.InRange(Math.Abs((c.Gap - 2 * b.Gap + a.Gap) / (h * h)), 0, bound);
            }
        }

        Patch215 At(double t, bool front) => g.Contact(s with
        {
            Center = s.Center + s.Momentum * (t / Base.Mass),
            Pose = s.Pose.Advance(w, t),
            Rider = s.Rider + m.RiderRate * t,
            Steer = s.Steer + t * rate
        }, m, front, rate);
    }

    [Fact]
    public void ChordDetectorFindsAnInStepGapReversalFromRotatingRingGeometry()
    {
        var g = new Geometry215(Base, Axes215.Fixed);
        var s = Seed(g) with { Center = new(0, 0, .55), Rider = g.Neutral };
        const double width = .004, rollRate = 12;
        var w = Vec215.Forward * rollRate;
        var m = new Kinematics215(w, w, default, default);
        s = s with { Pose = Attitude215.Pose(0, 0, -.5 * width * rollRate) };
        double closest = At(width / 2).Gap;
        double outer = At(0).Gap;
        s = s with { Center = s.Center with { Z = s.Center.Z - (outer + closest) / 2 } };
        Assert.True(At(0).Gap > 0 && At(width).Gap > 0 && At(width / 2).Gap < 0);
        var hit = GapEvent214.First(t => (At(t).Gap, At(t).NormalSpeed), width, g.GapBound(s, m, false, 0, width));
        Assert.NotNull(hit);
        Assert.InRange(hit.Value, 0, width / 2);

        Patch215 At(double t) => g.Contact(s with { Pose = s.Pose.Advance(w, t) }, m, false, 0);
    }

    [Fact]
    public void LimitedActuationDepartsFromRequestedMotionThroughLeanedImpact()
    {
        var g = new Geometry215(Base, Axes215.All);
        var s = g.Supported(Seed(g) with { Pose = Attitude215.Pose(0, 0, -.32) }, Gravity, UnifiedModel215.Stiffness);
        s = s with { Center = s.Center + Vec215.Up * .18, Momentum = -Vec215.Up * (.5 * Base.Mass) };
        var queue = new Activation215[] { new(0, .05, new(default, new(.15, .16, -.095))) };
        var engine = Make(g, Actuator215.Limited, Gravity);
        bool saturated = false, landing = false;
        for (int i = 0; i < 100; i++)
        {
            var row = engine.Step(s, queue, .0025);
            double dt = row.After.Time - row.Before.Time;
            foreach (var t in new[] { row.Forces.Front, row.Forces.Rear })
            {
                Assert.True(t.Normal >= 0);
                Assert.Equal(0, t.EffectiveGap * t.Normal);
                if (t.Patch.Gap >= 0) Assert.Equal(default, t.Force);
            }
            if (row.Contacts.Any(x => x.Touchdown)) landing = true;
            Assert.InRange(row.Forces.Rider.Norm, 0, 700 + 1e-8);
            Assert.InRange(Math.Abs(row.ActuatorPower), 0, 350 + 1e-8);
            Assert.InRange((row.After.Angular - row.Before.Angular - row.RollPitchYawMoment * dt).Norm, 0, 1e-9);
            saturated |= Math.Abs(row.ActuatorPower) > 349.99;
            s = row.After;
        }
        Assert.True(saturated);
        Assert.True(landing);
        Assert.True((g.Neutral + s.Command.Target - s.Rider).Norm > .025);
    }

    [Fact]
    public void FootJacobianAndTransposeMatchMechanicalPower()
    {
        var g = new Geometry215(Base, Axes215.All);
        var s = g.WithRates(Seed(g) with { Momentum = new(42, -13, 12) }, new(.1, .2, .3), new(-.15, .3, .2));
        double[] leg = [.5, -.8];
        var foot = FootContact215.At(g, s, new(.06, .23, -.55), [new(.2, 0, -.18), new(.04, .32, .02)],
            leg, Vec215.Up, 0);
        var load = new Vec215(12, 5, 100);
        var j = foot.Transpose(load);
        var k = g.Motion(s);
        double power = j.Linear.Dot(s.Momentum / Base.Mass) + j.Angular.Dot(k.WorldRate) +
            j.Rider.Dot(k.RiderRate) + j.Leg.Zip(leg).Sum(pair => pair.First * pair.Second);
        Assert.InRange(Math.Abs(power - foot.Velocity.Dot(load)), 0, 1e-11);
    }

    private static State215 Seed(Geometry215 g) => new(0, new(0, 0, 3), default, Attitude215.Pose(0, 0, 0),
        default, g.Neutral, default, 0, 0, 0, default);

    private static UnifiedModel215 Make(Geometry215 g, Actuator215 actuator, double gravity)
    {
        var road = StandingWorld144.Frame();
        var rider = StandingWorld144.Rider(StartingGate.C);
        return new(g, rider, new HandlingSettings { H26 = H26Configuration.For("H26-LE") },
            new WorldParameters(), MultiActionExperiment135.Surface(road).SampleSurface(0, 2), actuator, 0, false, gravity);
    }
}
