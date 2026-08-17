using CoreSim;
using CoreSim.Logging;
using CoreSim.Race;
using Xunit;

namespace CoreSim.Tests;

public sealed class RaceProgressTrackerTests
{
    [Fact]
    public void CapturesOvertakeAndLapOrderWithoutCountingCrashAsOvertake()
    {
        var first = RiderState.CreateDefault(1, 0);
        var second = RiderState.CreateDefault(2, 1);
        first.ElapsedTimeSeconds = 10f;
        second.ElapsedTimeSeconds = 11f;
        var riders = new[] { first, second };
        var tracker = new RaceProgressTracker();
        var log = new SimLog();
        tracker.InitializeStartingGrid(riders);

        tracker.CaptureSegment(1, 0, lapComplete: false, riders, log);
        first.ElapsedTimeSeconds = 12f;
        second.ElapsedTimeSeconds = 11.5f;
        tracker.CaptureSegment(1, 1, lapComplete: false, riders, log);

        var overtake = Assert.Single(log.Overtakes);
        Assert.Equal(2, overtake.RiderId);
        Assert.Equal(1, overtake.PassedRiderId);
        Assert.Equal(2, overtake.FromPosition);
        Assert.Equal(1, overtake.ToPosition);

        first.IsCrashed = true;
        tracker.CaptureSegment(1, 7, lapComplete: true, riders, log);

        Assert.Single(log.Overtakes);
        var snapshot = Assert.Single(log.OrderSnapshots);
        Assert.Equal(new[] { 2, 1 }, snapshot.Order.Select(entry => entry.RiderId));
        Assert.True(snapshot.Order[1].Crashed);
        Assert.Contains(log.Lines, line => line.StartsWith("LAP_ORDER lap=1"));
    }

    [Fact]
    public void PassingRetiredRiderDoesNotCreateOvertakeAndLapOrderMarksDnf()
    {
        var first = RiderState.CreateDefault(1, 0);
        var second = RiderState.CreateDefault(2, 1);
        first.ElapsedTimeSeconds = 10f;
        second.ElapsedTimeSeconds = 11f;
        var riders = new[] { first, second };
        var tracker = new RaceProgressTracker();
        var log = new SimLog();
        tracker.InitializeStartingGrid(riders);
        tracker.CaptureSegment(1, 0, lapComplete: false, riders, log);

        first.Retire();
        tracker.CaptureSegment(1, 7, lapComplete: true, riders, log);

        Assert.Empty(log.Overtakes);
        var snapshot = Assert.Single(log.OrderSnapshots);
        Assert.Equal(new[] { 2, 1 }, snapshot.Order.Select(entry => entry.RiderId));
        Assert.True(snapshot.Order[1].Crashed);
        Assert.Contains(log.Lines, line => line.Contains("2:rider=1 DNF"));
    }
}
