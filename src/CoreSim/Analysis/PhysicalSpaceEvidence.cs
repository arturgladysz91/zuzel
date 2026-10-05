using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using CoreSim.Decisions;
using CoreSim.PhysicalSpace;
using CoreSim.Race;

namespace CoreSim.Analysis;

public sealed record SpacePairSummary(int RiderA, int RiderB, double? FirstTouchSeconds, double MinimumSeparationMeters,
    double MinimumTimeSeconds, string Classifications, ClosingContributions? FirstClosing, int ConflictingIntervals,
    int BoundaryAmbiguousIntervals, int FrameCoverageGaps, int UnresolvedIntervals);
public sealed record SpaceScenarioSummary(string Scenario, IReadOnlyList<SpacePairSummary> Pairs, SpaceWorkCounters Work);
public sealed record SpaceSensitivityRow(string Scenario, double LengthMeters, double BodyWidthMeters, double BarWidthMeters,
    double BarOffsetMeters, double PeakYawRadians, bool Conflict, double MinimumSeparationMeters);

/// <summary>Deterministic observation evidence; only Benchmark uses a clock.</summary>
public static class PhysicalSpaceEvidence
{
    public const string BaseMainSha = "12cce6f9709f34ea1617d21399d2a615b3f689af";
    public static bool IsFirstBend(PhysicalPoseInterval interval) => interval.Source is { LapIndex: 0 } source
        && (source.SegmentIndex == 0 || source.CornerId == 1);
    public static LinearBikePoseInterval Linear(int id, MeterPoint start, MeterPoint end, double startHeading = 0,
        double endHeading = 0, double startTime = 0, double endTime = 1, SpeedwayBikeDimensions? dimensions = null,
        string frame = "controlled-metres", bool discontinuity = false)
    {
        var velocity = end - start;
        var travel = velocity.Length == 0 ? 0 : Math.Atan2(velocity.Y, velocity.X);
        var dims = dimensions ?? SpeedwayBikeDimensions.Reference;
        return new(new(id, frame, start, new(startTime, travel, startHeading), dims, 0),
            new(id, frame, end, new(endTime, travel, endHeading), dims, 0), discontinuity);
    }
    public static IReadOnlyList<PhysicalPoseInterval> Controlled(string id, SpeedwayBikeDimensions? dimensions = null)
    {
        PhysicalPoseInterval L(int rider, double x, double y, double ex, double ey, double h0 = 0, double h1 = 0)
            => Linear(rider, new(x, y), new(ex, ey), h0, h1, dimensions: dimensions);
        return id switch
        {
            "A" => new[] { L(1, 0, 0, 20, 0), L(2, 0, 3, 20, 3) },
            "B" => new[] { L(1, 0, 0, 20, 0), L(2, 5, 0, 25, 0) },
            "C" => new[] { L(1, 0, 0, 20, 0), L(2, 0, .2, 20, .2) },
            "D" => new[] { L(1, -5, 0, 25, 0), L(2, 0, 0, 20, 0) },
            "E" => new[] { L(1, 0, 0, 20, 2), L(2, 0, 2, 20, 2) },
            "F" => new[] { L(1, 0, 0, 20, 0), L(2, 0, 2, 20, 0) },
            "G" => new[] { L(1, 0, -1.5, 20, 0), L(2, 0, 1.5, 20, 0) },
            "H" => new[] { L(1, 0, -2, 20, 2), L(2, 0, 2, 20, -2) },
            // Endpoints clear, central orientation crosses B: rotation is not a lane change.
            "I" => new[] { L(1, 0, 0, 20, 0, 0, Math.PI / 2), L(2, 1.5, .65, 21.5, .65) },
            "J" => new[] { L(1, 0, 0, 20, .3, 0, Math.PI / 2), L(2, 1.5, .95, 21.5, .95) },
            "K-zero" => new[] { L(1, 0, 0, 20, 0), L(2, 1.5, .65, 21.5, .65) },
            "K-yaw" => new[] { L(1, 0, 0, 20, 0, Math.PI / 4, Math.PI / 4), L(2, 1.5, .65, 21.5, .65) },
            _ => throw new ArgumentException("Unknown controlled scenario.", nameof(id))
        };
    }
    public static IReadOnlyList<SpacePairSummary> Summarize(ContestedSpaceReport report)
    {
        var ids = report.Intervals.Select(i => (i.RiderA, i.RiderB))
            .Concat(report.FrameCoverageGaps.Select(i => (i.RiderA, i.RiderB))).Distinct().OrderBy(p => p.RiderA).ThenBy(p => p.RiderB);
        return ids.Select(pair =>
        {
            var rows = report.Intervals.Where(r => (r.RiderA, r.RiderB) == pair).ToArray();
            var first = rows.Where(r => r.EligibleForFutureInteraction).OrderBy(r => r.FirstTouchCommonTimeSeconds).FirstOrDefault();
            var min = rows.OrderBy(r => r.MinimumSeparationMeters).ThenBy(r => r.MinimumSeparationCommonTimeSeconds).FirstOrDefault();
            return new SpacePairSummary(pair.RiderA, pair.RiderB, first?.FirstTouchCommonTimeSeconds,
                min?.MinimumSeparationMeters ?? double.NaN, min?.MinimumSeparationCommonTimeSeconds ?? double.NaN,
                first?.Kind.ToString() ?? "No observed eligible conflict", first?.Contributions, rows.Count(r => r.HasConflict),
                rows.Count(r => r.Kind == SpaceConflictKind.BoundaryAmbiguous),
                report.FrameCoverageGaps.Count(g => (g.RiderA, g.RiderB) == pair), rows.Count(r => !r.NumericallyResolved));
        }).ToArray();
    }
    public static (HeatResult Result, List<RiderState> Riders, TrackState Surface, PhysicalSpaceObserver Observer) FourRiderHeat(
        int laps = 4, bool reverse = false, bool capture = true, bool adaptive = false, int seed = 55,
        SpeedwayBikeDimensions? dimensions = null, ReferenceBikeAttitude? attitude = null)
    {
        var track = MatchedVenueProfiles.CreateMotoarenaStandingStartTrack();
        var riders = StartingGrid.Create(track, Enumerable.Range(0, 4).Select(i =>
            new StartingGateAssignment(RiderProfile.CreateDefault(i + 1), (StartingGate)i)).ToArray()).ToList();
        if (reverse) riders.Reverse();
        var surface = TrackState.CreateDefault(track, new TrackSurfaceState(1f, 0f, .35f));
        var observer = new PhysicalSpaceObserver(dimensions, attitude);
        var simulator = new HeatSimulator(adaptive ? new AdaptiveDecisionModel(55) : new LaunchConvergence());
        var result = simulator.SimulateHeat(track, surface, riders,
            new HeatSimulationOptions { Laps = laps, Seed = seed, IncidentFrequency = 0f }, 55, capture ? observer : null);
        return (result, riders, surface, observer);
    }
    public static string DeterministicJson()
    {
        var scenarios = new[] { "A", "B", "C", "D", "E", "F", "G", "H", "I", "J", "K-zero", "K-yaw" }
            .Select(id => { var r = ContestedSpaceResolver.Observe(Controlled(id)); return new SpaceScenarioSummary(id, Summarize(r), r.Work); }).ToArray();
        var four = FourRiderHeat(1).Observer;
        var firstBend = ContestedSpaceResolver.Observe(four.CapturedIntervals.Where(IsFirstBend));
        var complete = FourRiderHeat().Observer;
        var fullHeat = complete.Complete();
        var k = MotionFoundationDiagnostics.CrossingStep();
        var crossing = ContestedSpaceResolver.Observe(k.Motions.SelectMany(m => ResolvedBikePoses.FromMotion(m, k.Snapshot.Track)));
        return JsonSerializer.Serialize(new
        {
            BaseMainSha, Scenarios = scenarios, ProductionCrossing = Summarize(crossing),
            MetricEmbedding = new { Frame = "One deterministic metric model-space track frame across all closed laps",
                complete.Embedding!.Closure, HomeStraightMeters = 35 + 27, Origin = new MeterPoint(0, 0), InitialHeadingRadians = 0 },
            AttitudeProfiles = new[] { ReferenceBikeAttitude.Neutral, new ReferenceBikeAttitude(0, .5, .15, .25, .6),
                new ReferenceBikeAttitude(.1, .5, .4, .7, 1) }.Select((profile, index) => new
                { Profile = new[] { "neutral", "early", "late" }[index], Samples = new[] { 0d, .1, 1d / 3, .5, 2d / 3, .9, 1d }
                    .Select(p => new { CornerProgress = p, BetaRadians = profile.RelativeSlideAngle(p) }).ToArray() }),
            FourRiderStartAndFirstBend = new { Pairs = Summarize(firstBend), firstBend.Work, firstBend.IncompatibleFrameIntervals },
            FourRiderCompleteHeat = new { CapturedPoseIntervals = complete.CapturedIntervals.Count,
                Pairs = Summarize(fullHeat), fullHeat.Work, fullHeat.IncompatibleFrameIntervals },
            Sensitivity = Sensitivity()
        }, JsonOptions).Replace("\r\n", "\n", StringComparison.Ordinal) + "\n";
    }
    public static IReadOnlyList<SpaceSensitivityRow> Sensitivity()
    {
        var rows = new List<SpaceSensitivityRow>();
        foreach (var id in new[] { "A", "B", "C", "I", "J" })
        foreach (var dims in new[] { new SpeedwayBikeDimensions(1.9, .25, .7, .55, .03), SpeedwayBikeDimensions.Reference,
            new SpeedwayBikeDimensions(2.3, .35, .9, .75, .05) })
        foreach (var bar in new[] { .7, .8, .9 })
        foreach (var yaw in new[] { 0d, Math.PI / 6, Math.PI / 3 })
        {
            var d = new SpeedwayBikeDimensions(dims.OverallMechanicalLengthMeters, dims.ChassisBodyWidthMeters, bar,
                dims.HandlebarLongitudinalOffsetMeters, dims.HandlebarTubeDiameterMeters);
            var input = Controlled(id, d);
            if (id is "I" or "J") input = new PhysicalPoseInterval[]
            {
                Linear(1, input[0].Sample(0).Position, input[0].Sample(1).Position, 0, yaw, dimensions: d), input[1]
            };
            var result = ContestedSpaceResolver.Observe(input);
            rows.Add(new(id, d.OverallMechanicalLengthMeters, d.ChassisBodyWidthMeters, bar, d.HandlebarLongitudinalOffsetMeters, yaw,
                result.Intervals.Any(r => r.HasConflict), result.Intervals.Min(r => r.MinimumSeparationMeters)));
        }
        foreach (var dims in new[] { new SpeedwayBikeDimensions(1.9, .25, .7, .55, .03), SpeedwayBikeDimensions.Reference,
            new SpeedwayBikeDimensions(2.3, .35, .9, .75, .05) })
        foreach (var bar in new[] { .7, .8, .9 })
        foreach (var peak in new[] { 0d, Math.PI / 6, Math.PI / 3 })
        {
            var d = new SpeedwayBikeDimensions(dims.OverallMechanicalLengthMeters, dims.ChassisBodyWidthMeters, bar,
                dims.HandlebarLongitudinalOffsetMeters, dims.HandlebarTubeDiameterMeters);
            var profile = new ReferenceBikeAttitude(.05, peak, .35, .55, .95);
            var captured = FourRiderHeat(1, dimensions: d, attitude: profile).Observer.CapturedIntervals;
            var result = ContestedSpaceResolver.Observe(captured.Where(i => IsFirstBend(i) && i.RiderId is 1 or 3));
            rows.Add(new("production-first-bend-1/3", d.OverallMechanicalLengthMeters, d.ChassisBodyWidthMeters, bar,
                d.HandlebarLongitudinalOffsetMeters, peak, result.Intervals.Any(r => r.EligibleForFutureInteraction),
                result.Intervals.Min(r => r.MinimumSeparationMeters)));
        }
        return rows.AsReadOnly();
    }
    public static string BenchmarkJson()
    {
        // Capture resolved production once; compare CCD on identical immutable motions.
        var captureStart = GC.GetAllocatedBytesForCurrentThread(); var captureWatch = Stopwatch.StartNew();
        var captured = FourRiderHeat(); captureWatch.Stop(); var captureBytes = GC.GetAllocatedBytesForCurrentThread() - captureStart;
        var input = captured.Observer.CapturedIntervals;
        ContestedSpaceResolver.Observe(input); // JIT warmup
        var timings = new List<double>(); var allocations = new List<long>(); ContestedSpaceReport? report = null;
        for (var repeat = 0; repeat < 5; repeat++)
        {
            var before = GC.GetAllocatedBytesForCurrentThread(); var watch = Stopwatch.StartNew();
            report = ContestedSpaceResolver.Observe(input); watch.Stop();
            timings.Add(watch.Elapsed.TotalMilliseconds); allocations.Add(GC.GetAllocatedBytesForCurrentThread() - before);
        }
        return JsonSerializer.Serialize(new { Fixture = "Motoarena four riders, four laps, fixed convergence intents",
            CapturedPoseIntervals = input.Count, CaptureHeatMilliseconds = captureWatch.Elapsed.TotalMilliseconds, CaptureHeatAllocatedBytes = captureBytes,
            ResolverMilliseconds = timings, ResolverAllocatedBytes = allocations, report!.Work,
            BoundaryAmbiguousIntervals = report.Intervals.Count(i => i.Kind == SpaceConflictKind.BoundaryAmbiguous),
            report.IncompatibleFrameIntervals }, JsonOptions)
            .Replace("\r\n", "\n", StringComparison.Ordinal) + "\n";
    }
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true,
        Converters = { new JsonStringEnumConverter(), new EvidenceDoubleConverter() } };
    /// <summary>Presentation rounding only, 5e-11 maximum error, below CCD numerical tolerances.</summary>
    private sealed class EvidenceDoubleConverter : JsonConverter<double>
    {
        public override double Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) => reader.GetDouble();
        public override void Write(Utf8JsonWriter writer, double value, JsonSerializerOptions options)
            => writer.WriteNumberValue(Math.Round(value, 10, MidpointRounding.ToEven));
    }
    private sealed class LaunchConvergence : IRiderDecisionModel
    {
        public RiderDecision Decide(TrackSegment segment, RiderState rider) => new(rider.RiderId switch { 1 => 2, 2 => 0, 3 => 2, _ => 3 }, 0);
    }
}
