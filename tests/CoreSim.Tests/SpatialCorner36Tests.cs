
using CoreSim.PhysicalSpace;
using SharedWorldSpeedway;
using SpeedwayHandling;
using Xunit;

namespace CoreSim.Tests;

[Trait("Shard", "core")]
public sealed class SpatialCorner36Tests
{
    private static readonly Geometry214 Bike = Geometry214.Fixture(147, 70,
        TwoContactBikeParameters.Default, RearWheelParameters.Default, 52.72097);

    [Fact]
    public void ProvisionalWitnessAndActualHardPartsAreDifferentEnvelopeTests()
    {
        var figure = new SpatialBike36();
        var old = new Geometry215(Bike, Axes215.All);
        var spatial = new Geometry215(Bike, Axes215.All, spatial: figure);
        var initial = old.Supported(At(old, new(0, 0, .55), -52 * Math.PI / 180),
            LoadBasedTyreGrip.GravityMetersPerSecondSquared, UnifiedModel215.Stiffness);
        Assert.False(old.ChassisClear(initial));
        Assert.True(spatial.ChassisClear(initial));
        Assert.False(spatial.ChassisClear(initial with { Center = initial.Center - Vec215.Up * .30 }));
    }

    [Fact]
    public void ProjectedCapsuleDetectsABoundaryEvenWithAWellClearCenter()
    {
        var frame = StandingWorld144.Frame();
        var track = new RacingLineGeometry(frame);
        var (middle, road) = frame.At(12, 4);
        var p = new MeterPoint(middle.X, middle.Y);
        var inward = new MeterPoint(-road.Outward.X, -road.Outward.Y);
        Assert.True(track.ProjectedClearance([new(p, p, .1)]) > 2);
        Assert.True(track.ProjectedClearance([new(p, p + inward * 7, .1)]) < 0);
    }

    [Fact]
    public void SpatialProjectionTracksTheExtendedBootWithTheRider()
    {
        var g = new Geometry215(Bike, Axes215.All, new Leg216());
        var figure = new SpatialBike36();
        var seed = At(g, new(0, 0, .55), -.3);
        var extended = seed with { Leg = new(.90, 0), Rider = seed.Rider + Vec215.Left * .07 };
        double parkedBottom = figure.Parts(g, seed).Where(p => !p.GroundHard).Min(p => p.Bottom);
        double extendedBottom = figure.Parts(g, extended).Where(p => !p.GroundHard).Min(p => p.Bottom);
        Assert.True(extendedBottom < parkedBottom - .10);
        var leg = g.Leg!.At(g, extended.Leg.Extension);
        var shoe = extended.Center + extended.Pose.Apply(
            extended.Rider * (1 - Bike.RiderFraction) + leg.Shoe);
        var boot = figure.Parts(g, extended).Last();
        Assert.InRange(Math.Abs((boot.A + boot.B).Y / 2 - shoe.Y), 0, .03);
    }

    private static State215 At(Geometry215 g, Vec215 center, double roll) =>
        new(0, center, default, Attitude215.Pose(0, 0, roll), default, g.Neutral, default, 0, 0, 0, default);
}
