using CoreSim;
using CoreSim.Analysis;
using CoreSim.Setup;
using Xunit;
using static CoreSim.LongitudinalDynamics;
using static CoreSim.Tests.StandingStartFixture;

namespace CoreSim.Tests;

public sealed class SignedLongitudinalTraversalTests
{
    private static float Force(bool exit = false) => exit
        ? CalculateTurnExitAvailableDriveForceNewtons(RiderSkills.Balanced, BikeSetup.Neutral, Perfect)
        : CalculateStraightAvailableDriveForceNewtons(RiderSkills.Balanced, BikeSetup.Neutral, Perfect);
    private static float Equilibrium(bool exit = false) => CalculateFullDriveEquilibriumSpeedMetersPerSecond(Force(exit), BikeSetup.Neutral);
    private static StraightSpeedProfile Straight(float initial = 10f, float distance = 60f, float? target = null)
        => CalculateForceBasedStraightSpeedProfile(initial, RiderSkills.Balanced, BikeSetup.Neutral, Perfect, 2.6f, distance, target);
    private static TurnExitDriveProfile Exit(float initial, float distance = 60f)
        => CalculateForceBasedTurnExitDriveProfile(initial, RiderSkills.Balanced, BikeSetup.Neutral, Perfect, distance);
    private static StandingStartLaunchProfile Start(float distance = 35f, float? target = null)
        => CalculateStandingStartLaunchProfile(RiderSkills.Balanced, BikeSetup.Neutral, Perfect, distance, target);

    [Fact]
    public void LongStraightConvergesTowardNaturalEquilibrium()
    {
        var equilibrium = Equilibrium();
        var near = Straight(distance: 1000f);
        var longRun = Straight(distance: 10000f);
        Assert.True(MathF.Abs(longRun.ExitSpeedMetersPerSecond - equilibrium) < MathF.Abs(near.ExitSpeedMetersPerSecond - equilibrium));
        Assert.InRange(MathF.Abs(longRun.ExitSpeedMetersPerSecond - equilibrium), 0f, 0.001f);
    }

    [Fact]
    public void LongStraightNoLongerStopsAtFormerArtificialCeiling()
    {
        var formerCeiling = 23f; // Historical neutral Speed=50 value; not a production parameter.
        Assert.True(Straight(distance: 2000f).ExitSpeedMetersPerSecond > formerCeiling);
        Assert.InRange(MathF.Abs(Straight(distance: 10000f).ExitSpeedMetersPerSecond - Equilibrium()), 0f, 0.001f);
    }

    [Fact]
    public void InitialOverspeedAboveEquilibriumNaturallyDecelerates()
    {
        var initial = Equilibrium() + 5f;
        var p = Straight(initial);
        Assert.True(p.ExitSpeedMetersPerSecond < initial && p.ExitSpeedMetersPerSecond > Equilibrium());
        Assert.Equal(60f, p.DecelerationDistanceMeters);
    }

    [Fact]
    public void InitialOverspeedIsNotTeleportedToEquilibrium()
    {
        var initial = Equilibrium() + 5f;
        var firstMetre = Straight(initial, 1f);
        Assert.True(firstMetre.ExitSpeedMetersPerSecond > Equilibrium() + 4f);
        Assert.True(Straight(initial, 60f).ExitSpeedMetersPerSecond < firstMetre.ExitSpeedMetersPerSecond);
    }

    [Fact]
    public void StraightWithoutTargetUsesForceEquilibriumNotArtificialCap()
    {
        var p = Straight(Equilibrium() + 5f, 10000f);
        Assert.InRange(MathF.Abs(p.ExitSpeedMetersPerSecond - Equilibrium()), 0f, 0.001f);
        Assert.Equal(Equilibrium(), p.FullDriveEquilibriumSpeedMetersPerSecond);
    }

    [Fact]
    public void StraightWithNextTurnStillPreparesForRecoverableApproach()
    {
        var rider = Rider();
        rider.Speed = 30f;
        var step = Resolve(lap: 1, riders: new[] { rider });
        var profile = Assert.IsType<StraightSpeedProfile>(step.Diagnostics[0].StraightProfile);
        var target = FirstTurnApproachTarget(step);
        Assert.Equal(Straight(rider.Speed, step.Diagnostics[0].TravelledMeters, target), profile);
        Assert.True(profile.ExitSpeedMetersPerSecond > target); // Residual: 60 m is insufficient from 30 m/s.
        Assert.True(profile.DecelerationDistanceMeters > 0f);
        Assert.Null(step.Diagnostics[0].StandingStartLaunchProfile);
    }

    [Fact]
    public void PreparationBoundaryNeverRaisesNaturallyDeceleratingFullDriveCandidate()
    {
        // At this overspeed resistance exceeds even the available 2.6 m/s²
        // preparation capability. A lower envelope must not lift the candidate.
        const float initial = 100f;
        var full = Straight(initial, 1f);
        Assert.True((initial * initial - full.ExitSpeedMetersPerSecond * full.ExitSpeedMetersPerSecond) / 2f > 2.6f);
        Assert.Equal(full, Straight(initial, 1f, 0f));
        Assert.Equal(full, Straight(initial, 1f, initial));
    }

    [Fact]
    public void PreparationNeverExceedsAvailableCornerEntryDeceleration()
    {
        const float initial = 25f;
        var p = Straight(initial, 1f, 1f);
        var reachable = MathF.Sqrt(initial * initial - 2f * 2.6f);
        Assert.Equal(reachable, p.ExitSpeedMetersPerSecond, 5);
        Assert.True(p.ExitSpeedMetersPerSecond > 1f); // Unreachable target stays residual overspeed.
    }

    [Fact]
    public void StraightPhaseDistancesStillSumToTotalDistance()
    {
        foreach (var initial in new[] { 0f, 10f, Equilibrium(), Equilibrium() + 5f })
        foreach (var distance in new[] { 0f, 0.125f, 60.125f, 10000f })
        foreach (var target in new float?[] { null, 17f })
        {
            var p = Straight(initial, distance, target);
            Assert.Equal(distance, p.AccelerationDistanceMeters + p.CruiseDistanceMeters + p.DecelerationDistanceMeters);
        }
    }

    [Fact]
    public void StraightTravelTimeStillEqualsSumOfDistanceStepTimes()
    {
        foreach (var initial in new[] { 10f, Equilibrium() + 5f })
        foreach (var target in new float?[] { null, 17f })
        {
            var nodes = SignedForceReference.Integrate(initial, 60.125d, Force(), step: 1d, target: target);
            var profile = Straight(initial, 60.125f, target);
            Assert.InRange(Math.Abs(profile.TravelTimeSeconds - nodes.Sum(n => n.Time)), 0d, 0.00002d);
            Assert.InRange(Math.Abs(profile.ExitSpeedMetersPerSecond - nodes[^1].End), 0d, 0.00003d);
        }
    }

    [Fact]
    public void StraightSignedTraversalIsDeterministic()
    {
        var expected = Straight(Equilibrium() + 5f, 60.125f, 17f);
        for (var i = 0; i < 30; i++) Assert.Equal(expected, Straight(Equilibrium() + 5f, 60.125f, 17f));
    }

    [Fact]
    public void StraightSignedTraversalIsIndependentOfRiderOrder()
    {
        var track = new Track(new[] { new TrackSegment(0, SegmentType.Straight, 60f) });
        var riders = Riders();
        foreach (var rider in riders) rider.Speed = Equilibrium() + rider.RiderId;
        var forward = Resolve(track, riders);
        var reverse = Resolve(track, riders.AsEnumerable().Reverse().ToArray());
        Assert.Equal(forward.Diagnostics, reverse.Diagnostics);
        Assert.Equal(forward.Changes, reverse.Changes);
        Assert.All(forward.Diagnostics, d => Assert.True(d.StraightProfile!.Value.DecelerationDistanceMeters > 0f));
    }

    [Fact]
    public void TurnExitBelowEquilibriumAccelerates() => Assert.True(Exit(Equilibrium(true) - 5f).ExitSpeedMetersPerSecond > Equilibrium(true) - 5f);

    [Fact]
    public void TurnExitAboveEquilibriumNaturallyDecelerates()
    {
        var p = Exit(Equilibrium(true) + 5f);
        Assert.True(p.ExitSpeedMetersPerSecond < Equilibrium(true) + 5f && p.ExitSpeedMetersPerSecond > Equilibrium(true));
        Assert.True(p.EntryNetAccelerationMetersPerSecondSquared < 0f);
    }

    [Fact]
    public void TurnExitAtEquilibriumApproximatelyCruises()
    {
        var p = Exit(Equilibrium(true));
        Assert.InRange(MathF.Abs(p.ExitSpeedMetersPerSecond - Equilibrium(true)), 0f, 0.00001f);
        Assert.Equal(60f, p.CruiseDistanceMeters);
    }

    [Fact]
    public void TurnExitPhaseDistancesIncludeNaturalDeceleration()
    {
        var p = Exit(Equilibrium(true) + 5f);
        Assert.Equal(60f, p.DecelerationDistanceMeters);
        Assert.Equal(0f, p.AccelerationDistanceMeters);
        Assert.Equal(0f, p.CruiseDistanceMeters);
    }

    [Fact]
    public void TurnExitPhaseDistancesSumToTotalDistance()
    {
        foreach (var initial in new[] { 10f, Equilibrium(true), Equilibrium(true) + 5f })
        foreach (var distance in new[] { 0f, 0.125f, 60.125f, 10000f })
        {
            var p = Exit(initial, distance);
            Assert.Equal(distance, p.AccelerationDistanceMeters + p.CruiseDistanceMeters + p.DecelerationDistanceMeters);
        }
    }

    [Fact]
    public void TurnExitPeakPreservesInitialOverspeedWhenNaturallyDecelerating()
        => Assert.Equal(Equilibrium(true) + 5f, Exit(Equilibrium(true) + 5f).PeakSpeedMetersPerSecond);

    [Fact]
    public void TurnExitNoLongerUsesArtificialCeiling()
    {
        var p = Exit(16f, 10000f);
        Assert.True(p.ExitSpeedMetersPerSecond > 23f);
        Assert.InRange(MathF.Abs(p.ExitSpeedMetersPerSecond - Equilibrium(true)), 0f, 0.001f);
    }

    [Fact]
    public void TurnExitReferenceAccelerationAtSixteenUsesCalibratedNeutralValue()
        => Assert.Equal(2f, Exit(16f).EntryNetAccelerationMetersPerSecondSquared, 6);

    [Fact]
    public void StandingStartStillBeginsAtZero()
    {
        var step = Resolve();
        Assert.Equal(0f, step.Changes[0].PhysicsSpeed);
        Assert.Equal(0f, Start(0f).ExitSpeedMetersPerSecond);
        Assert.True(Start(0.01f).ExitSpeedMetersPerSecond > 0f);
    }

    [Fact]
    public void StandingStartReactionFormulaIsUnchanged()
    {
        foreach (var (skill, expected) in new[] { (0f, 0.28f), (50f, 0.24f), (100f, 0.20f) })
            Assert.Equal(expected, CalculateStandingStartReactionTimeSeconds(new RiderSkills(skill, 50f, 50f, 50f, 50f, 50f)), 6);
    }

    [Fact]
    public void StandingStartReferenceLaunchForceIsUnchanged()
    {
        foreach (var start in new[] { 0f, 50f, 100f })
        foreach (var gearing in new[] { 0f, 0.5f, 1f })
        foreach (var surface in new[] { Perfect, new TrackSurfaceState(0.3f, 0f, 0.35f) })
        {
            var skills = new RiderSkills(start, 50f, 50f, 50f, 50f, 50f);
            var setup = new BikeSetup(gearing, 0.5f);
            var expected = 142f * (9f + 2f * start / 100f) * (1.10f + (0.90f - 1.10f) * gearing)
                * (0.75f + 0.25f * surface.EffectiveGrip) + 40f;
            Assert.InRange(MathF.Abs(expected - CalculateStandingStartAvailableDriveForceNewtons(skills, setup, surface)), 0f, 0.001f);
        }
    }

    [Fact]
    public void StandingStartStillPreparesForImmediateTurnEntry()
    {
        var step = Resolve();
        var p = Launch(step);
        Assert.Equal(Start(35f, FirstTurnApproachTarget(step)), p);
        Assert.True(p.PreparationDistanceMeters > 0f);
    }

    [Fact]
    public void StandingStartTimeTo70StillUsesActualCorrectedSteps()
    {
        var p = Start(35f, 18f);
        var nodes = SignedForceReference.Integrate(0d, 35d, 1460d, step: 1d, target: 18d);
        var threshold = StandingStartTelemetry70KphMetersPerSecond;
        var crossing = nodes.First(n => n.Start < threshold && n.End >= threshold);
        var expected = p.ReactionTimeSeconds + crossing.StartTime + (threshold - crossing.Start) / crossing.Acceleration;
        Assert.InRange(Math.Abs(p.TimeTo70KphSeconds!.Value - expected), 0d, 0.00002d);
    }

    [Fact]
    public void StandingStartSpeedAtTwoSecondsStillUsesActualCorrectedSteps()
    {
        foreach (var (distance, target) in new (float, float?)[] { (35f, null), (20f, 12f) })
        {
            var p = Start(distance, target);
            var nodes = SignedForceReference.Integrate(0d, distance, 1460d, step: 1d, target: target);
            var movementObservation = 2d - p.ReactionTimeSeconds;
            var node = nodes.Single(n => n.StartTime <= movementObservation && movementObservation < n.StartTime + n.Time);
            if (target is not null) Assert.True(node.Acceleration < 0d);
            var expected = node.Start + node.Acceleration * (movementObservation - node.StartTime);
            Assert.InRange(Math.Abs(p.SpeedAtTwoSecondsMetersPerSecond!.Value - expected), 0d, 0.00002d);
        }
    }

    [Fact]
    public void StandingStartDistancePhasesStillSumExactly()
    {
        foreach (var distance in new[] { 0f, 0.125f, 35f, 35.125f, 1000f })
        {
            var p = Start(distance, 17f);
            Assert.Equal(distance, p.AccelerationDistanceMeters + p.CruiseDistanceMeters + p.PreparationDistanceMeters);
            Assert.Equal(p.ReactionTimeSeconds + p.MovementTimeSeconds, p.TotalTimeSeconds, 5);
        }
    }

    [Fact]
    public void StandingStartLaunchIsDeterministic() => Assert.Equal(Run().Trace!.StepSamples, Run().Trace!.StepSamples);

    [Fact]
    public void StandingStartLaunchRemainsRiderOrderIndependent()
    {
        var expected = Run().Trace!;
        var permutations = Permutations(new[] { 1, 2, 3, 4 }).ToArray();
        Assert.Equal(24, permutations.Length);
        foreach (var order in permutations)
        {
            var actual = Run(order: order).Trace!;
            Assert.Equal(expected.StepSamples, actual.StepSamples);
            Assert.Equal(expected.RiderSummaries, actual.RiderSummaries);
        }
    }

    [Fact]
    public void EquilibriumDiagnosticsComeOnlyFromActualProductionProfiles()
    {
        var run = Run();
        foreach (var step in run.Steps)
        foreach (var d in step.Diagnostics)
        {
            var expected = d.StandingStartLaunchProfile?.FullDriveEquilibriumSpeedMetersPerSecond
                ?? d.StraightProfile?.FullDriveEquilibriumSpeedMetersPerSecond
                ?? d.ContinuousCornerProfile?.FullDriveEquilibriumSpeedMetersPerSecond;
            Assert.Equal(expected, d.FullDriveEquilibriumSpeedMetersPerSecond);
            var sample = run.Trace!.StepSamples.Single(s => s.StepNumber == step.Snapshot.Step.StepNumber && s.RiderId == d.RiderId);
            Assert.Equal(expected, sample.FullDriveEquilibriumSpeedMetersPerSecond);
            if (sample.SegmentType is SegmentType.TurnEntry or SegmentType.TurnMiddle) Assert.NotNull(sample.ContinuousCornerProfile);
        }
    }

    [Fact]
    public void ArtificialCeilingApiAndDiagnosticSchemaAreRemoved()
    {
        var methods = typeof(LongitudinalDynamics).GetMethods().Select(m => m.Name).ToArray();
        Assert.DoesNotContain("CalculateAttainableTopSpeedMetersPerSecond", methods);
        Assert.DoesNotContain("AccelerateOverDistanceWithSpeedCeiling", methods);
        Assert.DoesNotContain("CalculateNetPositiveDriveAccelerationMetersPerSecondSquared", methods);
        var fields = typeof(LongitudinalDynamics).GetFields().Select(f => f.Name).ToArray();
        foreach (var name in new[] { "MinAttainableTopSpeedMetersPerSecond", "MaxAttainableTopSpeedMetersPerSecond", "LowGearingTopSpeedMultiplier", "HighGearingTopSpeedMultiplier" })
            Assert.DoesNotContain(name, fields);
        Assert.Null(typeof(RiderStepDiagnostics).GetProperty("AttainableTopSpeedMetersPerSecond"));
        Assert.Null(typeof(CalibrationStepSample).GetProperty("AttainableTopSpeedMetersPerSecond"));
        var header = CalibrationCsvExporter.ExportSteps(Run().Trace!).Split('\n')[0].Split(',');
        Assert.DoesNotContain("AttainableTopSpeedMetersPerSecond", header);
        Assert.Single(header.Where(h => h == "FullDriveEquilibriumSpeedMetersPerSecond"));
        Assert.Single(header.Where(h => h == "TurnExitDecelerationDistanceMeters"));
    }
}
