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
    [InlineData(.005)] [InlineData(.01)] [InlineData(.02)]
    public void ScenarioMatrixCompletesOrReturnsASafeStopAndIsDeterministic(double step)
    {
        foreach (var name in Scenarios.Names)
        {
            var race = Scenarios.Create(name, name == "real-R3-R4" ? Real.Value : null);
            var initialTime = race.Time; race.Run(2, step);
            output.WriteLine($"{name} dt={step}: t={race.Time:R}, events={race.Contacts.Count}, stop={race.StopReason}, narrow={race.Work.NarrowTests}, support={race.Work.SupportImpulses}");
            var reverse = Scenarios.Create(name, name == "real-R3-R4" ? Real.Value : null, true); reverse.Run(2, step);
            Assert.Equal(race.Time, reverse.Time); Assert.Equal(race.StopReason, reverse.StopReason);
            Assert.Equal(race.Bikes, reverse.Bikes); Assert.Equal(race.Contacts, reverse.Contacts);
            Assert.True(race.Time >= initialTime);
            Assert.InRange(race.MaximumPenetration, 0, GeometryNumerics.ContactDistanceMeters);
            Assert.InRange(race.Work.MaximumGripFraction, 0, 1.000000001);
            Assert.All(race.Contacts, c => Assert.True(c.EnergyAfter <= c.EnergyBefore + 1e-6));
            Assert.InRange(race.Work.MaximumContactEnergyGain, 0, 1e-6);
            Assert.True(race.Contacts.Zip(race.Contacts.Skip(1)).All(p => p.First.Time <= p.Second.Time));
            if (name == "real-R3-R4")
            {
                Assert.True(race.Time > initialTime); // the two-second acceptance criterion is reported separately
                Assert.NotNull(race.StopReason); // current PoC does NOT satisfy the real two-second target
                Assert.DoesNotContain(race.Contacts, c => c.Time == initialTime);
                Assert.All(race.Contacts.Where(c => c.A == 3 && c.B == 4), c =>
                    Assert.True(c.ReleaseWitnessMeters > GeometryNumerics.MinimumSeparationToleranceMeters));
                if (race.StopReason is not null) Assert.NotNull(race.LastRejectedWindow);
                output.WriteLine(race.LastRejectedWindow ?? "full horizon");
                output.WriteLine(System.Text.Json.JsonSerializer.Serialize(race.Contacts));
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
        Assert.Null(fast.StopReason); Assert.Equal(.02, fast.Time);
        Assert.InRange(fast.MaximumPenetration, 0, GeometryNumerics.ContactDistanceMeters);
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
