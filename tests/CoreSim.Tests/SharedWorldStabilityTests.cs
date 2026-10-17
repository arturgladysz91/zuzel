using SharedWorldSpeedway;
using System.Numerics;
using CoreSim;
using CoreSim.Interactions;
using CoreSim.PhysicalSpace;
using Xunit;

namespace CoreSim.Tests;

[Trait("Shard", "core")]
[Collection("Box2D worlds")]
public sealed class SharedWorldStabilityTests
{
    [Fact]
    public void OptionalForceAndContactObserversDoNotChangeAcceptedPhysics()
    {
        var s = Scenarios.Support();
        var expected = Scenarios.Run(s, .01); var forces = 0; var points = 0;
        using var world = new WorldRunner(s.Riders, diagnostics: new() {
            Force = _ => forces++, ContactPoint = _ => points++, Consequence = _ => { } });
        var actual = world.Run(s.Name, .01, s.DurationSeconds);
        Assert.Equal(expected.Fingerprint, actual.Fingerprint);
        Assert.Equal(expected.Contacts, actual.Contacts);
        Assert.True(forces > 0); Assert.True(points > 0);
    }

    [Fact]
    public void RoadFrameDoesNotRotatePropulsionIntoSmallLateralLaunchVelocityAndBoundsCombinedForce()
    {
        var s = Scenarios.DenseStart(); using var w = new WorldRunner(s.Riders);
        var surface = TrackState.CreateDefault(w.Frame.Track).Snapshot();
        var v = w.Snapshot.Riders[0] with { Velocity = new(.1f, -.001f) };
        var a = RiderController.Forces(v, s.Riders[0], w.Frame, surface, w.Parameters, 1, 142, 50);
        var b = RiderController.Forces(v with { Velocity = new(.1f, .001f) }, s.Riders[0], w.Frame, surface, w.Parameters, 1, 142, 50);
        Assert.InRange(Math.Abs(a.Force.X - b.Force.X), 0, .001f);
        ForceObservation? observed = null;
        RiderController.Forces(v with { Velocity = new(20, -30) }, s.Riders[0], w.Frame, surface, w.Parameters,
            1, 142, 50, o => observed = o);
        Assert.NotNull(observed); Assert.True(observed.Value.GripBudgetNewtons > 0);
        Assert.InRange(observed.Value.ForceNewtons.Length() / observed.Value.GripBudgetNewtons!.Value, .99999, 1.000001);
    }

    [Theory]
    [InlineData(.02)] [InlineData(.01)] [InlineData(.005)]
    public void OffGridTapeDeadlineIsAnActualCommonWorldBoundary(double step)
    {
        var riders = Scenarios.A().Riders.Take(2).ToArray(); var r = riders[0];
        var gameplay = new RiderGameplayProfile(new(57, 50, 70, 65, 70, 65, 50, 60),
            r.Profile.Gameplay.Physical, r.Profile.Gameplay.InteractionStyle);
        riders[0] = r with { Profile = RiderProfile.CreateCanonical(1, "R1", gameplay, r.Profile.Skills, RiderStyle.Balanced) };
        riders[1] = riders[1] with { ReferenceMeters = 150, Passive = true };
        var deadline = RiderController.TapeReactionSeconds(riders[0]);
        var before = new List<WorldSnapshot>(); double? firstForce = null;
        using var w = new WorldRunner(riders, diagnostics: new() { Force = o => {
            if (o.Rider.Id == 1 && o.Phase == "racing") firstForce ??= o.TimeSeconds;
        } });
        var result = w.Run("off-grid", step, .4, acceptedObserver: before.Add);
        Assert.Equal(deadline, firstForce!.Value, 10);
        Assert.Contains(before, s => Math.Abs(s.TimeSeconds - deadline) < 1e-12);
        Assert.All(before.Where(s => s.TimeSeconds <= deadline), s => Assert.Equal(Vector2.Zero, s.Riders[0].Velocity));
        Assert.Empty(result.Contacts);
    }

    [Fact]
    public void UnchangedDenseInputHasBoundedEarlyMotionAndDecreasingStepError()
    {
        var s = Scenarios.DenseStart(); var runs = new List<SortedDictionary<int, WorldSnapshot>>();
        foreach (var step in new[] { .02, .01, .005 })
        {
            var samples = new SortedDictionary<int, WorldSnapshot>(); using var w = new WorldRunner(s.Riders);
            var result = w.Run(s.Name, step, s.DurationSeconds, acceptedObserver: snapshot => {
                if (Math.Abs(snapshot.TimeSeconds / .02 - Math.Round(snapshot.TimeSeconds / .02)) < 1e-8)
                    samples[(int)Math.Round(snapshot.TimeSeconds / .02)] = snapshot;
            });
            Assert.Empty(result.Contacts); Assert.All(result.Riders, v => Assert.Equal(RiderActivity.Racing, v.Activity));
            Assert.True(result.Work.MinimumTrackClearanceMeters > 0); runs.Add(samples);
        }
        var errors = new double[2];
        for (var k = 0; k < 2; k++) foreach (var (time, a) in runs[k])
        {
            var b = runs[2][time];
            for (var i = 0; i < a.Riders.Count; i++)
            {
                var dp = Vector2.Distance(a.Riders[i].Position, b.Riders[i].Position);
                errors[k] = Math.Max(errors[k], dp);
                if (a.TimeSeconds <= 1.2)
                {
                    Assert.InRange(dp, 0, SpeedwayBikeDimensions.Reference.ChassisBodyWidthMeters / 10);
                    Assert.InRange(Vector2.Distance(a.Riders[i].Velocity, b.Riders[i].Velocity), 0, .03);
                }
            }
        }
        Assert.True(errors[1] < errors[0] / 2);
        var repeat = Scenarios.Run(s, .02); var reverse = Scenarios.Run(s, .02, reverse: true);
        Assert.Equal(repeat.Fingerprint, reverse.Fingerprint);
        Assert.Equal(repeat.Decisions, reverse.Decisions);
    }

    [Theory]
    [InlineData(.02)] [InlineData(.01)] [InlineData(.005)]
    public void FiniteBendImpactUsesPeakPointApproachRatherThanFirstFractionOfImpulse(double step)
    {
        var points = new List<ContactPointObservation>(); var consequences = new List<ConsequenceObservation>();
        var s = Scenarios.D(); using var w = new WorldRunner(s.Riders, diagnostics: new() {
            ContactPoint = points.Add, Consequence = consequences.Add });
        var result = w.Run(s.Name, step, s.DurationSeconds);
        var begin = result.Contacts.First(c => c.Kind == "begin");
        var impact = points.Where(p => p.TimeSeconds <= begin.TimeSeconds + .04 && p.TotalNormalImpulseNewtonSeconds > 0).ToArray();
        Assert.NotEmpty(impact);
        Assert.Equal(impact.Max(p => -(double)p.NormalVelocityMetersPerSecond), begin.NormalClosingSpeed, 5);
        var first = impact[0];
        Assert.InRange(Math.Abs(first.NormalVelocityMetersPerSecond - first.PreStepPointNormalVelocityMetersPerSecond), 0, .001);
        Assert.InRange(begin.NormalClosingSpeed, 3.89, 3.91);
        Assert.InRange(begin.SeverityRatioA, 1.49, 1.50);
        Assert.Equal(2 * result.Work.Episodes, consequences.Count);
        Assert.Equal(result.Work.Episodes, result.Work.Consequences);
    }

    [Fact]
    public void RearmRequiresRealCapsuleSeparationAndContextsFollowPhysicalTrackProgress()
    {
        var s = Scenarios.DenseStart(); s = s with { Riders = s.Riders.Select(r => r with { TrafficEnabled = false }).ToArray() };
        var states = new Dictionary<double, WorldSnapshot>(); using var w = new WorldRunner(s.Riders);
        var result = w.Run(s.Name, .02, s.DurationSeconds, acceptedObserver: snapshot => states.Add(snapshot.TimeSeconds, snapshot));
        var active = new HashSet<(int, int)>(); var releases = 0;
        foreach (var contact in result.Contacts)
        {
            var pair = (contact.RiderA, contact.RiderB);
            if (contact.Kind == "begin") { Assert.True(active.Add(pair)); continue; }
            Assert.True(active.Remove(pair)); releases++;
            var snapshot = states[contact.TimeSeconds]; var a = snapshot.Riders.Single(r => r.Id == pair.RiderA);
            var b = snapshot.Riders.Single(r => r.Id == pair.RiderB);
            PhysicalBikePose Pose(RiderView v) => new(v.Id, "test", new(v.Position.X, v.Position.Y),
                new(0, v.Heading, v.Heading), SpeedwayBikeDimensions.Reference);
            Assert.True(MechanicalSeparation.Between(Pose(a), Pose(b)).SignedMeters > .02);
        }
        Assert.True(releases > 1); Assert.Equal(result.Work.Episodes, result.Work.Consequences);
        var firstCorner = w.Frame.Track.CornerTopology.Corners[0];
        var start = w.Frame.Embedding.Segments.Take(firstCorner.StartSegmentIndex).Sum(s => s.StraightLengthMeters);
        Assert.Equal(RacingContext.StartStraight, w.Frame.Context(w.Frame.At(start - .01, 3).Road, start - .01));
        Assert.Equal(RacingContext.FirstBend, w.Frame.Context(w.Frame.At(start + .01, 3).Road, start + .01));
        Assert.Equal(RacingContext.NormalRacing, w.Frame.Context(w.Frame.At(start + .01, 3).Road, w.Frame.LapMeters + start + .01));
    }

    [Theory]
    [InlineData(.95, false)] [InlineData(1.05, true)]
    public void SmallPhysicalClosingChangeCrossesUnchangedCrashThresholdConsistently(double speedFactor, bool crash)
    {
        var s = Scenarios.D(); s = s with { Riders = s.Riders.Select((r, i) => i == 0
            ? r with { InitialOutwardSpeed = r.InitialOutwardSpeed * (float)speedFactor } : r).ToArray() };
        var ratios = new List<double>();
        foreach (var step in new[] { .02, .01, .005 })
        {
            var result = Scenarios.Run(s, step); var begin = result.Contacts.First(c => c.Kind == "begin");
            ratios.Add(begin.SeverityRatioA);
            Assert.Equal(crash, PhysicalContactAnalyzer.Classify(begin.SeverityRatioA) == PhysicalContactSeverity.Crash);
            Assert.All(result.Riders, v => Assert.Equal(crash, v.Activity == RiderActivity.Crashed));
        }
        Assert.InRange(ratios.Max() - ratios.Min(), 0, .005);
    }
}

// The pinned managed port has a process-wide world registry. Creation/destruction
// in unrelated xUnit classes must not race; each simulated world still has one owner.
[CollectionDefinition("Box2D worlds", DisableParallelization = true)]
public sealed class Box2DWorldCollection { }
