using CoreSim.Analysis;
using CoreSim.Decisions;
using CoreSim.Logging;
using CoreSim.Race;
using Xunit;

namespace CoreSim.Tests;

[Trait("Shard", "core")]
public sealed class ResolvedRiderMotionTests
{
    [Theory]
    [InlineData(SegmentType.Straight, 2f, 2)]
    [InlineData(SegmentType.Straight, 0f, 1)]
    [InlineData(SegmentType.TurnEntry, 2f, 2)]
    [InlineData(SegmentType.TurnEntry, 3f, 0)]
    public void FixedAndMovingShareTheSameMonotonicReadOnlySampler(SegmentType type, float lateral, int target)
    {
        var step = One(type, lateral, target, 18f);
        Reconcile(step);
        var motion = step.Motions.Single();
        Assert.True(motion.Nodes.Count > 2);
        var before = step.Diagnostics.Single();
        for (var i = 0; i <= 100; i++)
        {
            var time = motion.TotalTimeSeconds * (i / 100f);
            Assert.Equal(motion.SampleAtTime(time), motion.SampleAtTime(time));
        }
        Assert.Equal(before, step.Diagnostics.Single());
        Assert.Throws<ArgumentOutOfRangeException>(() => motion.SampleAtTime(-1f));
        Assert.Throws<ArgumentOutOfRangeException>(() => motion.SampleAtTime(MathF.BitIncrement(motion.TotalTimeSeconds)));
        Assert.Throws<ArgumentOutOfRangeException>(() => motion.SampleAtTime(float.NaN));
        Assert.Throws<ArgumentOutOfRangeException>(() => motion.SampleAtTime(float.PositiveInfinity));
    }

    [Theory]
    [InlineData(StartingGate.A, 1.5f)] [InlineData(StartingGate.B, 4.5f)]
    [InlineData(StartingGate.C, 7.5f)] [InlineData(StartingGate.D, 10.5f)]
    public void StandingMotionStartsAtGateAndOccupiesItThroughoutReaction(StartingGate gate, float center)
    {
        var track = MatchedVenueProfiles.CreateMotoarenaStandingStartTrack();
        var rider = new RiderState(RiderProfile.CreateDefault(1), gate, track);
        var step = Resolve(track, new[] { rider }, new Targets(2));
        Reconcile(step);
        var motion = step.Motions.Single();
        Assert.Equal(center, motion.Initial.PhysicalOffsetFromInnerEdgeMeters);
        Assert.Equal(rider.LateralPosition, motion.Initial.LateralPosition);
        var reaction = step.Diagnostics.Single().StandingStartLaunchProfile!.Value.ReactionTimeSeconds;
        foreach (var time in new[] { 0f, reaction * .5f, reaction })
        {
            var sample = motion.SampleAtTime(time);
            Assert.Equal(center, sample.PhysicalOffsetFromInnerEdgeMeters);
            Assert.Equal(0d, sample.CanonicalProgress); Assert.Equal(0f, sample.TravelledMeters);
            Assert.Equal(0f, sample.SpeedMetersPerSecond);
        }
        Assert.True(motion.SampleAtTime(reaction + .1f).TravelledMeters > 0f);
    }

    [Fact]
    public void FixedLaunchAlsoContainsStationaryReaction()
    {
        var track = MatchedVenueProfiles.CreateMotoarenaStandingStartTrack();
        var step = Resolve(track, new[] { new RiderState(RiderProfile.CreateDefault(1), 2) }, new Targets(2));
        Reconcile(step);
        Assert.Null(step.Diagnostics[0].ExecutedPath);
        var motion = step.Motions[0];
        Assert.Equal(0f, motion.SampleAtTime(motion.ReactionTimeSeconds * .5f).TravelledMeters);
        Assert.Equal(0f, motion.SampleAtTime(motion.ReactionTimeSeconds).SpeedMetersPerSecond);
    }

    [Fact]
    public void CrossingIsDemonstratedOnlyBySimultaneousMotionSamples()
    {
        var diagnostic = MotionFoundationDiagnostics.Crossing();
        Assert.True(diagnostic.LateralOrderReversed);
        Assert.InRange(diagnostic.BracketStartSeconds, 0f, diagnostic.BracketEndSeconds);
        Assert.InRange(diagnostic.BracketEndSeconds - diagnostic.BracketStartSeconds, 0f, 1e-6f);
        Assert.Equal(diagnostic.RiderOne.LocalTimeSeconds, diagnostic.RiderTwo.LocalTimeSeconds);
        // Numerical representation tolerance only; no contact radius/threshold.
        Assert.Equal(diagnostic.RiderOne.PhysicalOffsetMeters, diagnostic.RiderTwo.PhysicalOffsetMeters, 5);
        Assert.Equal(diagnostic.RiderOne.SegmentProgress, diagnostic.RiderTwo.SegmentProgress, 5);
        var step = MotionFoundationDiagnostics.CrossingStep();
        Assert.Equal(step.Diagnostics[0].TravelledMeters, step.Diagnostics[1].TravelledMeters, 5);
        Assert.DoesNotContain(step.Events, e => e.Type != SimulationEventType.SegmentResolved);
        Reconcile(step);
    }

    [Fact]
    public void Audit51LeaderRetainsFifteenMetresAtTheSameCommonTime()
    {
        var samples = MotionFoundationDiagnostics.DistantLeader();
        Assert.Equal(15f, samples[0].LongitudinalSeparationMeters);
        Assert.InRange(samples[1].LongitudinalSeparationMeters, 14.9f, 15f);
        Assert.All(samples, s =>
        {
            Assert.Equal(s.Leader.LocalTimeSeconds, s.Follower.LocalTimeSeconds);
            Assert.True(s.Leader.CanonicalProgress > s.Follower.CanonicalProgress);
        });
    }

    [Theory]
    [InlineData(2f, 2)] [InlineData(1.4f, 2)]
    public void PartialCrashHasExplicitTerminalSpeedAndSkipsWear(float lateral, int target)
    {
        var step = One(SegmentType.TurnEntry, lateral, target, 50f, .25f);
        Reconcile(step);
        Assert.Equal(.625f, step.Changes[0].Position.SegmentProgress);
        Assert.Equal(RiderRaceStatus.Crashed, step.Changes[0].Status);
        Assert.False(step.Changes[0].ApplySurfaceWear);
        var motion = step.Motions[0];
        Assert.Equal(0f, motion.Final.SpeedMetersPerSecond);
        Assert.Contains(motion.StateTransitions, t => t.Kind == MotionStateTransitionKind.TerminalCrash);
        Assert.True(motion.SampleAtTime(MathF.BitDecrement(motion.TotalTimeSeconds)).SpeedMetersPerSecond > 0f);
    }

    [Fact]
    public void RunWideMotionRepresentsTheResolvedOutwardPath()
    {
        var step = Enumerable.Range(22, 22).Select(speed => One(SegmentType.TurnEntry, 1f, 1, speed))
            .First(s => s.Changes[0].Outcome == SegmentOutcome.RunWide);
        Reconcile(step);
        Assert.True(step.Motions[0].Final.LateralPosition > step.Motions[0].Initial.LateralPosition);
        Assert.All(step.Diagnostics[0].ExecutedPath!.Nodes, n => Assert.Contains(step.Motions[0].Nodes,
            m => m.LateralPosition == n.LateralPosition && m.TravelledMeters == n.DistanceMeters));
    }

    [Theory]
    [InlineData(0, 1, 5f, 7.3f)] [InlineData(3, 4, 7.3f, 5f)]
    public void WidthTransitionIsExplicitAndNeverSwept(int from, int to, float oldOffset, float newOffset)
    {
        var track = MatchedVenueProfiles.CreateMotoarenaStandingStartTrack();
        var rider = new RiderState(RiderProfile.CreateDefault(1), 2) { Speed = 18f };
        rider.RestorePosition(RiderPosition.Create(1, from, 0f, track.Segments.Count));
        var state = TrackState.CreateDefault(track, new TrackSurfaceState(1f, 0f, .35f));
        var engine = new SimulationEngine(new Targets(2));
        var first = Resolve(track, new[] { rider }, new Targets(2), from);
        var boundary = Assert.IsType<MotionBoundaryTransition>(first.Motions[0].ExitBoundary);
        Assert.True(boundary.HasPhysicalOffsetDiscontinuity);
        Assert.Equal(oldOffset, boundary.FromPhysicalOffsetMeters, 5);
        Assert.Equal(newOffset, boundary.ToPhysicalOffsetMeters, 5);
        engine.Commit(first, new[] { rider }, state, new SimLog());
        var next = Resolve(track, new[] { rider }, new Targets(2), to);
        Assert.Equal(boundary, next.Motions[0].EntryBoundary);
        Assert.Equal(oldOffset, first.Motions[0].Final.PhysicalOffsetMeters, 5);
        Assert.Equal(newOffset, next.Motions[0].Initial.PhysicalOffsetMeters, 5);
        Assert.Equal(first.Motions[0].Final.CanonicalProgress, next.Motions[0].Initial.CanonicalProgress);
        Assert.Equal(0f, next.Motions[0].Initial.TravelledMeters);
        Reconcile(first); Reconcile(next);
    }

    [Fact]
    public void FinishedAndInactiveSemanticsAreExplicit()
    {
        var step = One(SegmentType.Straight, 2f, 2, 18f, .4f);
        Reconcile(step);
        Assert.Equal(RiderRaceStatus.Finished, step.Changes[0].Status);
        Assert.Null(step.Motions[0].ExitBoundary);
        var track = step.Snapshot.Track;
        var rider = new RiderState(RiderProfile.CreateDefault(1), 2);
        rider.SetStatus(RiderRaceStatus.Finished);
        Assert.Empty(Resolve(track, new[] { rider }, new Targets(2)).Motions);
    }

    [Fact]
    public void RepeatReverseAndIncidentOffSeedPreserveAllMotionAndSamples()
    {
        var baseline = MotionFoundationDiagnostics.CrossingStep();
        foreach (var step in new[] { MotionFoundationDiagnostics.CrossingStep(), MotionFoundationDiagnostics.CrossingStep(31, true) })
        {
            Assert.Equal(baseline.Changes, step.Changes); Assert.Equal(baseline.Diagnostics, step.Diagnostics);
            Assert.Equal(baseline.Motions, step.Motions);
            for (var i = 0; i < baseline.Motions.Count; i++)
                for (var k = 0; k <= 10; k++)
                {
                    var time = baseline.Motions[i].TotalTimeSeconds * (k / 10f);
                    Assert.Equal(baseline.Motions[i].SampleAtTime(time), step.Motions[i].SampleAtTime(time));
                }
        }
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void MixedFixedMovingAndGatedLaunchAreIndependentOfOrderAndIncidentOffSeed(bool launch)
    {
        ResolvedSimulationStep Run(int seed, bool reverse)
        {
            var track = MatchedVenueProfiles.CreateMotoarenaStandingStartTrack();
            var riders = launch
                ? StartingGrid.Create(track, Enumerable.Range(0, 4).Select(i =>
                    new StartingGateAssignment(RiderProfile.CreateDefault(i + 1), (StartingGate)i)).ToArray()).ToArray()
                : Enumerable.Range(1, 4).Select(i => new RiderState(RiderProfile.CreateDefault(i), i - 1) { Speed = 18f }).ToArray();
            foreach (var rider in riders) rider.ElapsedTimeSeconds = rider.RiderId * 100f;
            var engine = new SimulationEngine(new MixedTargets());
            var snapshot = engine.CaptureSnapshot(track, TrackState.CreateDefault(track, new TrackSurfaceState(1f, 0f, .35f)),
                reverse ? riders.Reverse().ToArray() : riders, new SimulationStepContext(53, 0, 0, 0, seed, 1));
            return engine.Resolve(snapshot, engine.Decide(snapshot), new HeatSimulationOptions { Laps = 1, IncidentFrequency = 0f });
        }
        var a = Run(0, false); var b = Run(31, true);
        Assert.Equal(a.Changes, b.Changes); Assert.Equal(a.Diagnostics, b.Diagnostics); Assert.Equal(a.Motions, b.Motions);
        Reconcile(a); Reconcile(b);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void ExistingContactConsequencesAreExplicitEventsAndReconcileTheFinalState(bool crash)
    {
        var track = new Track(new[] { new TrackSegment(0, SegmentType.TurnMiddle) });
        ResolvedSimulationStep Run(int seed)
        {
            var riders = Enumerable.Range(1, 2).Select(i => new RiderState(RiderProfile.CreateDefault(i), 2) { Speed = 18f }).ToArray();
            var engine = new SimulationEngine(new Targets(2));
            var snapshot = engine.CaptureSnapshot(track, TrackState.CreateDefault(track, new TrackSurfaceState(1f, 0f, .35f)), riders,
                new SimulationStepContext(53, 0, 0, 0, seed, 1));
            return engine.Resolve(snapshot, engine.Decide(snapshot), new HeatSimulationOptions { Laps = 1, IncidentFrequency = 0f });
        }
        var type = crash ? SimulationEventType.ContactCrash : SimulationEventType.ContactLostRhythm;
        var step = Enumerable.Range(0, 200).Select(Run).First(s => s.Events.Any(e => e.Type == type));
        Reconcile(step);
        var motion = step.Motions.Single(m => m.RiderId == step.Events.Single(e => e.Type == type).RiderId);
        var transition = Assert.Single(motion.StateTransitions.Where(t => t.Kind == MotionStateTransitionKind.ExistingContact));
        Assert.Equal(transition.Before.CanonicalProgress, transition.After.CanonicalProgress);
        Assert.Equal(transition.Before.TravelledMeters, transition.After.TravelledMeters);
        Assert.Equal(transition.Before.LocalTimeSeconds, transition.After.LocalTimeSeconds);
        if (!crash)
        {
            Assert.True(motion.TotalTimeSeconds > transition.After.LocalTimeSeconds);
            Assert.Equal(transition.After.TravelledMeters, motion.SampleAtTime(motion.TotalTimeSeconds).TravelledMeters);
        }
    }

    [Fact]
    public void AllFourLapProductionMotionsReconcileWithCurrentTypedResults()
    {
        var track = MatchedVenueProfiles.CreateMotoarenaStandingStartTrack();
        var riders = new[] { new RiderState(RiderProfile.CreateDefault(1), StartingGate.C, track) };
        var engine = new SimulationEngine(new MixedTargets());
        for (var i = 0; i < track.Segments.Count * 4; i++)
        {
            var state = TrackState.CreateDefault(track, new TrackSurfaceState(1f, 0f, .35f));
            var snapshot = engine.CaptureSnapshot(track, state, riders,
                new SimulationStepContext(53, i, i / track.Segments.Count, i % track.Segments.Count, 0, 4));
            var step = engine.Resolve(snapshot, engine.Decide(snapshot), new HeatSimulationOptions { Laps = 4, IncidentFrequency = 0f });
            Reconcile(step);
            engine.Commit(step, riders, state, new SimLog());
        }
    }

    [Fact]
    public void EvidenceIsDeterministicAndMatchesTheCheckedInReport()
    {
        var evidence = MotionFoundationDiagnostics.Evidence();
        Assert.Equal(evidence, MotionFoundationDiagnostics.Evidence());
        Assert.Equal(File.ReadAllText(Path.Combine(Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../")),
            "docs/calibration/resolved-rider-motion.json")).Replace("\r\n", "\n", StringComparison.Ordinal), evidence);
    }

    private static void Reconcile(ResolvedSimulationStep step)
    {
        foreach (var motion in step.Motions)
        {
            var change = step.Changes.Single(c => c.RiderId == motion.RiderId);
            var d = step.Diagnostics.Single(c => c.RiderId == motion.RiderId);
            var entry = step.Snapshot.Rider(motion.RiderId);
            Assert.Equal(0f, motion.Initial.LocalTimeSeconds);
            Assert.Equal(entry.CanonicalProgress, motion.Initial.CanonicalProgress);
            Assert.Equal(change.Position.TotalSegmentProgress, motion.Final.CanonicalProgress);
            Assert.Equal(change.LateralPosition, motion.Final.LateralPosition);
            Assert.Equal(change.Speed, motion.Final.SpeedMetersPerSecond);
            Assert.Equal(d.TravelTimeSeconds, motion.TotalTimeSeconds);
            Assert.Equal(d.TravelledMeters, motion.TotalDistanceMeters);
            Assert.Equal(motion.Initial, motion.SampleAtTime(0f));
            Assert.Equal(motion.Final, motion.SampleAtTime(motion.TotalTimeSeconds));
            foreach (var group in motion.Nodes.GroupBy(n => n.LocalTimeSeconds).Where(g => g.Key > 0f))
                Assert.Equal(group.Last(), motion.SampleAtTime(group.Key));
            for (var i = 1; i < motion.Nodes.Count; i++)
            {
                var a = motion.Nodes[i - 1]; var b = motion.Nodes[i];
                Assert.True(b.LocalTimeSeconds >= a.LocalTimeSeconds);
                Assert.True(b.CanonicalProgress >= a.CanonicalProgress); Assert.True(b.TravelledMeters >= a.TravelledMeters);
                if (b.LocalTimeSeconds == a.LocalTimeSeconds) continue;
                var sample = motion.SampleAtTime((float)(((double)a.LocalTimeSeconds + b.LocalTimeSeconds) * .5d));
                Assert.InRange(sample.CanonicalProgress, a.CanonicalProgress, b.CanonicalProgress);
                Assert.InRange(sample.TravelledMeters, a.TravelledMeters, b.TravelledMeters);
                if (sample.RadiusMeters.HasValue) Assert.Equal(1f / sample.RadiusMeters.Value, sample.CurvaturePerMeter);
            }
        }
    }
    private static ResolvedSimulationStep One(SegmentType type, float lateral, int target, float speed, float progress = 0f)
    {
        var track = new Track(new[] { new TrackSegment(0, type) });
        var rider = new RiderState(RiderProfile.CreateDefault(1), (int)MathF.Round(lateral)) { LateralPosition = lateral, Speed = speed };
        rider.RestorePosition(RiderPosition.Create(1, 0, progress, 1));
        return Resolve(track, new[] { rider }, new Targets(target));
    }
    private static ResolvedSimulationStep Resolve(Track track, IReadOnlyList<RiderState> riders, IRiderDecisionModel model, int segment = 0)
    {
        var engine = new SimulationEngine(model);
        var snapshot = engine.CaptureSnapshot(track, TrackState.CreateDefault(track, new TrackSurfaceState(1f, 0f, .35f)), riders,
            new SimulationStepContext(53, segment, 0, segment, 0, 1));
        return engine.Resolve(snapshot, engine.Decide(snapshot), new HeatSimulationOptions { Laps = 1, IncidentFrequency = 0f });
    }
    private sealed class Targets(int target) : IRiderDecisionModel
    {
        public RiderDecision Decide(TrackSegment segment, RiderState rider) => new(target, 0f);
    }
    private sealed class MixedTargets : IRiderDecisionModel
    {
        public RiderDecision Decide(TrackSegment segment, RiderState rider)
            => new(rider.RiderId % 2 == 0 ? rider.Lane : segment.Type == SegmentType.Straight ? 4 : 0, 0f);
    }
}
