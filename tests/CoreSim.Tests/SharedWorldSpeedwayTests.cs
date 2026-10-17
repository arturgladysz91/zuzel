using System.Numerics;
using CoreSim.Analysis;
using CoreSim.Interactions;
using CoreSim.PhysicalSpace;
using SharedWorldSpeedway;
using Xunit;

namespace CoreSim.Tests;

[Trait("Shard", "core")]
public sealed class SharedWorldSpeedwayTests
{
    [Theory]
    [InlineData(.01)] [InlineData(.02)]
    public void FourActualGatesCompleteLapAndHeatInsideTheExistingTrack(double step)
    {
        var scenario = Scenarios.A(4); using var world = new WorldRunner(scenario.Riders);
        var sawFirstBend = new HashSet<int>(); var sawLap = new HashSet<int>();
        var result = world.Run(scenario.Name, step, scenario.DurationSeconds, 4, s =>
        {
            foreach (var v in s.Riders)
            {
                if (v.Context == RacingContext.FirstBend) sawFirstBend.Add(v.Id);
                if (v.TotalReferenceMeters >= world.Frame.LapMeters) sawLap.Add(v.Id);
                Assert.True(float.IsFinite(v.ForwardSpeed) && float.IsFinite(v.SideSpeed));
            }
        });
        Assert.True(result.Completed); Assert.Equal(4, result.FinishTimes.Count);
        Assert.Equal(4, sawFirstBend.Count); Assert.Equal(4, sawLap.Count);
        Assert.All(result.FinishTimes.Values, t => Assert.InRange(t, 50, 80));
        Assert.Empty(result.Contacts); Assert.Equal(0, result.Work.Episodes);
        Assert.True(result.Work.MinimumTrackClearanceMeters > 0);
        var gates = new StartingGateGeometry(world.Frame.Track.Geometry.StraightWidthMeters);
        foreach (var (spec, i) in scenario.Riders.Select((r, i) => (r, i)))
            Assert.Equal(gates.Field((StartingGate)i).CenterPhysicalOffsetMeters - TrackGeometry.InnerReferenceOffsetFromTrackEdgeMeters,
                spec.OffsetMeters, 5);
    }

    [Theory]
    [InlineData(.01)] [InlineData(.02)]
    public void B_RealObservationDelayedControlAndGradualMotionAvoidTheCounterfactualContact(double step)
    {
        var trace = new List<WorldSnapshot>(); var control = new List<WorldSnapshot>(); var s = Scenarios.B();
        using var w = new WorldRunner(s.Riders);
        var result = w.Run(s.Name, step, s.DurationSeconds, acceptedObserver: trace.Add);
        using var baseline = new WorldRunner(Scenarios.B(false).Riders);
        var reference = baseline.Run("B-no-response", step, s.DurationSeconds, acceptedObserver: control.Add);
        var decision = result.Decisions.First();
        Assert.True(decision.ObservedAt > 0); Assert.Equal(decision.ObservedAt, decision.DecisionAt);
        Assert.InRange(decision.RequestedActivation - decision.DecisionAt, .2, .281);
        Assert.InRange(decision.ActualActivation!.Value - decision.RequestedActivation, 0, step);
        Assert.Empty(result.Contacts); Assert.True(reference.Work.SolvedPoints > 0);
        var prefix = trace.TakeWhile(t => t.TimeSeconds <= decision.ActualActivation).ToArray();
        Assert.NotEmpty(prefix);
        for (var i = 0; i < prefix.Length; i++)
        { Assert.Equal(control[i].TimeSeconds, prefix[i].TimeSeconds); Assert.Equal(control[i].Riders, prefix[i].Riders); }
        var before = trace.Last(t => t.TimeSeconds <= decision.ActualActivation).Riders[0];
        var after = trace.First(t => t.TimeSeconds > decision.ActualActivation).Riders[0];
        Assert.InRange(Vector2.Distance(before.Position, after.Position), 0, (float)(30 * step));
        Assert.True(result.Work.MinimumGapMeters > .4);
    }

    [Theory]
    [InlineData(.005)] [InlineData(.01)] [InlineData(.02)]
    public void C_OneLightEpisodePersistsWithoutRepeatedConsequencesOrBounce(double step)
    {
        var r = Scenarios.Run(Scenarios.C(), step);
        Assert.Equal(1, r.Work.Episodes); Assert.Equal(1, r.Work.Consequences);
        var begin = Assert.Single(r.Contacts.Where(c => c.Kind == "begin"));
        Assert.Equal(PhysicalContactSeverity.Brush, PhysicalContactAnalyzer.Classify(begin.SeverityRatioA));
        Assert.True(r.Work.SolvedPoints > 0); Assert.True(r.Work.ContactManifolds > 1);
        Assert.All(r.Riders, v => { Assert.Equal(RiderActivity.Racing, v.Activity); Assert.True(v.DistanceMeters > 35); });
        Assert.All(r.Riders, v => Assert.InRange(Math.Abs(Vector2.Dot(v.Velocity, v.Road.Outward)), 0, .5));
        Assert.InRange(r.Work.MinimumGapMeters, -.005, .01);
    }

    [Theory]
    [InlineData(.01)] [InlineData(.02)]
    public void D_BendImpactUsesSolverApproachSpeedAndExistingReserveThenRecovers(double step)
    {
        var states = new List<WorldSnapshot>(); var s = Scenarios.D(); using var w = new WorldRunner(s.Riders);
        var r = w.Run(s.Name, step, s.DurationSeconds, acceptedObserver: states.Add);
        var begin = r.Contacts.First(c => c.Kind == "begin");
        Assert.Equal(PhysicalContactSeverity.MajorSave, PhysicalContactAnalyzer.Classify(begin.SeverityRatioA));
        Assert.Contains(states, t => t.Riders.Any(v => v.Activity == RiderActivity.Disturbed));
        Assert.All(r.Riders, v => Assert.Equal(RiderActivity.Racing, v.Activity));
        Assert.Equal(r.Work.Episodes, r.Work.Consequences);
        Assert.InRange(r.Work.MinimumGapMeters, -.005, .01);
        Assert.True(r.Work.MinimumTrackClearanceMeters > 0);
    }

    [Theory]
    [InlineData(.005)] [InlineData(.01)] [InlineData(.02)]
    public void E_TwoFastDynamicBodiesDoNotTunnelWithCommonLocalSubsteps(double step)
    {
        var s = Scenarios.E();
        Assert.All(s.Riders, v => Assert.True(v.SpeedMetersPerSecond >= 40));
        var r = Scenarios.Run(s, step);
        Assert.True(r.Work.SolverCalls > r.Work.PhysicsUpdates);
        Assert.True(r.Work.SolvedPoints > 0); Assert.Contains(r.Contacts, c => c.Kind == "begin");
        Assert.InRange(r.Work.MinimumGapMeters, -.005, .01);
        Assert.InRange(r.Work.MaximumPassiveEnergyIncreaseJoules, 0, .1);
        Assert.All(r.Riders, v => Assert.Equal(RiderActivity.Crashed, v.Activity));
    }

    [Theory]
    [InlineData(.01)] [InlineData(.02)]
    public void F_CloseNonclosingTrafficDoesNotInventCorrections(double step)
    {
        var r = Scenarios.Run(Scenarios.F(), step);
        Assert.Empty(r.Decisions); Assert.Empty(r.Contacts); Assert.Equal(0, r.Work.SolvedPoints);
        Assert.Equal(0, r.Work.Threats); Assert.InRange(r.Work.MinimumGapMeters, .099, .101);
        Assert.True(r.Work.MinimumTrackClearanceMeters > 0);
    }

    [Theory]
    [InlineData(.01)] [InlineData(.02)]
    public void G_FourRidersActuallyTraverseFirstBendAndAreOrderIndependent(double step)
    {
        var scenario = Scenarios.G(); var first = Scenarios.Run(scenario, step);
        var repeat = Scenarios.Run(scenario, step); var reversed = Scenarios.Run(scenario, step, reverse: true);
        Assert.Equal(first.Fingerprint, repeat.Fingerprint); Assert.Equal(first.Fingerprint, reversed.Fingerprint);
        Assert.Equal(first.Decisions, reversed.Decisions); Assert.Equal(first.Contacts, reversed.Contacts);
        Assert.All(first.Riders, v => { Assert.True(v.TotalReferenceMeters > 180); Assert.Equal(RiderActivity.Racing, v.Activity); });
        Assert.True(first.Work.MinimumTrackClearanceMeters > 0);
    }

    [Fact]
    public void BulletFlagsAndInternalSubstepsAloneMissTheFastDynamicCrossing()
    {
        var rows = Audit.CcdProbe();
        Assert.Equal(9, rows.Count(r => !r.ExternalProximitySubsteps));
        Assert.All(rows.Where(r => !r.ExternalProximitySubsteps), r =>
        { Assert.Equal(0, r.Result.Work.SolvedPoints); Assert.True(r.TransverseOrderReversed); });
        var safe = Assert.Single(rows.Where(r => r.ExternalProximitySubsteps));
        Assert.True(safe.Result.Work.SolvedPoints > 0);
        Assert.InRange(safe.Result.Work.MinimumGapMeters, -.005, .01);
    }

    [Fact]
    public void SustainedContactUsesSolverSupportWithoutRepeatedGameplayImpact()
    {
        var r = Scenarios.Run(Scenarios.Support(), .02);
        Assert.Equal(1, r.Work.Episodes); Assert.Equal(1, r.Work.Consequences);
        Assert.True(r.Work.SolvedPoints > 30); Assert.True(r.Work.ContactManifolds > 30);
        Assert.InRange(r.Work.MinimumGapMeters, -.005, .001);
    }

    [Fact]
    public void InvalidInputIsRejectedWithoutInventingACrash()
    {
        var s = Scenarios.A();
        Assert.Throws<ArgumentOutOfRangeException>(() => new WorldRunner(s.Riders,
            parameters: new() { NearbyMaximumTravelMeters = float.NaN }));
        Assert.Throws<ArgumentException>(() => new WorldRunner(new[] { s.Riders[0], s.Riders[0] }));
    }

    [Fact]
    public void TrackProjectionPreservesPhysicalOffsetsAndRejectsUnclosedExample()
    {
        var f = new TrackFrame(MatchedVenueProfiles.CreateMotoarenaStandingStartTrack());
        foreach (var s in new[] { 0d, 20, 40, 80, 150, 250, f.LapMeters - 1 })
        {
            var at = f.At(s, 4); var inverse = f.Project(at.Position);
            Assert.InRange(Math.Abs(f.UnwrapDelta(s, inverse.ReferenceMeters)), 0, 2e-5);
            Assert.InRange(Math.Abs(inverse.OffsetMeters - 4), 0, 2e-5);
        }
        Assert.Throws<ArgumentException>(() => new TrackFrame(Track.CreateStandingStartExample()));
    }
}
