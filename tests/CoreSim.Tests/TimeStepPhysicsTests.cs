using CoreSim.PhysicalSpace;
using CoreSim;
using TimeStepPhysics;
using Xunit;
using Xunit.Abstractions;

namespace CoreSim.Tests;

[Trait("Shard", "core")]
public sealed class TimeStepPhysicsTests(ITestOutputHelper output)
{
    private static readonly Lazy<Scenarios.RealOrigin> Real = new(Scenarios.CaptureReal);
    [Theory]
    [InlineData(.005)] [InlineData(.01)]
    public void ScenarioMatrixCompletesOrReturnsASafeStopAndIsDeterministic(double step)
    {
        foreach (var name in Scenarios.Names)
        {
            var race = Scenarios.Create(name, name == "real-R3-R4" ? Real.Value : null);
            var initialTime = race.Time; race.Run(2, step);
            output.WriteLine($"{name} dt={step}: t={race.Time:R}, events={race.Contacts.Count}, stop={race.StopReason}, narrow={race.Work.NarrowTests}, support={race.Work.SupportImpulses}");
            if (name != "real-R3-R4")
            {
                var reverse = Scenarios.Create(name, reverse: true); reverse.Run(2, step);
                Assert.Equal(race.Time, reverse.Time); Assert.Equal(race.StopReason, reverse.StopReason);
                Assert.Equal(race.Bikes, reverse.Bikes); Assert.Equal(race.Contacts, reverse.Contacts);
            }
            Assert.True(race.Time >= initialTime);
            Assert.InRange(race.MaximumPenetration, 0, GeometryNumerics.ContactDistanceMeters);
            Assert.InRange(race.Work.MaximumGripFraction, 0, 1.000000001);
            Assert.All(race.Contacts, c => Assert.True(c.EnergyAfter <= c.EnergyBefore + 1e-6));
            Assert.InRange(race.Work.MaximumContactEnergyGain, 0, 1e-6);
            Assert.True(race.Contacts.Zip(race.Contacts.Skip(1)).All(p => p.First.Time <= p.Second.Time));
            if (name == "real-R3-R4")
            {
                Assert.True(race.Time > initialTime); // the two-second acceptance criterion is reported separately
                Assert.True(race.TerminalCrash, race.LastRejectedWindow); // physical interruption, not two elapsed seconds
                Assert.Null(race.StopReason);
                Assert.Contains(race.Contacts, c => c.SeverityA == CoreSim.Interactions.PhysicalContactSeverity.Crash
                    || c.SeverityB == CoreSim.Interactions.PhysicalContactSeverity.Crash);
                Assert.DoesNotContain(race.Contacts, c => c.Time == initialTime);
                Assert.All(race.Contacts.Where(c => c.A == 3 && c.B == 4), c =>
                    Assert.True(c.ReleaseWitnessMeters > GeometryNumerics.MinimumSeparationToleranceMeters));
                if (race.StopReason is not null) Assert.NotNull(race.LastRejectedWindow);
                output.WriteLine(race.LastRejectedWindow ?? "full horizon");
                output.WriteLine(System.Text.Json.JsonSerializer.Serialize(race.Contacts));
            }
            else if (name is "light-straight" or "bend-contact")
            {
                Assert.Null(race.StopReason); Assert.False(race.TerminalCrash); Assert.Equal(2, race.Time);
                var hit = Assert.Single(race.Contacts); Assert.Equal((1, 2), (hit.A, hit.B));
                Assert.Equal(1, race.Work.Episodes); Assert.Equal(1, race.Work.ConsequenceApplications);
                Assert.True(race.Work.RiderReactions > 0); Assert.True(race.Work.SupportImpulses > 0);
                Assert.All(race.Bikes, b => Assert.Equal(CoreSim.Race.RiderRaceStatus.Racing, b.Status));
                var separation = (race.Bikes[1].Position - race.Bikes[0].Position).Length;
                Assert.True(separation > 1); // recovered actual space, no assigned positions
            }
            else if (race.TerminalCrash)
            {
                Assert.Null(race.StopReason); Assert.Equal(race.Time, race.Contacts[^1].Time);
                Assert.Contains(race.Contacts, c => c.SeverityA == CoreSim.Interactions.PhysicalContactSeverity.Crash
                    || c.SeverityB == CoreSim.Interactions.PhysicalContactSeverity.Crash);
            }
            else if (name == "encroach" && race.StopReason is not null)
            {
                Assert.NotNull(race.LastRejectedWindow); Assert.NotEmpty(race.Contacts);
                Assert.InRange(race.Time, .02, 2); // explicit incomplete contact-solver outcome
            }
            else
            {
                Assert.True(race.StopReason is null, race.LastRejectedWindow); Assert.Equal(initialTime + 2, race.Time);
                Assert.Equal(80, race.Work.Decisions);
                if (name is "straight" or "corner" or "near-miss") Assert.Empty(race.Contacts);
                else Assert.NotEmpty(race.Contacts);
            }
        }
    }

    [Fact]
    public void ContactActsWithinTheStepIncludingLateralImpulseAndFastSweptRearCollision()
    {
        var side = Scenarios.Create("side"); var before = side.Bikes.ToArray(); side.Run(.02, .02);
        var hit = Assert.Single(side.Contacts); Assert.InRange(hit.Time, .000001, .01999999);
        Assert.True(Math.Abs(side.Bikes[0].LateralSpeed - before[0].LateralSpeed) > .1);
        Assert.Equal(before[0].Control.TargetOffset, side.Bikes[0].Control.TargetOffset);
        Assert.True(side.Bikes[0].Control.ReactAt > side.Time); // mechanical response precedes driver reaction
        Assert.All(side.Bikes, b => Assert.True((b.Position - before.Single(p => p.Rider.RiderId == b.Rider.RiderId).Position).Length < 1));

        var fast = Scenarios.Create("rear"); fast.Bikes[0] = fast.Bikes[0] with { Velocity = new(250, 0) };
        fast.Bikes[1] = fast.Bikes[1] with { Velocity = new(0, 0) };
        fast.Run(.02, .02);
        output.WriteLine($"fast: {fast.Time:R}, {fast.StopReason}, {fast.LastRejectedWindow}");
        Assert.NotEmpty(fast.Contacts); Assert.True(fast.Contacts[0].Time < .02);
        Assert.Null(fast.StopReason); Assert.True(fast.TerminalCrash); Assert.Equal(fast.Contacts[0].Time, fast.Time);
        Assert.InRange(fast.MaximumPenetration, 0, GeometryNumerics.ContactDistanceMeters);
    }

    [Fact]
    public void DelayedDriverResponseDoesNotRefreshImpactOrTeleportAndReleaseRearmsWithoutTimer()
    {
        var race = Scenarios.Create("light-straight"); race.Run(.1);
        var hit = Assert.Single(race.Contacts); var b = race.Bikes[0]; var recovery = b.Reaction!;
        Assert.True(recovery.ReactAt > race.Time); Assert.Equal(2, b.Control.TargetOffset);
        var before = b.Position; var beforeVelocity = b.Velocity;
        race.Run(.00001);
        Assert.Equal(recovery, race.Bikes[0].Reaction);
        Assert.True((race.Bikes[0].Position - before - beforeVelocity * .00001).Length < 1e-6);
        Assert.Single(race.Contacts); Assert.Equal(1, race.Work.ConsequenceApplications);

        var input = Scenarios.Create("light-straight");
        foreach (var gap in new[] { .00005, .001 })
        {
            var bikes = input.Bikes.ToArray();
            bikes[0] = bikes[0] with { Velocity = new(18, -.1) };
            bikes[1] = bikes[1] with { Position = bikes[0].Position + new MeterPoint(0, -.8 - gap), Velocity = new(18, .1) };
            var consumed = new TimeStepRace(input.Track, input.SurfaceSnapshot, bikes, parameters: new(VisualResponse: false), consumed: (1, 2));
            var velocity = consumed.Bikes[0].Velocity; consumed.Run(.00001);
            Assert.Equal(0, consumed.Work.SupportImpulses); Assert.Equal(0, consumed.Work.ConsequenceApplications);
            Assert.True(Math.Abs(consumed.Bikes[0].Velocity.Y - velocity.Y) < 1e-4); // no force across free space
            consumed.Run(.02);
            if (gap > GeometryNumerics.MinimumSeparationToleranceMeters)
            { var again = Assert.Single(consumed.Contacts); Assert.True(again.ReleaseWitnessMeters >= gap - 1e-12); }
            else { Assert.Empty(consumed.Contacts); Assert.Equal(0, consumed.Work.ConsequenceApplications); }
        }
    }

    [Fact]
    public void ContactOutcomesConvergeAndRecoveryUsesGripAndAvailableRoom()
    {
        foreach (var name in new[] { "light-straight", "bend-contact", "strong-no-room" })
        {
            var a = Scenarios.Create(name); var b = Scenarios.Create(name); a.Run(2, .005); b.Run(2, .01);
            Assert.Equal(a.TerminalCrash, b.TerminalCrash); Assert.Equal(a.Contacts.Count, b.Contacts.Count);
            Assert.True(Math.Abs(a.Contacts[0].Time - b.Contacts[0].Time) < .0001);
            Assert.All(a.Bikes.Zip(b.Bikes), p => Assert.True((p.First.Position - p.Second.Position).Length < .05));
        }
        var free = Scenarios.Create("light-straight"); free.Run(.1);
        double Request(TimeStepRace r) => r.Bikes[1].Reaction!.TargetOffset - r.Road(r.Bikes[1].Position).Offset;
        var input = Scenarios.Create("light-straight"); var edgeBikes = input.Bikes.ToArray();
        for (var i = 0; i < 2; i++) edgeBikes[i] = edgeBikes[i] with
        { Position = edgeBikes[i].Position + new MeterPoint(0, -.9), Control = new(edgeBikes[i].Control.TargetOffset + .9) };
        var edge = new TimeStepRace(input.Track, input.SurfaceSnapshot, edgeBikes); edge.Run(.1);
        Assert.True(Request(edge) < Request(free));
        var slippery = new TrackState(input.Track.Segments.Count, 5, (_, _) => new(.4f, .2f, .1f)).Snapshot();
        var weak = new TimeStepRace(input.Track, slippery, input.Bikes); weak.Run(.1);
        Assert.True(Request(weak) < Request(free));
        Assert.Equal(3, new[] { Scenarios.ContextFor("light-straight"), Scenarios.ContextFor("bend-contact"),
            Scenarios.ContextFor("strong-no-room") }.Distinct().Count());
    }

    [Fact]
    public void ProjectionFloorBoundsRotatingCapsulesAndCannotMissBetweenEndpointCrossing()
    {
        var race = Scenarios.Create("light-straight"); var random = new Random(714);
        for (var trial = 0; trial < 40; trial++)
        {
            var a = race.Bikes[0] with { Heading = random.NextDouble() * 6, AngularVelocity = random.NextDouble() * 8 - 4 };
            var b = race.Bikes[1] with { Heading = random.NextDouble() * 6, AngularVelocity = random.NextDouble() * 8 - 4 };
            var ma = new TimeStepRace.Motion(race, a, new(new(3, -7), 5), 0, .02);
            var mb = new TimeStepRace.Motion(race, b, new(new(-4, 9), -3), 0, .02);
            var floor = race.SeparationBound(ma, mb, 0, .02);
            for (var sample = 0; sample <= 100; sample++)
                Assert.True(floor <= MechanicalSeparation.Between(ma.SampleFootprint(sample * .0002), mb.SampleFootprint(sample * .0002)).SignedMeters + 1e-12);
        }
        var still = race.Bikes[0] with { Velocity = new(0, 0) };
        var crossing = still with { Position = still.Position + new MeterPoint(0, 2), Velocity = new(0, -400) };
        var sa = new TimeStepRace.Motion(race, still, new(new(0, 0), 0), 0, .01);
        var sb = new TimeStepRace.Motion(race, crossing, new(new(0, 0), 0), 0, .01);
        Assert.True(MechanicalSeparation.Between(sa.Sample(0), sb.Sample(0)).SignedMeters > 0);
        Assert.True(MechanicalSeparation.Between(sa.Sample(.01), sb.Sample(.01)).SignedMeters > 0);
        Assert.True(race.SeparationBound(sa, sb, 0, .01) < -GeometryNumerics.ContactDistanceMeters);
    }

    [Fact]
    public void ParametersChangePhysicalMotionAndImportPreservesAlreadyAppliedEffects()
    {
        var real = Real.Value; var imported = Scenarios.Import(real);
        Assert.Empty(imported.Contacts);
        foreach (var bike in imported.Bikes)
        {
            var old = real.First.Riders.Single(r => r.Rider.RiderId == bike.Rider.RiderId);
            Assert.InRange(Math.Abs(old.Rider.Speed - bike.Speed), 0, 1e-12);
            Assert.Equal(old.Rider.ContactRecovery?.ControlLoss01 ?? 0, bike.ControlLoss);
        }
        var baseline = Scenarios.Create("straight"); var altered = Scenarios.Create("straight");
        var bike0 = altered.Bikes[0];
        altered.Bikes[0] = bike0 with { Rider = bike0.Rider with { Profile = bike0.Rider.Profile with
            { Skills = new(50, 90, 50, 50, 50, 50) }, ActiveSetup = new(.1f, .9f) } };
        baseline.Run(.5); altered.Run(.5);
        Assert.True(altered.Bikes[0].Speed > baseline.Bikes[0].Speed);
        Assert.NotEqual(altered.Bikes[0].Position, baseline.Bikes[0].Position);

        var heavy = Scenarios.Create("straight"); var rider = heavy.Bikes[0].Rider; var gameplay = rider.Profile.Gameplay;
        heavy.Bikes[0] = heavy.Bikes[0] with { Rider = rider with { Profile = RiderProfile.CreateCanonical(1, "heavy",
            new(gameplay.Abilities, new(120), gameplay.InteractionStyle), rider.Profile.Skills, rider.Profile.Style) } };
        heavy.Run(.5); Assert.True(heavy.Bikes[0].Speed < baseline.Bikes[0].Speed);

        var corner = Scenarios.Create("corner"); var track = CoreSim.Track.CreateStandingStartExample();
        var icy = new TrackState(track.Segments.Count, 5, (_, _) => new(.4f, .2f, .1f)).Snapshot();
        var lowerGrip = new TimeStepRace(track, icy, corner.Bikes);
        corner.Run(.5); lowerGrip.Run(.5); Assert.NotEqual(corner.Bikes[0].Position, lowerGrip.Bikes[0].Position);
        var beforeSegment = corner.Road(corner.Bikes[0].Position).Segment; corner.Run(1.5);
        Assert.NotEqual(beforeSegment, corner.Road(corner.Bikes[0].Position).Segment);
    }
}
