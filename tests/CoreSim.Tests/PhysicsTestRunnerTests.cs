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
            segmentPlans: new[] { new PhysicsTestSegmentPlan(targetLane: 0, note: "forces consequence") });

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
            name: "brake",
            track: track,
            trackState: trackState,
            initialLane: 0,
            initialSpeed: SegmentPhysics.MaxSafeTurnSpeed(0) * initialMultiplier,
            segmentPlans: new[]
            {
                new PhysicsTestSegmentPlan(targetLane: 0, EntrySpeed: SegmentPhysics.MaxSafeTurnSpeed(0), note: "hard brake"),
                new PhysicsTestSegmentPlan(targetLane: 0)
            });

        var mixedScenario = PhysicsTestScenario.FromSafeSpeedMultiplier(
            name: "mixed",
            track: track,
            trackState: trackState,
            initialLane: 0,
            safeSpeedMultiplier: initialMultiplier,
            segmentPlans: new[]
            {
                new PhysicsTestSegmentPlan(targetLane: 0, note: "allow run wide"),
                new PhysicsTestSegmentPlan(targetLane: 1, note: "hold wider exit")
            });

        var runner = new PhysicsTestRunner();
        var brakeResult = runner.Run(brakeScenario);
        var mixedResult = runner.Run(mixedScenario);

        Assert.True(mixedResult.FinalSpeed > brakeResult.FinalSpeed);
        Assert.Equal(0, brakeResult.FinalLane);
        Assert.Equal(1, mixedResult.FinalLane);
    }
}
