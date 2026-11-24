using SharedWorldSpeedway;
using SpeedwayHandling;
using Xunit;

namespace CoreSim.Tests;

[Trait("Shard", "core")]
public sealed class UnifiedDynamics214Tests
{
    private static readonly Geometry214 Geometry = Geometry214.Fixture(147, 70, TwoContactBikeParameters.Default,
        RearWheelParameters.Default, 52.72097);

    [Fact]
    public void StrokeRespectsTravelSpeedAndAccelerationAndStartsAndEndsAtRest()
    {
        var from = new Vec214(-.18, -.14);
        var to = new Vec214(.18, .04);
        var stroke = Stroke214.Begin(2, from, to);
        Assert.Equal(new RiderMotion214(from, default, default), stroke.At(2));
        Assert.Equal(new RiderMotion214(to, default, default), stroke.At(stroke.End));
        for (int i = 0; i <= 100; i++)
        {
            var state = stroke.At(2 + (stroke.End - 2) * i / 100);
            Assert.InRange(state.Position.X, from.X, to.X);
            Assert.InRange(state.Position.Y, from.Y, to.Y);
            Assert.InRange(state.Velocity.Norm, 0, Stroke214.MaximumSpeed + 1e-12);
            Assert.InRange(state.Acceleration.Norm, 0, Stroke214.MaximumAcceleration + 1e-12);
        }
    }

    [Fact]
    public void ClockSplitsAtActivationAndPendingCommandCannotMoveTheCurrentRider()
    {
        var fixture = Rest() with { Height = 3, Mode = WheelContact213.Air };
        var actuator = new Command214(new(0, 0), new(.12, -.09));
        var pending = fixture.Request(actuator, .007);
        var horizontal = new CountingBackend();
        var model = Build(true, 0, horizontal);
        var first = model.Step(pending, .010, 0);
        Assert.Equal(.007, first.After.Time);
        Assert.Equal(fixture.Rider, first.After.Rider);
        Assert.Equal(fixture.Active, first.After.Active);
        Assert.Single(first.After.Pending);
        var second = model.Step(first.After, .010, 0);
        Assert.Equal(actuator, second.Before.Active);
        Assert.Empty(second.Before.Pending);
        Assert.NotEqual(second.Before.Rider, second.After.Rider);
        Assert.Equal(2, horizontal.Accepted);
    }

    [Fact]
    public void InternalMotionRepositionsFrameWithoutChangingFreeLinearOrPitchMomentum()
    {
        var p = Rest() with { Height = 3, Mode = WheelContact213.Air, PitchMomentum = 1,
            Plane = new(default, new(3, -.5), 0, 0) };
        var one = p.Request(new(new(0, 0), new(.16, -.10)), .05);
        var model = Build(true, 0);
        double work = 0;
        var state = one;
        while (state.Time < 1 - 1e-10)
        {
            var step = model.Step(state, Math.Min(.005, 1 - state.Time), 0);
            work += step.RiderWork;
            state = step.After;
        }
        Assert.Equal(p.Plane.Velocity, state.Plane.Velocity);
        Assert.Equal(p.VerticalMomentum, state.VerticalMomentum);
        Assert.Equal(p.PitchMomentum, state.PitchMomentum);
        Assert.Equal(p.Plane.YawMomentum, state.Plane.YawMomentum);
        Assert.NotEqual(one.Rider.Position, state.Rider.Position);
        var recoil = (state.Rider.Position.Rotate(state.Pitch) - one.Rider.Position.Rotate(one.Pitch)) * -Geometry.RiderFraction;
        Assert.True(recoil.Norm > .05);
        Near(0, Geometry.Energy(state, 0) - Geometry.Energy(one, 0) - work, .0002);
    }

    [Fact]
    public void VaryingYawInertiaCannotCreateYawMomentum()
    {
        var model = Build(true, 0);
        var p = Rest() with { Height = 3, Mode = WheelContact213.Air, Plane = new(default, default, 0, 20) };
        var stage = p.Request(new(new(0, 0), new(.18, -.08)), 0);
        while (stage.Time < .70)
            stage = model.Step(stage, .005, 0).After;
        Assert.Equal(20, stage.Plane.YawMomentum);
        Assert.NotEqual(Geometry.YawRate(p), Geometry.YawRate(stage));
    }

    [Fact]
    public void InteriorGapCrossingIsFoundWithPositiveEndpointsAndGrazingIsIgnored()
    {
        const double center = .37;
        static (double, double) Curve(double t, double depth) =>
            ((t - center) * (t - center) - depth, 2 * (t - center));
        double? hit = GapEvent214.First(t => Curve(t, .003), 1, 2);
        Assert.NotNull(hit);
        Near(center - Math.Sqrt(.003), hit.Value, 2e-8);
        Assert.Null(GapEvent214.First(t => Curve(t, 0), 1, 2));
        Assert.Null(GapEvent214.First(t => Curve(t, -1e-5), 1, 2));
    }

    [Fact]
    public void TouchdownAccountsForTheInternalImpulseOfAMovingRider()
    {
        var initial = Rest();
        var stroke = Stroke214.Begin(0, Geometry.RiderNeutral, Geometry.RiderNeutral + new Vec214(.12, -.12));
        double time = stroke.End * .47;
        var rider = stroke.At(time);
        var s = initial with
        {
            Time = time, Stroke = stroke, Height = Geometry.FrameHeight + Geometry.RiderFraction * rider.Position.Y,
            VerticalMomentum = -90, PitchMomentum = Geometry.CrossMomentum(rider) - initial.CrankMomentum
        };
        var hit = Contact214.Impact(Geometry, s, 9.81);
        Assert.Equal(WheelContact213.Both, hit.Impulse.Mode);
        Assert.True(hit.Dissipated > 0);
        Assert.True(Math.Abs(hit.RiderWork) > .1);
        Assert.Equal(s.Plane, hit.After.Plane);
        Assert.Equal(s.Height, hit.After.Height);
        Assert.Equal(s.CrankMomentum, hit.After.CrankMomentum);
        Assert.Equal(s.WheelMomentum, hit.After.WheelMomentum);
        Near(0, Geometry.Contact(hit.After, WheelContact213.Front).GapRate, 1e-11);
        Near(0, Geometry.Contact(hit.After, WheelContact213.Rear).GapRate, 1e-11);
        Near(0, hit.EnergyResidual, 1e-10);
    }

    [Fact]
    public void PoweredFlightKeepsTyresOffAndSteeringActive()
    {
        var model = Build(false, 9.81);
        var s = Rest() with { Height = 3, Time = .5, Mode = WheelContact213.Air,
            Plane = new(default, new(18, 0), 0, 0), Active = new(new(.1f, .8f), default) };
        var step = model.Step(s, .005, 0);
        Assert.Equal(WheelContact213.Air, step.Forces.Normal.Mode);
        Assert.Equal(default, step.Forces.Front.Force);
        Assert.Equal(default, step.Forces.Rear.Force);
        Assert.Equal(0, step.Forces.FrontYawMaximum);
        Assert.True(step.After.Steer > 0);
        Assert.True(step.After.WheelMomentum > s.WheelMomentum || step.After.CrankMomentum != s.CrankMomentum);
    }

    [Fact]
    public void HorizontalReceiptRejectsDuplicateForcesAndUnscheduledCollision()
    {
        var plan = new HorizontalPlan214(.3, .004, default, 147, 52.7,
            new(new Vec214(10, 25), new Vec214(.7, 0)), new(new Vec214(400, 0), new Vec214(-.6, 0)), new(-45, 0));
        var right = new ModelHorizontal214().Preview(plan);
        StepContract214.Validate(plan, right);
        Assert.Throws<InvalidOperationException>(() => StepContract214.Validate(plan,
            right with { AppliedImpulse = right.AppliedImpulse * 2 }));
        Assert.Throws<InvalidOperationException>(() => StepContract214.Validate(plan,
            right with { CollisionImpulse = new(1, 0) }));
        Assert.Throws<InvalidOperationException>(() => StepContract214.Validate(plan,
            right with { End = right.End + .002 }));
        var unsupported = new CountingBackend { Collision = new(1, 0) };
        var model = Build(true, 0, unsupported);
        Assert.Throws<InvalidOperationException>(() => model.Step(
            Rest() with { Height = 3, Mode = WheelContact213.Air }, .005, 0));
        Assert.Equal(0, unsupported.Accepted);
    }

    private static State214 Rest() =>
        new(0, default, .55, 0, 0, -4.19, 4.19, 0, 0, 0, Stroke214.Stationary(0, Geometry.RiderNeutral),
            default, [], WheelContact213.Both);

    private static UnifiedModel214 Build(bool isolated, double gravity, IHorizontal214? backend = null)
    {
        var road = StandingWorld144.Frame();
        var rider = StandingWorld144.Rider(StartingGate.C);
        var f = new ForcesModel214(Geometry, rider, new HandlingSettings { H26 = H26Configuration.For("H26-LE") },
            new WorldParameters(), MultiActionExperiment135.Surface(road).SampleSurface(0, 2), 0, isolated, gravity);
        return new(Geometry, f, backend ?? new ModelHorizontal214(), 0);
    }

    private static void Near(double expected, double actual, double error) =>
        Assert.InRange(actual, expected - error, expected + error);

    private sealed class CountingBackend : IHorizontal214
    {
        private readonly ModelHorizontal214 model = new();
        internal Vec214 Collision { get; init; }
        internal int Accepted { get; private set; }
        public HorizontalReceipt214 Preview(HorizontalPlan214 interval) =>
            model.Preview(interval) with { CollisionImpulse = Collision };
        public void Commit(HorizontalPlan214 interval, HorizontalReceipt214 receipt)
        {
            Accepted++;
        }
    }
}
