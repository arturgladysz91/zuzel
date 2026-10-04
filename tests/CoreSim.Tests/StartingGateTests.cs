using CoreSim;
using CoreSim.Analysis;
using CoreSim.Decisions;
using CoreSim.Logging;
using CoreSim.Race;
using Xunit;
using static CoreSim.Tests.StandingStartFixture;

namespace CoreSim.Tests;

[Trait("Shard", "core")]
public sealed class StartingGateTests
{
    [Theory]
    [InlineData(12f, 3f)]
    [InlineData(10f, 2.5f)]
    public void FieldsPartitionFullPhysicalWidthWithoutGapsOrOverlappingOwnership(float width, float fieldWidth)
    {
        var geometry = new StartingGateGeometry(width);
        Assert.Equal(4, geometry.Fields.Count);
        Assert.Equal(0f, geometry.Fields[0].LowerPhysicalOffsetMeters);
        Assert.Equal(width, geometry.Fields[^1].UpperPhysicalOffsetMeters);
        Assert.Equal(width, geometry.Fields.Sum(field => field.NominalWidthMeters));
        for (var index = 0; index < 4; index++)
        {
            var field = geometry.Fields[index];
            Assert.Equal((StartingGate)index, field.Gate);
            Assert.Equal(index * fieldWidth, field.LowerPhysicalOffsetMeters);
            Assert.Equal((index + 1) * fieldWidth, field.UpperPhysicalOffsetMeters);
            Assert.Equal((index + .5f) * fieldWidth, field.CenterPhysicalOffsetMeters);
            Assert.True(field.Contains(field.CenterPhysicalOffsetMeters));
            Assert.Single(geometry.Fields.Where(item => item.Contains(field.LowerPhysicalOffsetMeters)));
            if (index > 0) Assert.Equal(geometry.Fields[index - 1].UpperPhysicalOffsetMeters, field.LowerPhysicalOffsetMeters);
        }
        Assert.Single(geometry.Fields.Where(field => field.Contains(width)));
        Assert.Empty(geometry.Fields.Where(field => field.Contains(-.01f) || field.Contains(width + .01f)));
        Assert.Equal(new[] { fieldWidth, 2 * fieldWidth, 3 * fieldWidth }, geometry.InternalDividingLineOffsetsMeters);
        Assert.Equal(.05f, StartingGateGeometry.DividingLineWidthMeters);
        Assert.Equal(1f, StartingGateGeometry.BackwardMarkingLengthMeters);
    }

    [Theory]
    [InlineData(StartingGate.A, 1.5f, .2f)]
    [InlineData(StartingGate.B, 4.5f, 1.4f)]
    [InlineData(StartingGate.C, 7.5f, 2.6f)]
    [InlineData(StartingGate.D, 10.5f, 3.8f)]
    public void MotoarenaCentersUseExistingPhysicalReferenceTransform(StartingGate gate, float center, float expected)
    {
        var geometry = MatchedVenueProfiles.Motoarena2026.CreateGeometry();
        var field = new StartingGateGeometry(geometry.StraightWidthMeters).Field(gate);
        Assert.Equal(center, field.CenterPhysicalOffsetMeters);
        var lateral = StartingGateGeometry.CenterLateralPosition(gate, geometry);
        Assert.Equal(expected, lateral, 5);
        Assert.InRange(lateral, 0f, 4f);
        Assert.Equal(center, LaneModel.PhysicalLateralOffsetFromInnerEdgeMeters(lateral, SegmentType.Straight, geometry), 5);
    }

    [Theory]
    [InlineData(0f)]
    [InlineData(-1f)]
    [InlineData(float.NaN)]
    [InlineData(float.PositiveInfinity)]
    [InlineData(float.Epsilon)]
    public void InvalidPhysicalWidthsAreRejected(float width)
        => Assert.Throws<ArgumentOutOfRangeException>(() => new StartingGateGeometry(width));

    [Fact]
    public void LargeFiniteWidthKeepsFiniteCenters()
        => Assert.All(new StartingGateGeometry(float.MaxValue).Fields,
            field => Assert.True(float.IsFinite(field.CenterPhysicalOffsetMeters)));

    [Fact]
    public void InvalidGateOrUnrepresentableSyntheticCenterIsNotSilentlyClamped()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new StartingGateGeometry(12f).Field((StartingGate)4));
        Assert.Throws<ArgumentOutOfRangeException>(() => StartingGateGeometry.CenterLateralPosition(
            StartingGate.A, Track.CreateExample().Geometry));
        Assert.Throws<ArgumentException>(() => new RiderState(RiderProfile.CreateDefault(1), StartingGate.B, Track.CreateExample()));
    }

    [Fact]
    public void ExplicitFourRiderGridIsNeutralAndSnapshotPreservesAssignment()
    {
        var track = MatchedVenueProfiles.CreateMotoarenaStandingStartTrack();
        var riders = StartingGrid.Create(track, Assignments());
        var engine = new SimulationEngine(new GateAwareDecision());
        var snapshot = engine.CaptureSnapshot(track, TrackState.CreateDefault(track, Perfect), riders,
            new SimulationStepContext(52, 0, 0, 0, 52, 4));
        Assert.Equal(4, snapshot.Riders.Select(rider => rider.StartingGate).Distinct().Count());
        Assert.Equal(4, snapshot.Riders.Select(rider => rider.StartingPosition!.CenterPhysicalOffsetMeters).Distinct().Count());
        foreach (var rider in snapshot.Riders)
        {
            Assert.Equal(track.StartFinishLine, rider.Position);
            Assert.Equal(0d, rider.CanonicalProgress);
            Assert.Equal(0f, rider.DistanceMeters);
            Assert.Equal(0f, rider.Speed);
            Assert.Equal(0f, rider.ElapsedTimeSeconds);
            Assert.Equal(RiderRaceStatus.NotStarted, rider.Status);
            var field = rider.StartingPosition!;
            var physical = LaneModel.PhysicalLateralOffsetFromInnerEdgeMeters(rider.LateralPosition, SegmentType.Straight, track.Geometry);
            Assert.True(field.Contains(physical));
            Assert.Equal(field.CenterPhysicalOffsetMeters, physical, 5);
            Assert.Equal(Assignments().Single(assignment => assignment.Profile.Id == rider.RiderId).Gate, rider.StartingGate);
        }
        // Legacy decision adapters must also see explicit identity, not a reconstructed lane-as-gate.
        Assert.Equal(4, engine.Decide(snapshot).Count);
        riders[0].ResetForHeat(0);
        Assert.Null(riders[0].StartingGate);
        Assert.NotNull(snapshot.Riders[0].StartingGate); // Detached immutable starting metadata.
        riders[0].ResetForHeat(StartingGate.D, track);
        Assert.Equal(StartingGate.D, riders[0].StartingGate);
        Assert.Equal(3.8f, riders[0].LateralPosition, 5);
        Assert.Equal(track.StartFinishLine, riders[0].Position);
    }

    [Fact]
    public void GridRejectsDuplicateRidersDuplicateGatesMissingOrInvalidFields()
    {
        var track = MatchedVenueProfiles.CreateMotoarenaStandingStartTrack();
        var assignments = Assignments();
        Assert.Throws<ArgumentException>(() => StartingGrid.Create(track, assignments.Take(3).ToArray()));
        var duplicate = assignments.ToArray();
        duplicate[1] = duplicate[1] with { Gate = StartingGate.A };
        Assert.Throws<ArgumentException>(() => StartingGrid.Create(track, duplicate));
        duplicate = assignments.ToArray();
        duplicate[1] = duplicate[1] with { Profile = duplicate[0].Profile };
        Assert.Throws<ArgumentException>(() => StartingGrid.Create(track, duplicate));
        duplicate = assignments.ToArray();
        duplicate[1] = duplicate[1] with { Gate = (StartingGate)9 };
        Assert.Throws<ArgumentOutOfRangeException>(() => StartingGrid.Create(track, duplicate));
    }

    [Fact]
    public void ProductionLaunchSamplesContinuousGateCenterAndAllowsImmediateNormalLateralMovement()
    {
        var track = MatchedVenueProfiles.CreateMotoarenaStandingStartTrack();
        var state = new TrackState(track.Segments.Count, 5,
            (_, lane) => new TrackSurfaceState(.4f + .1f * lane, .02f * lane, .35f));
        var riders = StartingGrid.Create(track, Assignments());
        var engine = new SimulationEngine(new GateAwareDecision());
        var snapshot = engine.CaptureSnapshot(track, state, riders, new SimulationStepContext(52, 0, 0, 0, 52, 4));
        var options = Options();
        var step = engine.Resolve(snapshot, engine.Decide(snapshot), options);
        foreach (var diagnostic in step.Diagnostics)
        {
            var rider = snapshot.Rider(diagnostic.RiderId);
            var sampled = snapshot.TrackState.SampleSurface(0, rider.LateralPosition);
            Assert.Equal(sampled, diagnostic.EntrySurface);
            Assert.Equal(.4f + .1f * rider.LateralPosition, sampled.Grip, 5);
            var launch = Assert.IsType<StandingStartLaunchProfile>(diagnostic.StandingStartLaunchProfile);
            Assert.Equal(LongitudinalDynamics.CalculateStandingStartReactionTimeSeconds(rider.Profile.Skills), launch.ReactionTimeSeconds);
            var change = step.Changes.Single(item => item.RiderId == rider.RiderId);
            Assert.Equal(0f, change.EntrySpeed);
            Assert.Equal(RiderRaceStatus.Racing, change.Status);
            var path = Assert.IsType<ExecutedSegmentPath>(diagnostic.ExecutedPath);
            Assert.True(change.Position.DistanceMeters > 35f);
            Assert.Equal(path.DistanceMeters, change.Position.DistanceMeters);
            Assert.Equal(path.Nodes[^1].LateralPosition, change.LateralPosition);
            Assert.Equal(path.MovementTimeSeconds, launch.MovementTimeSeconds);
            Assert.Contains(path.Steps, s => s.SampledSurface != sampled);
        }
        foreach (var gate in new[] { StartingGate.A, StartingGate.D })
        {
            var before = snapshot.Riders.Single(rider => rider.StartingGate == gate);
            var change = step.Changes.Single(item => item.RiderId == before.RiderId);
            Assert.False(before.StartingPosition!.Contains(LaneModel.PhysicalLateralOffsetFromInnerEdgeMeters(
                change.LateralPosition, SegmentType.Straight, track.Geometry)));
        }
        engine.Commit(step, riders, state, new SimLog());
        Assert.All(riders, rider => { Assert.Equal(RiderRaceStatus.Racing, rider.Status); Assert.NotNull(rider.StartingGate); });
    }

    [Fact]
    public void GateIdentityHasNoPerformanceEffectBeyondInitialPosition()
    {
        var track = MatchedVenueProfiles.CreateMotoarenaStandingStartTrack();
        var gated = StartingGrid.Create(track, Assignments());
        var compatibility = gated.Select(rider => new RiderState(rider.Profile, rider.Lane)
            { LateralPosition = rider.LateralPosition }).ToArray();
        var engine = new SimulationEngine(new HoldLane(0));
        ResolvedSimulationStep ResolveRiders(IReadOnlyList<RiderState> riders)
        {
            var snapshot = engine.CaptureSnapshot(track, TrackState.CreateDefault(track, Perfect), riders,
                new SimulationStepContext(52, 0, 0, 0, 52, 4));
            return engine.Resolve(snapshot, engine.Decide(snapshot), Options());
        }
        var actual = ResolveRiders(gated);
        var control = ResolveRiders(compatibility);
        Assert.Equal(control.Changes, actual.Changes);
        Assert.Equal(control.Diagnostics, actual.Diagnostics); // Includes TimeTo70, SpeedAt2s, all forces and preparation.
        Assert.Equal(control.Events, actual.Events);
    }

    [Fact]
    public void All24AssignmentAndRiderOrderPermutationsIncludingReverseProduceIdenticalFullHeatEvidence()
    {
        var track = MatchedVenueProfiles.CreateMotoarenaStandingStartTrack();
        var assignments = Assignments();
        var options = Options(52) with { EnableLogging = true };
        string? expectedTrace = null;
        RiderHeatResult[]? expectedClassification = null;
        string[]? expectedLog = null;
        float[]? expectedFinalLateral = null;
        TrackSurfaceState[]? expectedSurface = null;
        var count = 0;
        foreach (var permutation in Permutations(new[] { 0, 1, 2, 3 }))
        {
            var grid = StartingGrid.Create(track, permutation.Select(index => assignments[index]).ToArray());
            var riders = permutation.Select(index => grid[index]).ToList();
            var state = TrackState.CreateDefault(track, Perfect);
            var collector = new CalibrationTraceCollector(track, options, 52);
            var result = new HeatSimulator(new GateAwareDecision()).SimulateHeat(track, state, riders, options, 52, collector);
            var trace = CalibrationCsvExporter.ExportSteps(collector.Complete(result));
            var classification = result.Classification.ToArray();
            var log = result.Log.Lines.ToArray();
            var lateral = riders.OrderBy(rider => rider.RiderId).Select(rider => rider.LateralPosition).ToArray();
            var surfaces = Enumerable.Range(0, track.Segments.Count)
                .SelectMany(segment => Enumerable.Range(0, 5).Select(lane => state.GetSurface(segment, lane))).ToArray();
            expectedTrace ??= trace; expectedClassification ??= classification; expectedLog ??= log; expectedFinalLateral ??= lateral;
            expectedSurface ??= surfaces;
            Assert.Equal(expectedTrace, trace);
            Assert.Equal(expectedClassification, classification);
            Assert.Equal(expectedLog, log);
            Assert.Equal(expectedFinalLateral, lateral);
            Assert.Equal(expectedSurface, surfaces);
            Assert.All(riders, rider => Assert.Equal(RiderRaceStatus.Finished, rider.Status));
            count++;
        }
        Assert.Equal(24, count);
    }

    [Fact]
    public void GateGridIsOnlyTiePresentationAndBreakingInitialTieDoesNotCreateAnOvertake()
    {
        var track = MatchedVenueProfiles.CreateMotoarenaStandingStartTrack();
        var riders = StartingGrid.Create(track, Assignments());
        foreach (var rider in riders) rider.Lane = 0; // Deliberately not a source of gate identity.
        var tracker = new RaceProgressTracker();
        var log = new SimLog();
        tracker.InitializeStartingGrid(riders.Reverse().ToArray());
        Assert.Equal(new[] { 23, 4, 61, 9 }, tracker.StartingGridOrder);
        tracker.CaptureSegment(1, 0, false, riders, log); // An optional pre-tape capture must not consume tie protection.
        Assert.Empty(log.Overtakes);
        foreach (var rider in riders)
        {
            rider.RestorePosition(RiderPosition.Create(1, 1, 0f, track.Segments.Count, 35f));
            rider.ElapsedTimeSeconds = rider.StartingGate switch { StartingGate.D => 1f, StartingGate.C => 2f, StartingGate.B => 3f, _ => 4f };
        }
        tracker.CaptureSegment(1, 0, false, riders, log);
        Assert.Empty(log.Overtakes);
        var inside = riders.Single(rider => rider.StartingGate == StartingGate.A);
        inside.ElapsedTimeSeconds = .5f;
        tracker.CaptureSegment(1, 1, true, riders, log);
        Assert.Equal(3, log.Overtakes.Count);
        Assert.All(log.Overtakes, item => Assert.Equal(inside.RiderId, item.RiderId));
        Assert.Equal(new[] { 23, 9, 61, 4 }, Assert.Single(log.OrderSnapshots).Order.Select(item => item.RiderId));
    }

    [Fact]
    public void CurrentMotoarena35And27PreservesAllFixedLineFlyingLapGeometryAndCanonicalBoundary()
    {
        var current = MatchedVenueProfiles.CreateMotoarenaStandingStartTrack();
        var historical = MatchedVenueProfiles.Motoarena2026.CreateTrack(31f); // Explicit historical control, not current default.
        Assert.Equal(35f, MatchedVenueProfiles.MotoarenaPrimaryStartLineToFirstCornerMeters);
        Assert.Equal(35f, current.Segments[0].StraightLengthMetersOverride);
        Assert.Equal(27f, current.Segments[^1].StraightLengthMetersOverride);
        Assert.Equal(62f, current.Segments[0].StraightLengthMetersOverride + current.Segments[^1].StraightLengthMetersOverride);
        Assert.Equal(62f, current.Geometry.StraightLengthMeters);
        Assert.Equal(historical.Geometry, current.Geometry);
        for (var lane = 0; lane <= 4; lane++)
        {
            var oldLap = MotoarenaMatchedVenueCalibration.LapDistance(historical, lane);
            var newLap = MotoarenaMatchedVenueCalibration.LapDistance(current, lane);
            Assert.Equal(oldLap, newLap, 4);
            foreach (var flyingLap in new[] { 2, 3, 4 })
                Assert.Equal(oldLap * flyingLap, newLap * flyingLap, 3);
        }
        var nextLap = RiderPosition.Create(2, 0, 0f, current.Segments.Count);
        Assert.Equal(current.StartFinishLine.SegmentIndex, nextLap.SegmentIndex);
        Assert.Equal(current.StartFinishLine.SegmentProgress, nextLap.SegmentProgress);
        Assert.Equal(1, nextLap.LapsCompleted);
        Assert.Contains("FIMConstrainedStartLineBaseline", MatchedVenueProfiles.Motoarena2026.StartLineConfidenceNotes);
        Assert.Contains("ExactMotoarenaOffsetNotPubliclyVerified", MatchedVenueProfiles.Motoarena2026.StartLineConfidenceNotes);
    }

    private static StartingGateAssignment[] Assignments() => new[]
    {
        new StartingGateAssignment(RiderProfile.CreateDefault(23), StartingGate.A),
        new StartingGateAssignment(RiderProfile.CreateDefault(4), StartingGate.B),
        new StartingGateAssignment(RiderProfile.CreateDefault(61), StartingGate.C),
        new StartingGateAssignment(RiderProfile.CreateDefault(9), StartingGate.D),
    };

    private sealed class GateAwareDecision : IRiderDecisionModel
    {
        public RiderDecision Decide(TrackSegment segment, RiderState rider)
        {
            Assert.NotNull(rider.StartingGate);
            return new RiderDecision(rider.StartingGate == StartingGate.A ? 4 : 0, 0f);
        }
    }
}
