using CoreSim;
using CoreSim.Analysis;
using CoreSim.Setup;
using Xunit;

namespace CoreSim.Tests;

[Trait("Shard", "core")]
public sealed class PhysicalTrackWidthIntegrationTests
{
    [Fact]
    public void StandingExampleInnerRiderKeepsPreviousPhysicalRadius()
    {
        var compatibility = Track.CreateExample().Geometry;
        var standing = Track.CreateStandingStartExample().Geometry;

        Assert.Equal(24f, LaneModel.TurnArcRadiusMeters(0f, compatibility));
        Assert.Equal(
            LaneModel.TurnArcRadiusMeters(0f, compatibility),
            LaneModel.TurnArcRadiusMeters(0f, standing));
        Assert.Equal(24f, FirstMiddleSample(0).TravelledMeters / standing.TurnSegmentAngleRadians, 5);
    }

    [Fact]
    public void StandingExampleOuterRiderUsesNewPhysicalRadius()
    {
        var sample = FirstMiddleSample(4);
        Assert.Equal(SegmentOutcome.Ok, sample.Outcome);
        Assert.Equal(36f, sample.TravelledMeters / Track.CreateStandingStartExample().Geometry.TurnSegmentAngleRadians, 5);
    }

    [Fact]
    public void StandingExampleOuterRiderTravelsLongerTurnDistance()
    {
        var geometry = Track.CreateStandingStartExample().Geometry;
        var inner = LaneModel.TurnArcLengthMeters(0f, geometry);
        var outer = LaneModel.TurnArcLengthMeters(4f, geometry);

        Assert.Equal(1.5f, outer / inner, 6);
        Assert.True(outer > inner);
        Assert.True(FirstMiddleSample(4).TravelledMeters > FirstMiddleSample(0).TravelledMeters);
    }

    [Fact]
    public void StandingExampleOuterRiderGetsGeometryBasedSafeSpeedChangeWithoutSpeedConstantChange()
    {
        var compatibility = Track.CreateExample().Geometry;
        var standing = Track.CreateStandingStartExample().Geometry;
        var oldOuter = SegmentPhysics.MaxSafeTurnSpeed(4f, compatibility);
        var newOuter = SegmentPhysics.MaxSafeTurnSpeed(4f, standing);

        Assert.Equal(16f, SegmentPhysics.ReferenceTurnSpeedMetersPerSecond);
        Assert.Equal(24f, SegmentPhysics.ReferenceTurnRadiusMeters);
        Assert.Equal(
            SegmentPhysics.ReferenceTurnSpeedMetersPerSecond * MathF.Sqrt(36f / 24f),
            newOuter,
            6);
        Assert.True(newOuter > oldOuter);
        // Identical incoming speed is constrained on the old radius but fits
        // the wider standing-example turn through the existing engine.
        var track = Track.CreateStandingStartExample();
        var oldTrack = new Track(track.Segments, compatibility);
        var oldRider = StandingStartFixture.Rider(lane: 4);
        var newRider = StandingStartFixture.Rider(lane: 4);
        oldRider.Speed = newRider.Speed = CornerTestSupport.Envelope(oldTrack, oldRider, StandingStartFixture.Perfect, index: 2).SpeedMetersPerSecond(1f / 3f) * 1.025f;
        oldRider.RestorePosition(CoreSim.Race.RiderPosition.Create(1, 2, 0f, track.Segments.Count));
        newRider.RestorePosition(CoreSim.Race.RiderPosition.Create(1, 2, 0f, track.Segments.Count));
        var oldChange = Assert.Single(StandingStartFixture.Resolve(
            oldTrack, new[] { oldRider }, segment: 2).Changes);
        var newChange = Assert.Single(StandingStartFixture.Resolve(
            track, new[] { newRider }, segment: 2).Changes);
        Assert.Equal(SegmentOutcome.Brake, oldChange.Outcome);
        Assert.Equal(SegmentOutcome.Ok, newChange.Outcome);
        Assert.Equal(oldRider.Speed, oldChange.PhysicsSpeed, 5);
        Assert.True(oldChange.Speed < oldChange.PhysicsSpeed);
        Assert.Equal(newRider.Speed, newChange.PhysicsSpeed, 5);
    }

    [Fact]
    public void SameSkillsAndSetupUseSamePhysicsConstantsAcrossWidths()
    {
        var compatibility = Track.CreateExample().Geometry;
        var standing = Track.CreateStandingStartExample().Geometry;
        var surface = new TrackSurfaceState(1f, 0f, 0.35f);
        var setup = BikeSetup.Neutral;

        var compatibilitySpeed = SegmentPhysics.MaxSafeTurnSpeed(2f, compatibility, surface, RiderSkills.Balanced, setup);
        var standingSpeed = SegmentPhysics.MaxSafeTurnSpeed(2f, standing, surface, RiderSkills.Balanced, setup);

        Assert.Equal(MathF.Sqrt(30f / 26f), standingSpeed / compatibilitySpeed, 6);
        Assert.Equal(1.10f, SegmentPhysics.BrakeSpeedFactor);
        Assert.Equal(1.30f, SegmentPhysics.RunWideSpeedFactor);
    }

    [Fact]
    public void PhysicalWidthChangeDoesNotCreateRandomness()
    {
        var scenario = new CalibrationSkillScenario("physical-width-determinism", RiderSkills.Balanced);
        Assert.Equal(
            CalibrationSkillSweep.RunScenario(scenario).Riders,
            CalibrationSkillSweep.RunScenario(scenario).Riders);
    }

    [Fact]
    public void RiderOrderIndependenceSurvivesPhysicalWidthGeometry()
    {
        var scenario = new CalibrationSkillScenario("physical-width-order", RiderSkills.Balanced);
        var forward = CalibrationSkillSweep.RunScenario(scenario, new[] { 1, 2, 3, 4 });
        var reverse = CalibrationSkillSweep.RunScenario(scenario, new[] { 4, 3, 2, 1 });

        Assert.Equal(forward.Riders, reverse.Riders);
    }

    [Fact]
    public void AllTwentyFourRiderPermutationsRemainEquivalent()
    {
        var scenario = new CalibrationSkillScenario("physical-width-permutations", RiderSkills.Balanced);
        var expected = CalibrationSkillSweep.RunScenario(scenario);
        var permutations = Permutations(new[] { 1, 2, 3, 4 }).ToArray();

        Assert.Equal(24, permutations.Length);
        foreach (var permutation in permutations)
            Assert.Equal(expected.Riders, CalibrationSkillSweep.RunScenario(scenario, permutation).Riders);
    }

    [Fact]
    public void MeasurementLineLapLengthIsExactlySumOfPhysicalSegments()
    {
        var track = Track.CreateStandingStartExample();
        var physicalSegments = track.Segments
            .Select(segment => LaneModel.SegmentLengthMeters(segment, 0f, track.Geometry))
            .ToArray();

        Assert.Equal(130f + 2f * MathF.PI * 24f, physicalSegments.Sum(), 3);
        Assert.Equal(35f, physicalSegments[0]);
        Assert.Equal(35f, physicalSegments[^1]);
    }

    private static CalibrationStepSample FirstMiddleSample(int lane)
    {
        var track = Track.CreateStandingStartExample();
        var trace = CalibrationRunner.RunHeat(track,
            TrackState.CreateDefault(track, StandingStartFixture.Perfect),
            new List<RiderState> { StandingStartFixture.Rider(lane: lane) },
            new StandingStartFixture.HoldLane(), StandingStartFixture.Options());
        return trace.StepSamples.First(sample => sample.SegmentType == SegmentType.TurnMiddle);
    }

    private static IEnumerable<int[]> Permutations(int[] values)
    {
        if (values.Length == 0)
        {
            yield return Array.Empty<int>();
            yield break;
        }

        for (var index = 0; index < values.Length; index++)
        {
            var head = values[index];
            var tail = values.Where((_, candidate) => candidate != index).ToArray();
            foreach (var permutation in Permutations(tail))
                yield return new[] { head }.Concat(permutation).ToArray();
        }
    }
}
