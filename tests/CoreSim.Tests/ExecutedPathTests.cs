using System.Text.Json;
using CoreSim.Analysis;
using CoreSim.Decisions;
using CoreSim.Logging;
using CoreSim.Race;
using CoreSim.Setup;
using Xunit;

namespace CoreSim.Tests;

public sealed class ExecutedPathTests
{
    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)] [InlineData(4)]
    public void AllMotoarenaFixedLinesPreserveEveryFloatInProductionDiagnostics(int lane)
    {
        var expected = JsonSerializer.Deserialize<FixedLineResult[]>(File.ReadAllText(Path.Combine(Root,
            "docs/calibration/executed-trajectory-fixed-line-before.json")))!.Single(r => r.Lane == lane);
        var actual = FixedLineCompatibility.Run().Single(r => r.Lane == lane);
        Assert.Equal(JsonSerializer.Serialize(expected), JsonSerializer.Serialize(actual));
    }

    [Fact]
    public void StraightGeometryIsEuclideanSymmetricAndPreservesZeroMovementExactly()
    {
        var track = Motoarena(); var segment = track.Segments[4];
        Assert.Equal(62f, ExecutedPathTraversal.GeometricDistance(segment, track.Geometry, 2f, 2f, 1d));
        var inward = ExecutedPathTraversal.GeometricDistance(segment, track.Geometry, 3f, 2f, 1d);
        Assert.Equal(inward, ExecutedPathTraversal.GeometricDistance(segment, track.Geometry, 1f, 2f, 1d));
        Assert.Equal((float)Math.Sqrt(62d * 62d + 2.5d * 2.5d), inward);
        Assert.True(inward > 62f);
    }

    [Theory]
    [InlineData(0f)] [InlineData(1f)] [InlineData(2f)] [InlineData(3f)] [InlineData(4f)]
    public void ConstantTurnGeometryIsExactlyTheExistingArc(float lateral)
    {
        var track = Motoarena();
        Assert.Equal(LaneModel.SegmentLengthMeters(track.Segments[1], lateral, track.Geometry),
            ExecutedPathTraversal.GeometricDistance(track.Segments[1], track.Geometry, lateral, lateral, 1d));
    }

    [Fact]
    public void EarlyTargetArrivalSplitsDiagonalThenHeldRemainder()
    {
        var step = Resolve(SegmentType.Straight, .8f, 1, 19f, straightLength: 62f);
        var path = Assert.IsType<ExecutedSegmentPath>(step.Diagnostics.Single().ExecutedPath);
        var arrival = path.Nodes.Select((n, i) => (n, i)).First(item => item.n.LateralPosition == 1f);
        Assert.InRange(arrival.i, 1, path.Nodes.Count - 2);
        Assert.All(path.Nodes.Skip(arrival.i), n => Assert.Equal(1f, n.LateralPosition));
        Assert.True(path.DistanceMeters > 62f);
        var held = path.DistanceMeters - arrival.n.DistanceMeters;
        Assert.Equal(62f * (1f - arrival.n.SegmentProgress), held, 4);
        Reconcile(step);
    }

    [Theory]
    [InlineData(3f, 0, -1)] [InlineData(1f, 4, 1)]
    public void MovingTurnUsesLocalPolarGeometryAndCapability(float lateral, int target, int direction)
    {
        var step = Resolve(SegmentType.TurnEntry, lateral, target, 21f);
        var path = Assert.IsType<ExecutedSegmentPath>(step.Diagnostics.Single().ExecutedPath);
        Assert.True(direction * (path.Nodes[^1].RadiusMeters - path.Nodes[0].RadiusMeters) > 0f);
        foreach (var node in path.Nodes)
        {
            Assert.InRange(node.LateralPosition, 0f, 4f);
            Assert.Equal(24f + node.PhysicalOffsetMeters, node.RadiusMeters);
            Assert.Equal(1f / node.RadiusMeters!.Value, node.CurvaturePerMeter);
            Assert.Equal(SegmentPhysics.MaxSafeTurnSpeed(node.LateralPosition, step.Snapshot.Track.Geometry,
                node.Surface, step.Snapshot.Riders[0].Profile.Skills, BikeSetup.Neutral), node.LocalSafeSpeedMetersPerSecond);
        }
        for (var i = 0; i < path.Steps.Count; i++)
        {
            var a = path.Nodes[i]; var b = path.Nodes[i + 1];
            var tangent = ((double)a.RadiusMeters!.Value + b.RadiusMeters!.Value) * .5d
                * step.Snapshot.Track.Geometry.TurnSegmentAngleRadians * (b.SegmentProgress - a.SegmentProgress);
            var dr = (double)b.PhysicalOffsetMeters - a.PhysicalOffsetMeters;
            Assert.InRange(Math.Abs(path.Steps[i].DistanceMeters - Math.Sqrt(tangent * tangent + dr * dr)), 0d, 1e-5d);
        }
        Assert.NotEqual(path.Nodes[0].LocalSafeSpeedMetersPerSecond, path.Nodes[^1].LocalSafeSpeedMetersPerSecond);
        Reconcile(step);
    }

    [Fact]
    public void NonuniformSurfaceIsSampledAlongTheConsumedTrajectory()
    {
        var step = Resolve(SegmentType.TurnEntry, 3f, 0, 18f, gradient: true);
        var path = Assert.IsType<ExecutedSegmentPath>(step.Diagnostics.Single().ExecutedPath);
        Assert.NotEqual(path.Nodes[0].Surface, path.Nodes[^1].Surface);
        Assert.All(path.Steps, s => Assert.Equal(step.Snapshot.TrackState.SampleSurface(0, s.SampledLateralPosition), s.SampledSurface));
        foreach (var s in path.Steps)
            Assert.Equal(LongitudinalDynamics.CalculateTurnExitAvailableDriveForceNewtons(RiderSkills.Balanced,
                BikeSetup.Neutral, s.SampledSurface), s.ReferenceDriveForceNewtons);
        Reconcile(step);
    }

    [Fact]
    public void EntryHoldVersusInwardConsumesDifferentActualGeometry()
    {
        var hold = Resolve(SegmentType.TurnEntry, 3f, 3, 21f);
        var inward = Resolve(SegmentType.TurnEntry, 3f, 0, 21f);
        Assert.Null(hold.Diagnostics.Single().ExecutedPath);
        var path = Assert.IsType<ExecutedSegmentPath>(inward.Diagnostics.Single().ExecutedPath);
        Assert.True(path.Nodes[^1].LateralPosition < 3f);
        Assert.True(path.MinimumRadiusMeters < path.MaximumRadiusMeters);
        Assert.NotEqual(hold.Diagnostics.Single().TravelledMeters, path.DistanceMeters);
        Assert.Equal(path.DistanceMeters, inward.Diagnostics.Single().TravelledMeters);
        Reconcile(inward);
    }

    [Fact]
    public void ExactAudit51ControlConsumesMovingGeometryRatherThanEntryArc()
    {
        var hold = SingleRiderTrajectoryBenchmark.ResolveAudit51(3);
        var inward = SingleRiderTrajectoryBenchmark.ResolveAudit51(0);
        var path = Assert.IsType<ExecutedSegmentPath>(inward.Diagnostics.Single().ExecutedPath);
        Assert.Equal(3f, path.Nodes[0].LateralPosition);
        Assert.InRange(path.Nodes[^1].LateralPosition, 2f, 2.3f);
        Assert.Equal(33f, path.Nodes[0].RadiusMeters);
        Assert.True(path.MinimumRadiusMeters < 33f);
        Assert.NotEqual(hold.Diagnostics.Single().TravelledMeters, path.DistanceMeters);
        Reconcile(inward);
    }

    [Fact]
    public void CrashKeepsHalfRemainingCanonicalAdvanceAndNoWear()
    {
        var step = Resolve(SegmentType.TurnEntry, 1.4f, 2, 50f);
        var change = step.Changes.Single();
        Assert.Equal(RiderRaceStatus.Crashed, change.Status);
        Assert.Equal(.5f, change.Position.SegmentProgress);
        Assert.Equal(0f, change.Speed);
        Assert.False(change.ApplySurfaceWear);
        Reconcile(step);
    }

    [Fact]
    public void MovingWearVisitsActualRegionsAndPreservesEntryBudget()
    {
        var step = Resolve(SegmentType.TurnEntry, 3f, 0, 18f);
        var state = TrackState.CreateDefault(step.Snapshot.Track, new TrackSurfaceState(1f, 0f, .35f));
        var rider = new RiderState(RiderProfile.CreateDefault(1), 3);
        new SimulationEngine(new Target(0)).Commit(step, new[] { rider }, state, new SimLog());
        var total = Enumerable.Range(0, 5).Sum(lane => state.GetSurface(0, lane).Ruts);
        Assert.Equal(.015f * 1.4f, total, 6);
        Assert.True(state.GetSurface(0, 1).Ruts > 0f); // Unvisited by the old entry-3 kernel.
        Assert.True(state.GetSurface(0, 3).Ruts < .015f);
    }

    [Fact]
    public void EveryHistoricalArtifactRetainsItsCanonicalBytesAndProvenance()
    {
        using var manifest = JsonDocument.Parse(File.ReadAllText(Path.Combine(Root, "tests/fixtures/historical-artifact-manifest.json")));
        foreach (var artifact in manifest.RootElement.GetProperty("CanonicalLfSha256").EnumerateObject())
            HistoricalPhysicsSource.AssertArtifactUnchanged(artifact.Name);
    }

    [Fact]
    public void BenchmarkHasAll21ControlsAndIsExactlyReproducible()
    {
        var first = SingleRiderTrajectoryBenchmark.Run(); var repeated = SingleRiderTrajectoryBenchmark.Run();
        Assert.Equal(21, first.Count);
        Assert.Equal(SingleRiderTrajectoryBenchmark.Evidence(first), SingleRiderTrajectoryBenchmark.Evidence(repeated));
        Assert.Equal(File.ReadAllText(Path.Combine(Root, "docs/calibration/single-rider-executed-trajectory.json")),
            SingleRiderTrajectoryBenchmark.Evidence(first));
        Assert.All(first, r => { Assert.True(float.IsFinite(r.ApexLateral)); Assert.True(float.IsFinite(r.CombinedTimeSeconds)); });
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void ReversedCollectionsAndIncidentOffSeedsPreserveTheSamePath(bool reverse)
    {
        var track = Motoarena();
        ResolvedSimulationStep Run(int seed, bool reversed)
        {
            var riders = Enumerable.Range(1, 4).Select(id => new RiderState(RiderProfile.CreateDefault(id), 3)
                { LateralPosition = 3f, Speed = 21f, ElapsedTimeSeconds = id * 100f }).ToArray();
            foreach (var r in riders) r.RestorePosition(RiderPosition.Create(1, 1, 0f, track.Segments.Count));
            var engine = new SimulationEngine(new Target(0));
            var snapshot = engine.CaptureSnapshot(track, TrackState.CreateDefault(track, new TrackSurfaceState(1f, 0f, .35f)),
                reversed ? riders.Reverse().ToArray() : riders, new SimulationStepContext(53, 0, 0, 1, seed, 1));
            return engine.Resolve(snapshot, engine.Decide(snapshot), new HeatSimulationOptions { Laps = 1, IncidentFrequency = 0f });
        }
        var control = Run(0, false); var actual = Run(31, reverse);
        Assert.Equal(control.Changes, actual.Changes);
        Assert.Equal(control.Diagnostics, actual.Diagnostics);
    }

    private static void Reconcile(ResolvedSimulationStep step)
    {
        var d = step.Diagnostics.Single(); var path = Assert.IsType<ExecutedSegmentPath>(d.ExecutedPath);
        var change = step.Changes.Single(); var entry = step.Snapshot.Riders.Single();
        Assert.Equal(path.DistanceMeters, d.TravelledMeters);
        Assert.Equal(path.DistanceMeters, change.Position.DistanceMeters - entry.DistanceMeters, 4);
        Assert.Equal(path.Steps.Sum(s => s.DistanceMeters), d.TravelledMeters, 4);
        Assert.Equal(path.Steps.Sum(s => s.TimeSeconds) + path.ReactionTimeSeconds, d.TravelTimeSeconds, 5);
        Assert.Equal(path.Nodes[^1].LateralPosition, change.LateralPosition);
        Assert.Equal(path.TravelTimeSeconds, change.ElapsedTimeSeconds - entry.ElapsedTimeSeconds, 5);
        Assert.All(path.Steps, s => { Assert.InRange(s.DistanceMeters, 0f, 1f); Assert.True(float.IsFinite(s.TimeSeconds)); });
        if (d.ContinuousCornerProfile is { } profile)
            Assert.Equal(profile.CorrectionDistanceMeters + profile.CarryDistanceMeters + profile.DriveDistanceMeters, d.TravelledMeters, 4);
    }

    private static ResolvedSimulationStep Resolve(SegmentType type, float lateral, int target, float speed,
        float straightLength = 60f, bool gradient = false)
    {
        var track = new Track(new[] { new TrackSegment(0, type, type == SegmentType.Straight ? straightLength : null) });
        var state = new TrackState(1, 5, (_, lane) => gradient ? new TrackSurfaceState(.55f + .08f * lane, 0f, .35f) : new TrackSurfaceState(1f, 0f, .35f));
        var rider = new RiderState(RiderProfile.CreateDefault(1), (int)MathF.Round(lateral)) { LateralPosition = lateral, Speed = speed };
        var engine = new SimulationEngine(new Target(target));
        var snapshot = engine.CaptureSnapshot(track, state, new[] { rider }, new SimulationStepContext(53, 0, 0, 0, 0, 1));
        return engine.Resolve(snapshot, engine.Decide(snapshot), new HeatSimulationOptions { Laps = 1, IncidentFrequency = 0f });
    }
    private static Track Motoarena() => MatchedVenueProfiles.CreateMotoarenaStandingStartTrack();
    private static string Root => Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../"));
    private sealed class Target(int lane) : IRiderDecisionModel
    {
        public RiderDecision Decide(TrackSegment segment, RiderState rider) => new(lane, 0f);
    }
}
