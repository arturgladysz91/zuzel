using CoreSim;
using CoreSim.Analysis;
using CoreSim.Decisions;
using CoreSim.Race;
using CoreSim.Setup;
using Xunit;

namespace CoreSim.Tests;

internal static class StandingStartFixture
{
    internal static readonly TrackSurfaceState Perfect = new(1f, 0f, 0.35f);
    internal static HeatSimulationOptions Options(int seed = 90210) => new()
    {
        Laps = 4, Seed = seed, IncidentFrequency = 0f, EnableLogging = false,
        Weather = new WeatherState(WeatherCondition.Cloudy, 0f, 0f),
    };

    internal static RiderState Rider(int id = 1, int lane = 0, float start = 50f, float gearing = 0.5f)
        => new(new RiderProfile(id, $"Rider {id}", new RiderSkills(start, 50f, 50f, 50f, 50f, 50f), RiderStyle.Balanced), lane)
        { ActiveSetup = new BikeSetup(gearing, 0.5f) };

    internal static List<RiderState> Riders(IEnumerable<int>? order = null)
        => (order ?? new[] { 1, 2, 3, 4 }).Select(id => Rider(id, id - 1)).ToList();

    internal static Track StartTrack(float distance = 30f, float spacing = 1f)
        => new(new[] { new TrackSegment(101, SegmentType.Straight, distance, true), new TrackSegment(202, SegmentType.Straight, 30f) },
            new TrackGeometry(60f, 24f, spacing, MathF.PI / 3f));

    internal static ResolvedSimulationStep Resolve(Track? track = null, IReadOnlyList<RiderState>? riders = null,
        TrackState? state = null, bool legacy = false, int lap = 0, int segment = 0,
        int? targetLane = null, int seed = 90210)
    {
        track ??= Track.CreateStandingStartExample();
        riders ??= new[] { Rider() };
        state ??= TrackState.CreateDefault(track, Perfect);
        var engine = new SimulationEngine(new HoldLane(targetLane));
        var options = Options(seed);
        var snapshot = engine.CaptureSnapshot(track, state, riders,
            new SimulationStepContext(30, lap * track.Segments.Count + segment, lap, segment, seed, options.Laps, legacy));
        return engine.Resolve(snapshot, engine.Decide(snapshot), options);
    }

    internal static StandingStartLaunchProfile Launch(ResolvedSimulationStep step)
        => Assert.IsType<StandingStartLaunchProfile>(Assert.Single(step.Diagnostics).StandingStartLaunchProfile);

    internal static RunResult Run(bool observe = true, IEnumerable<int>? order = null, bool logging = false)
    {
        var track = Track.CreateStandingStartExample();
        var state = TrackState.CreateDefault(track, Perfect);
        var riders = Riders(order);
        var options = Options() with { EnableLogging = logging };
        var collector = observe ? new CalibrationTraceCollector(track, options, 30) : null;
        var observer = observe ? new RecordingObserver(collector!) : null;
        var result = new HeatSimulator(new HoldLane()).SimulateHeat(track, state, riders, options, 30, observer);
        return new RunResult(track, state, riders, result, collector?.Complete(result), observer?.Steps ?? new());
    }

    internal static IEnumerable<int[]> Permutations(int[] values)
    {
        if (values.Length == 0) { yield return Array.Empty<int>(); yield break; }
        for (var i = 0; i < values.Length; i++)
        foreach (var tail in Permutations(values.Where((_, index) => index != i).ToArray()))
            yield return new[] { values[i] }.Concat(tail).ToArray();
    }

    internal sealed class HoldLane(int? target = null) : IRiderDecisionModel
    {
        public RiderDecision Decide(TrackSegment segment, RiderState rider) => new(target ?? rider.Lane, 0f);
    }

    private sealed class RecordingObserver(CalibrationTraceCollector collector) : ISimulationStepObserver
    {
        internal List<ResolvedSimulationStep> Steps { get; } = new();
        public void OnStepResolved(ResolvedSimulationStep step) { Steps.Add(step); collector.OnStepResolved(step); }
    }

    internal sealed record RunResult(Track Track, TrackState State, List<RiderState> Riders,
        HeatResult Result, CalibrationTrace? Trace, List<ResolvedSimulationStep> Steps);
}
