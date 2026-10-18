using System.Numerics;
using SharedWorldSpeedway;
using Xunit;

namespace CoreSim.Tests;

[Trait("Shard", "core")]
[Collection("Box2D worlds")]
public sealed class SharedWorldDynamicsTests
{
    [Theory]
    [InlineData(.02)] [InlineData(.01)] [InlineData(.005)]
    public void OneActualBodyUsesBox2DSemiImplicitForceIntegration(double step)
    {
        var rider = Scenarios.A().Riders[0] with { OffsetMeters = 3, TargetOffsetMeters = 3, Passive = true };
        var frame = new TrackFrame(CoreSim.Analysis.MatchedVenueProfiles.CreateMotoarenaStandingStartTrack());
        using var physics = new ContactAdapter(new[] { rider }, frame, new());
        physics.Force(0, new(physics.Masses[0] * 10, 0), 0); physics.Step((float)step, 4);
        var actual = physics.Read(0);
        Assert.InRange(Math.Abs(actual.Position.X - .625 * 10 * step * step), 0, 1e-7);
        Assert.InRange(Math.Abs(actual.Velocity.X - 10 * step), 0, 1e-7);
        Assert.InRange(Math.Abs(actual.Position.Y + 3), 0, 1e-7);
    }

    [Theory]
    [InlineData(.5f)] [InlineData(3f)] [InlineData(9.5f)]
    public void ProjectionRecognizesHalfMillimetreProgressAcrossTangentBoundary(float offset)
    {
        var f = new TrackFrame(CoreSim.Analysis.MatchedVenueProfiles.CreateMotoarenaStandingStartTrack());
        var start = f.Embedding.Segments[0].StraightLengthMeters;
        Assert.False(f.Project(f.At(start - .0005, offset).Position).IsTurn);
        Assert.True(f.Project(f.At(start + .0005, offset).Position).IsTurn);
        Assert.InRange(f.DistanceToCurvatureChange(f.At(start - .01, offset).Road), .00999, .01001);
    }

    [Fact]
    public void AuthoredCommandsAreCausalAtOffGridDeadlinesAndDoNotAlterAttributes()
    {
        var c = DynamicsAudit.Cases().Single(c => c.Scenario.Name == "C-lap-scripted");
        foreach (var step in new[] { .02, .01, .005 })
        {
            var trace = new List<WorldSnapshot>(); var r = DynamicsAudit.Execute(c, step, observer: trace.Add);
            Assert.True(r.Completed); Assert.Single(r.Riders); Assert.Empty(r.Contacts);
            Assert.Equal(2, r.Decisions.Count);
            foreach (var command in r.Decisions)
            {
                Assert.Equal(command.RequestedActivation, command.ActualActivation!.Value, 10);
                Assert.Contains(trace, s => Math.Abs(s.TimeSeconds - command.RequestedActivation) < 1e-12);
                Assert.Equal(0, command.DecisionAt);
            }
            Assert.All(trace.Where(s => s.TimeSeconds < 8.013), s => Assert.Equal(1, s.Riders[0].Drive));
            Assert.All(trace.Where(s => s.TimeSeconds > 8.013 && s.TimeSeconds < 8.347), s => Assert.Equal(.9f, s.Riders[0].Drive));
            Assert.All(trace.Where(s => s.TimeSeconds > 8.347), s => Assert.Equal(1, s.Riders[0].Drive));
        }
        Assert.Throws<ArgumentException>(() => new WorldRunner(Scenarios.G().Riders,
            controls: new[] { new ControlCommand(1, 1, 3, .9f) }));
    }

    [Fact]
    public void SmallLocalBoundaryStepsImproveLapPositionVelocityHeadingAndFinishTogether()
    {
        var c = DynamicsAudit.Cases().Single(c => c.Scenario.Name == "C-lap-scripted");
        var samples = new List<Dictionary<int, RiderView>>(); var results = new List<RunResult>();
        foreach (var step in new[] { .02, .01, .005 })
        {
            var states = new Dictionary<int, RiderView>();
            results.Add(DynamicsAudit.Execute(c, step, observer: s => {
                if (Math.Abs(s.TimeSeconds / .02 - Math.Round(s.TimeSeconds / .02)) < 1e-8)
                    states[(int)Math.Round(s.TimeSeconds / .02)] = s.Riders[0];
            }));
            samples.Add(states);
            Assert.True(results[^1].Work.SolverCalls - results[^1].Work.PhysicsUpdates < 20);
        }
        var errors = new List<(double Position, double Velocity, double Heading)>();
        for (var k = 0; k < 2; k++)
        {
            double position = 0, velocity = 0, heading = 0;
            foreach (var (time, a) in samples[k])
            {
                if (!samples[2].TryGetValue(time, out var b)) continue;
                position = Math.Max(position, Vector2.Distance(a.Position, b.Position));
                velocity = Math.Max(velocity, Vector2.Distance(a.Velocity, b.Velocity));
                heading = Math.Max(heading, Math.Abs(CoreSim.PhysicalSpace.BikeAngles.Wrap(a.Heading - b.Heading)));
            }
            errors.Add((position, velocity, heading));
            Assert.InRange(position, 0, .3); Assert.InRange(velocity, 0, .15); Assert.InRange(heading, 0, Math.PI / 180);
            Assert.InRange(Math.Abs(results[k].FinishTimes[1] - results[2].FinishTimes[1]), 0, .015);
        }
        Assert.True(errors[1].Position < errors[0].Position);
        Assert.True(errors[1].Velocity < errors[0].Velocity);
        Assert.True(errors[1].Heading < errors[0].Heading);
    }
}
