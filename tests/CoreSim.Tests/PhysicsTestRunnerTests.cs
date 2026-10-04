using CoreSim;
using CoreSim.Debug;
using Xunit;

namespace CoreSim.Tests;

[Trait("Shard", "core")]
public sealed class PhysicsTestRunnerTests
{
    [Fact]
    public void TooFastTightLine_ForcesResolution()
    {
        var track = new Track(new[] { new TrackSegment(0, SegmentType.TurnMiddle) });
        var surface = new TrackSurfaceState(0.9f, 0.1f, 0.4f);
        var trackState = TrackState.CreateDefault(track, surface);

        var scenario = PhysicsTestScenario.FromSafeSpeedMultiplier(
            name: "tight-too-fast",
            track: track,
            trackState: trackState,
            initialLane: 0,
            safeSpeedMultiplier: 1.2f,
            segmentPlans: new[] { new PhysicsTestSegmentPlan(TargetLane: 0, Note: "forces consequence") });

        var result = new PhysicsTestRunner().Run(scenario);

        var segment = Assert.Single(result.Segments);
        var logLine = segment.ToString();
        var maxSafeSpeed = SegmentPhysics.MaxSafeTurnSpeed(LaneModel.MinLane, track.Geometry);
        Assert.Contains("outcome=RunWide", logLine);
        Assert.Equal(1, result.FinalLane);
        Assert.True(segment.SpeedOut < segment.SpeedIn);
        Assert.InRange(segment.SpeedOut, maxSafeSpeed, segment.SpeedIn);
        Assert.True(segment.SpeedOut > 0f);
        Assert.True(float.IsFinite(segment.SpeedOut));
    }

    [Fact]
    public void MixedTrajectoryKeepsMoreSpeedThanHardBrake()
    {
        var track = new Track(new[]
        {
            new TrackSegment(0, SegmentType.TurnEntry),
            new TrackSegment(1, SegmentType.TurnMiddle)
        });
        var trackState = TrackState.CreateDefault(track);
        var initialMultiplier = 1.108f; // just above brake threshold on lane 0

        var brakeScenario = new PhysicsTestScenario(
            Name: "brake",
            Track: track,
            TrackState: trackState,
            InitialLane: 0,
            InitialSpeed: SegmentPhysics.MaxSafeTurnSpeed(0, track.Geometry) * initialMultiplier,
            SegmentPlans: new[]
            {
                new PhysicsTestSegmentPlan(
                    TargetLane: 0,
                    EntrySpeed: SegmentPhysics.MaxSafeTurnSpeed(0, track.Geometry),
                    Note: "hard brake"),
                new PhysicsTestSegmentPlan(TargetLane: 0)
            });

        var mixedScenario = PhysicsTestScenario.FromSafeSpeedMultiplier(
            name: "mixed",
            track: track,
            trackState: trackState,
            initialLane: 0,
            safeSpeedMultiplier: initialMultiplier,
            segmentPlans: new[]
            {
                new PhysicsTestSegmentPlan(TargetLane: 0, Note: "allow run wide"),
                new PhysicsTestSegmentPlan(TargetLane: 1, Note: "hold wider exit")
            });

        var runner = new PhysicsTestRunner();
        var brakeResult = runner.Run(brakeScenario);
        var mixedResult = runner.Run(mixedScenario);

        Assert.True(mixedResult.FinalSpeed > brakeResult.FinalSpeed);
        Assert.Equal(0, brakeResult.FinalLane);
        Assert.Equal(1, mixedResult.FinalLane);
    }

    [Fact]
    public void SafeSpeedMultiplierUsesScenarioGeometry()
    {
        var track = new Track(
            new[] { new TrackSegment(0, SegmentType.TurnMiddle) },
            new TrackGeometry(60f, 54f, 1f, MathF.PI / 3f));
        const float multiplier = 1.15f;

        var scenario = PhysicsTestScenario.FromSafeSpeedMultiplier(
            name: "broad-turn",
            track: track,
            trackState: TrackState.CreateDefault(track),
            initialLane: LaneModel.MinLane,
            safeSpeedMultiplier: multiplier,
            segmentPlans: new[] { new PhysicsTestSegmentPlan(LaneModel.MinLane) });

        Assert.Equal(
            SegmentPhysics.MaxSafeTurnSpeed(LaneModel.MinLane, track.Geometry) * multiplier,
            scenario.InitialSpeed,
            3);
        Assert.NotEqual(
            SegmentPhysics.MaxSafeTurnSpeed(LaneModel.MinLane) * multiplier,
            scenario.InitialSpeed);
    }

    [Fact]
    public void RunnerUsesScenarioGeometry()
    {
        const float entrySpeedMetersPerSecond = 18f;
        var segment = new TrackSegment(0, SegmentType.TurnMiddle);
        var tightTrack = new Track(
            new[] { segment },
            new TrackGeometry(60f, 24f, 1f, MathF.PI / 3f));
        var broadTrack = new Track(
            new[] { segment },
            new TrackGeometry(60f, 54f, 1f, MathF.PI / 3f));
        var plan = new[] { new PhysicsTestSegmentPlan(LaneModel.MinLane) };
        var runner = new PhysicsTestRunner();

        var tightResult = runner.Run(new PhysicsTestScenario(
            "tight",
            tightTrack,
            TrackState.CreateDefault(tightTrack),
            LaneModel.MinLane,
            entrySpeedMetersPerSecond,
            plan));
        var broadResult = runner.Run(new PhysicsTestScenario(
            "broad",
            broadTrack,
            TrackState.CreateDefault(broadTrack),
            LaneModel.MinLane,
            entrySpeedMetersPerSecond,
            plan));

        Assert.Equal(SegmentOutcome.RunWide, Assert.Single(tightResult.Segments).Outcome);
        Assert.Equal(SegmentOutcome.Ok, Assert.Single(broadResult.Segments).Outcome);
    }
}
