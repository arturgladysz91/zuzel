using System.Numerics;
using SharedWorldSpeedway;
using SpeedwayHandling;
using Xunit;

namespace CoreSim.Tests;

[Trait("Shard", "core")]
public sealed class DynamicPitch213Tests
{
    private const double Mass = 147;
    private static PitchBike213 Bike(double foreAft = 0, double lower = 0) =>
        PitchBike213.FixedRider(Mass, 72, TwoContactBikeParameters.Default, .33, foreAft, lower);

    [Fact]
    public void StandingSupportMatchesWeightAndMomentBalance()
    {
        var bike = Bike();
        var load = PitchDynamics213.Loads(bike, bike.Rest, 0);
        Assert.Equal(WheelContact213.Both, load.Normal.Mode);
        Assert.Equal(Mass * PitchDynamics213.Gravity, load.Normal.Front + load.Normal.Rear, 9);
        AssertNear(0, bike.Front.X * load.Normal.Front + bike.Rear.X * load.Normal.Rear, 1e-10);
        AssertNear(0, load.PitchAcceleration, 1e-10);
        AssertNear(0, load.VerticalAcceleration, 1e-10);
    }

    [Fact]
    public void SupportedStatesTransferLoadAndFrontLiftRequiresPositiveSeparationAcceleration()
    {
        var bike = Bike();
        double lift = -bike.Rear.X * Mass * PitchDynamics213.Gravity;
        var below = PitchDynamics213.Loads(bike, bike.Rest, .99 * lift);
        Assert.Equal(WheelContact213.Both, below.Normal.Mode);
        Assert.True(below.Normal.Front > 0);
        var above = PitchDynamics213.Loads(bike, bike.Rest, 1.02 * lift);
        Assert.Equal(WheelContact213.Rear, above.Normal.Mode);
        Assert.Equal(0, above.Normal.Front);
        Assert.True(above.FrontGapAcceleration > 0);
        AssertNear(0, above.RearGapAcceleration, 1e-10);
        var airborne = PitchDynamics213.Move(bike, bike.Rest, above.Normal.Mode, 1.02 * lift, .015);
        Assert.True(airborne.Angle > 0);
        Assert.True(bike.Front.At(airborne.Angle).Gap(airborne) > 0);
        AssertNear(0, bike.Rear.At(airborne.Angle).Gap(airborne), 1e-12);
    }

    [Fact]
    public void StrongRearUnloadingAdmitsFrontOnlySupport()
    {
        var bike = Bike();
        double frontModeMoment = -1.03 * bike.Front.X * Mass * PitchDynamics213.Gravity;
        var result = PitchDynamics213.Loads(bike, bike.Rest, frontModeMoment);
        Assert.Equal(WheelContact213.Front, result.Normal.Mode);
        Assert.Equal(0, result.Normal.Rear);
        var pose = PitchDynamics213.Move(bike, bike.Rest, result.Normal.Mode, frontModeMoment, .02);
        Assert.True(bike.Rear.At(pose.Angle).Gap(pose) > 0);
        AssertNear(0, bike.Front.At(pose.Angle).Gap(pose), 1e-12);
    }

    [Fact]
    public void AnInelasticLandingConservesMomentumWithOnlyTheGroundImpulse()
    {
        var bike = Bike();
        var before = bike.Rest with { Rate = -.55, VerticalSpeed = -.33 };
        var impact = PitchDynamics213.Land(bike, before);
        Assert.Equal(WheelContact213.Both, impact.Impulse.Mode);
        Assert.Equal(before.Height, impact.After.Height);
        Assert.Equal(before.Angle, impact.After.Angle);
        AssertNear(0, impact.After.VerticalSpeed, 1e-11);
        AssertNear(0, impact.After.Rate, 1e-11);
        Assert.InRange(impact.EnergyLostJoules, 10, 20);
        AssertNear(0, impact.MomentumError, 1e-11);
        AssertNear(0, impact.PitchImpulseError, 1e-11);
        AssertNear(0, impact.WorkIdentityError, 1e-11);
    }

    [Fact]
    public void FixedForwardAndLoweredPositionsRaiseTheUprightLiftThreshold()
    {
        double Threshold(PitchBike213 bike) => -bike.Rear.X * PitchDynamics213.Gravity / bike.Height;
        Assert.True(Threshold(Bike(-.18)) < Threshold(Bike()));
        Assert.True(Threshold(Bike(.18)) > Threshold(Bike()));
        Assert.True(Threshold(Bike(lower: .14)) > Threshold(Bike()));
        Assert.True(PitchDynamics213.Loads(Bike(.18), Bike(.18).Rest, 0).Normal.Front >
            PitchDynamics213.Loads(Bike(-.18), Bike(-.18).Rest, 0).Normal.Front);
    }

    [Fact]
    public void SyntheticFlightProducesNoTyreForceOrFrontYawMomentAndSteeringStillMoves()
    {
        var frame = StandingWorld144.Frame();
        var rider = StandingWorld144.Rider(StartingGate.C);
        var bike = Bike();
        var position = frame.At(42, 8);
        var body = new RiderView(1, position.Position, 18 * position.Road.Tangent,
            (float)position.Road.Heading, 0, position.Road, 42, 42, default, 0, 8, 0);
        var start = new State213(4, body, bike.Rest with { Height = bike.Height + .20 }, 540, 60, 0, 0);
        var model = new PitchModel213(bike, frame, rider, MultiActionExperiment135.Surface(frame).SampleSurface(0, 2),
            new HandlingSettings { H26 = H26Configuration.For("H26-LE") }, new WorldParameters(), 52.72097, 0);
        var result = model.Advance(start, new(.22f, .85f), .005);
        Assert.Equal(WheelContact213.Air, result.Forces.Support);
        Assert.Equal(Vector2.Zero, result.Forces.Front);
        Assert.Equal(Vector2.Zero, result.Forces.Rear);
        Assert.Equal(0, result.Forces.FrontMomentMaximum);
        Assert.Equal(0, result.Forces.YawMoment);
        Assert.True(result.After.Steer > 0);
        Assert.True(result.After.Body.Speed < start.Body.Speed);
        Assert.True(result.After.Wheel != start.Wheel);
        // Bound energy rounding at the retained float XY state boundary.
        float speed = start.Body.Speed;
        double planarRounding = Mass * speed * (MathF.BitIncrement(speed) - speed) * Math.Sqrt(2);
        AssertNear(0, result.EnergyResidual, planarRounding);
        double AngularMomentum(State213 s) => bike.Inertia * s.Pitch.Rate
            - RearWheelParameters.Default.WheelInertiaKgMetersSquared * (s.Wheel - s.Pitch.Rate)
            - LaunchParameters144.Default.CrankInertiaKgMetersSquared * (s.Crank - s.Pitch.Rate);
        AssertNear(AngularMomentum(start), AngularMomentum(result.After), 1e-5);
        Assert.Equal(result, model.Advance(start, new(.22f, .85f), .005));
    }

    [Fact]
    public void ReducedSupportIntegratorConvergesUnderStepRefinement()
    {
        var bike = Bike();
        double moment = -1.3 * bike.Rear.X * Mass * PitchDynamics213.Gravity;
        PitchState213 Run(int pieces)
        {
            var p = bike.Rest;
            for (int i = 0; i < pieces; i++)
                p = PitchDynamics213.Move(bike, p, WheelContact213.Rear, moment, .24 / pieces);
            return p;
        }
        double fine = Run(64).Angle;
        double coarse = Math.Abs(Run(2).Angle - fine);
        double medium = Math.Abs(Run(8).Angle - fine);
        Assert.True(coarse > 8 * medium);
        Assert.True(fine > 0);
    }

    private static void AssertNear(double expected, double actual, double tolerance) =>
        Assert.InRange(actual, expected - tolerance, expected + tolerance);
}
