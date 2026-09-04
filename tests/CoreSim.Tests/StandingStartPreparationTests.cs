using CoreSim;
using CoreSim.Analysis;
using CoreSim.Setup;
using Xunit;
using static CoreSim.Tests.StandingStartFixture;

namespace CoreSim.Tests;

public sealed class StandingStartPreparationTests
{
    [Fact]
    public void StandingStartPreparesForImmediateRecoverableTurnEntryTarget()
    {
        var step = Resolve();
        var target = FirstTurnApproachTarget(step);
        var launch = Launch(step);
        Assert.True(launch.PreparationDistanceMeters > 0f);
        Assert.True(launch.PeakSpeedMetersPerSecond > launch.ExitSpeedMetersPerSecond);
        Assert.Equal(target, launch.ExitSpeedMetersPerSecond, 4);
        Assert.Equal(launch.ExitSpeedMetersPerSecond, step.Changes[0].Speed);
    }

    [Fact]
    public void StandingStartPreparationPhasesCoverOnlyActualDistance()
    {
        foreach (var distance in new[] { 0f, 0.25f, 20f, 35f, 35.125f, 100.25f })
        {
            var p = Profile(distance, 12f);
            Assert.Equal(distance, p.AccelerationDistanceMeters + p.CruiseDistanceMeters + p.PreparationDistanceMeters);
            Assert.True(p.AccelerationDistanceMeters >= 0f && p.CruiseDistanceMeters >= 0f && p.PreparationDistanceMeters >= 0f);
        }
    }

    [Fact]
    public void StandingStartPreparationUsesFastestFeasibleCorrectedSteps()
    {
        var expected = Reference(35.125f, 17f);
        var profile = Profile(35.125f, 17f);
        Assert.Equal(expected[^1].EndSpeed, profile.ExitSpeedMetersPerSecond, 4);
        Assert.Equal(expected.Max(s => s.EndSpeed), profile.PeakSpeedMetersPerSecond, 4);
        Assert.Equal((float)expected.Sum(s => s.Time), profile.MovementTimeSeconds, 5);
        Assert.Equal(expected.Where(s => s.EndSpeed > s.StartSpeed + 1e-6f).Sum(s => s.Distance), profile.AccelerationDistanceMeters);
        Assert.Equal(expected.Where(s => s.EndSpeed < s.StartSpeed - 1e-6f).Sum(s => s.Distance), profile.PreparationDistanceMeters);
    }

    [Fact]
    public void StandingStartPreparationNeverExceedsAvailableDeceleration()
    {
        var deceleration = LongitudinalDynamics.CalculateCornerEntryDecelerationMetersPerSecondSquared(RiderSkills.Balanced, Perfect);
        var previous = Profile(35f, 12f);
        // Each longer total distance exposes a different reachable envelope;
        // compare complete production profiles against bounded reference nodes.
        foreach (var distance in new[] { 20f, 35f, 35.125f, 100f })
        {
            var nodes = Reference(distance, 12f);
            var profile = Profile(distance, 12f);
            Assert.Equal(nodes[^1].EndSpeed, profile.ExitSpeedMetersPerSecond, 4);
            Assert.Equal((float)nodes.Sum(s => s.Time), profile.MovementTimeSeconds, 5);
            Assert.All(nodes, node => Assert.True(node.Acceleration >= -deceleration - 0.0001d));
        }
        Assert.True(previous.PreparationDistanceMeters > 0f);
    }

    [Fact]
    public void StandingStartDoesNotTeleportToAnUnreachableHighTarget()
    {
        var unrestricted = Profile(1f, null);
        var withTarget = Profile(1f, 20f);
        Assert.Equal(unrestricted, withTarget);
        Assert.True(withTarget.ExitSpeedMetersPerSecond < 20f);
        Assert.Equal(0f, withTarget.PreparationDistanceMeters);
    }

    [Fact]
    public void StandingStartTargetAboveCeilingDoesNotAddPreparation()
        => Assert.Equal(Profile(100f, null), Profile(100f, 30f));

    [Fact]
    public void StandingStartDoesNotLookThroughAnImmediateStraight()
    {
        var track = new Track(new[]
        {
            new TrackSegment(0, SegmentType.Straight, 35f, true),
            new TrackSegment(1, SegmentType.Straight, 1f),
            new TrackSegment(2, SegmentType.TurnEntry),
            new TrackSegment(3, SegmentType.Straight, 35f),
        });
        Assert.Equal(Profile(35f, null), Launch(Resolve(track)));
    }

    [Fact]
    public void StandingStartApproachTargetUsesNextTurnEntrySampledSurface()
    {
        var track = Track.CreateStandingStartExample();
        var state = new TrackState(track.Segments.Count, LaneModel.LanesCount,
            (segment, _) => segment == 1 ? new TrackSurfaceState(0.6f, 0.1f, 0.35f) : Perfect);
        var step = Resolve(track, state: state);
        var target = FirstTurnApproachTarget(step);
        Assert.Equal(Perfect, step.Diagnostics[0].EntrySurface);
        Assert.True(target < FirstTurnApproachTarget(Resolve()));
        Assert.Equal(Profile(35f, target), Launch(step));
        Assert.Equal(target, Launch(step).ExitSpeedMetersPerSecond, 4);
    }

    [Fact]
    public void StandingStartTimeTo70CanBeReachedBeforeLowerExitSpeed()
    {
        var p = Launch(Resolve());
        Assert.True(p.PeakSpeedMetersPerSecond > LongitudinalDynamics.StandingStartTelemetry70KphMetersPerSecond);
        Assert.True(p.ExitSpeedMetersPerSecond < LongitudinalDynamics.StandingStartTelemetry70KphMetersPerSecond);
        Assert.NotNull(p.TimeTo70KphSeconds);
        Assert.True(p.TimeTo70KphSeconds < p.TotalTimeSeconds);
    }

    [Fact]
    public void StandingStartSpeedAtTwoSecondsCanFallInsidePreparation()
    {
        var p = Profile(20f, 12f);
        var observationMovementTime = 2d - p.ReactionTimeSeconds;
        var node = Reference(20f, 12f).Single(s =>
            s.StartTime <= observationMovementTime && observationMovementTime < s.StartTime + s.Time);
        Assert.True(node.EndSpeed < node.StartSpeed);
        var expected = node.StartSpeed + node.Acceleration * (observationMovementTime - node.StartTime);
        Assert.InRange(Math.Abs(expected - p.SpeedAtTwoSecondsMetersPerSecond!.Value), 0d, 1e-5d);
        Assert.True(p.SpeedAtTwoSecondsMetersPerSecond < p.PeakSpeedMetersPerSecond);
    }

    [Fact]
    public void StandingStartPreparedMovementAgreesWithFineDistanceReference()
    {
        foreach (var distance in new[] { 20f, 35f, 35.125f, 100f })
        {
            var fine = Reference(distance, 17f, 0.05f);
            var p = Profile(distance, 17f);
            Assert.InRange(Math.Abs(fine[^1].EndSpeed - p.ExitSpeedMetersPerSecond), 0d, 0.01d);
            Assert.InRange(Math.Abs(fine.Sum(s => s.Time) - p.MovementTimeSeconds), 0d, 0.01d);
        }
    }

    [Fact]
    public void StandingStartPreparationDistanceIsExportedWithoutReorderingCsv()
    {
        var run = Run();
        var rows = CalibrationCsvExporter.ExportSteps(run.Trace!).Split('\n', StringSplitOptions.RemoveEmptyEntries);
        var header = rows[0].Split(',');
        Assert.Equal("StandingStartPreparationDistanceMeters", header[^1]);
        Assert.Equal("StandingStartSpeedAtTwoSecondsMetersPerSecond", header[^2]);
        var sample = run.Trace!.StepSamples[0];
        Assert.True(sample.StandingStartPreparationDistanceMeters > 0f);
        Assert.Equal(sample.TravelledMeters, sample.StandingStartAccelerationDistanceMeters
            + sample.StandingStartCruiseDistanceMeters + sample.StandingStartPreparationDistanceMeters);
        Assert.Equal(sample.StandingStartPreparationDistanceMeters!.Value.ToString("R", System.Globalization.CultureInfo.InvariantCulture),
            rows[1].Split(',')[^1]);
        Assert.Equal(string.Empty, rows[5].Split(',')[^1]);
    }

    [Fact]
    public void StandingStartPreparationRejectsInvalidTarget()
    {
        foreach (var target in new[] { -1f, float.NaN, float.PositiveInfinity, float.NegativeInfinity })
            Assert.Throws<ArgumentOutOfRangeException>(() => Profile(35f, target));
    }

    private static StandingStartLaunchProfile Profile(float distance, float? target)
        => LongitudinalDynamics.CalculateStandingStartLaunchProfile(RiderSkills.Balanced, BikeSetup.Neutral,
            Perfect, distance, 23f, target);

    // Test-only independent midpoint/reference path. No production integration
    // resolution setting is introduced; the fine 0.05 m oracle exists only here.
    private static List<Node> Reference(float distance, float target, float step = 1f)
    {
        const double force = 142d * 10d + 40d;
        const double deceleration = 2.6d;
        double Acceleration(double v) => Math.Max(0d,
            (force * Math.Max(0d, 1d - 0.01125d * Math.Max(0d, v - 16d)) - 40d - 0.20d * v * v) / 142d);
        var nodes = new List<Node>();
        var speed = 0f;
        var time = 0d;
        for (var position = 0d; position < distance;)
        {
            var ds = (float)Math.Min(step, distance - position);
            var predicted = Math.Min(23d, Math.Sqrt((double)speed * speed + 2d * Acceleration(speed) * ds));
            var midpoint = (speed + predicted) / 2d;
            var fullDrive = (float)Math.Min(23d, Math.Sqrt((double)speed * speed + 2d * Acceleration(midpoint) * ds));
            var allowed = (float)Math.Sqrt((double)target * target + 2d * deceleration * (distance - position - ds));
            var maximumRollOff = (float)Math.Sqrt(Math.Max(0d, (double)speed * speed - 2d * deceleration * ds));
            var end = fullDrive <= allowed ? fullDrive : Math.Max(allowed, maximumRollOff);
            var dt = 2d * ds / (speed + (double)end);
            nodes.Add(new Node(ds, speed, end, time, dt));
            speed = end;
            time += dt;
            position += ds;
        }
        return nodes;
    }

    private sealed record Node(float Distance, float StartSpeed, float EndSpeed, double StartTime, double Time)
    {
        public double Acceleration => ((double)EndSpeed * EndSpeed - (double)StartSpeed * StartSpeed) / (2d * Distance);
    }
}
