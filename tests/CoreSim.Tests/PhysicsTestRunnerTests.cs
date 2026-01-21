using CoreSim;
using CoreSim.Debug;
using Xunit;

namespace CoreSim.Tests;

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

        var logLine = Assert.Single(result.Segments).ToString();
        Assert.Contains("outcome=RunWide", logLine);
        Assert.Equal(1, result.FinalLane);
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
            InitialSpeed: SegmentPhysics.MaxSafeTurnSpeed(0) * initialMultiplier,
            SegmentPlans: new[]
            {
                new PhysicsTestSegmentPlan(TargetLane: 0, EntrySpeed: SegmentPhysics.MaxSafeTurnSpeed(0), Note: "hard brake"),
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
}
