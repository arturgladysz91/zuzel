using System.Diagnostics;
using System.Globalization;
using System.IO.Compression;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using CoreSim;
using CoreSim.Analysis;
using CoreSim.Decisions;
using CoreSim.Interactions;
using CoreSim.PhysicalSpace;
using CoreSim.Race;
using RaceReadiness;

CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
var mode = args.ElementAtOrDefault(0) ?? "pilot";
var output = args.ElementAtOrDefault(1) ?? "results/readiness";
var source = args.ElementAtOrDefault(2) ?? "c13af7b8a16fe4e58e9d404c849cdf323649b0ed";
Directory.CreateDirectory(output);
var json = new JsonSerializerOptions { WriteIndented = false, NumberHandling = JsonNumberHandling.AllowNamedFloatingPointLiterals };
json.Converters.Add(new JsonStringEnumConverter());
var catalog = Catalog.All();
if (mode is not ("pilot" or "smoke" or "batch" or "trace" or "repro")) throw new ArgumentException("pilot|smoke|batch|trace|repro output source-commit [scenario seed configuration]");
var selected = mode switch
{
    "pilot" or "smoke" => catalog.Where(s => s.Id is "balanced-example" or "balanced-motoarena"),
    "trace" => catalog.Where(s => s.Id is "same-line" or "rolling-C" or "contact-heavy" or "rain-motoarena"),
    "repro" => catalog.Where(s => s.Id == (args.ElementAtOrDefault(3) ?? "outside")),
    _ => catalog,
};
var observations = new List<RunEvidence>(); var begun = Stopwatch.StartNew();
double priorWallSeconds = 0;
if (mode == "batch" && args.Contains("--resume") && File.Exists(Path.Combine(output, "manifest.json")))
{
    using var previous = JsonDocument.Parse(File.ReadAllText(Path.Combine(output, "manifest.json")));
    if (previous.RootElement.GetProperty("SourceCommit").GetString() != source)
        throw new InvalidOperationException("Resume must use the same production source commit.");
    if (previous.RootElement.TryGetProperty("WallSeconds", out var prior)) priorWallSeconds = prior.GetDouble();
}
// Hard offline budget: no silently expanding Cartesian product. Pilot must be inspected first.
var limit = mode == "batch" ? TimeSpan.FromMinutes(90) : TimeSpan.FromMinutes(10);
foreach (var scenario in selected)
foreach (var seed in mode == "repro" ? new[] { int.Parse(args.ElementAtOrDefault(4) ?? "7", CultureInfo.InvariantCulture) }
    : mode is "pilot" or "smoke" or "trace" || scenario.Sweep ? new[] { 19 } : new[] { 7, 19, 83 })
foreach (var configuration in mode == "repro" ? new[] { args.ElementAtOrDefault(5) ?? "B" } : new[] { "A", "B", "C" })
{
    if (begun.Elapsed.TotalSeconds + priorWallSeconds > limit.TotalSeconds) throw new InvalidOperationException("Explicit execution budget exhausted; incomplete evidence is not success.");
    var options = new HeatSimulationOptions { Seed = seed, Weather = scenario.Weather,
        EnableContestedSpaceResponses = configuration != "A", EnablePhysicalContactConsequences = configuration == "C" };
    var cachePath = Path.Combine(output, $"case-{scenario.Id}-{seed}-{configuration}.json.gz");
    if (mode == "batch" && args.Contains("--resume") && File.Exists(cachePath))
    {
        using var cached = new GZipStream(File.OpenRead(cachePath), CompressionMode.Decompress);
        var row = JsonSerializer.Deserialize<RunEvidence>(cached, json)!;
        if (row.Id != scenario.Id || row.Seed != seed || row.Configuration != configuration || !row.ReversedExact)
            throw new InvalidOperationException("Invalid cached evidence.");
        observations.Add(row); continue;
    }
    var first = Run(scenario, options, false, true, false);
    if (first.Failed && mode is "pilot" or "smoke") throw new InvalidOperationException("Audit smoke heat did not complete: " + first.Failure);
    if (mode is "trace" or "repro")
    {
        var diagnostic = Run(scenario, options with { InteractionDiagnostics = InteractionDiagnosticsLevel.FullAudit,
            PhysicalContactDiagnostics = PhysicalContactDiagnosticsLevel.FullAudit }, false, true, true);
        if (diagnostic.FinalHash != first.FinalHash) throw new InvalidOperationException("Full diagnostics changed exact final state.");
        // Extra candidate telemetry is recorded only on named representative reruns.
        using var diagnosticStream = new GZipStream(File.Create(Path.Combine(output,
            $"diagnostic-{scenario.Id}-{seed}-{configuration}.json.gz")), CompressionLevel.SmallestSize);
        JsonSerializer.Serialize(diagnosticStream, diagnostic, json);
    }
    var reverse = Run(scenario, options, true, true, false);
    if (first.BehaviorHash != reverse.BehaviorHash || first.FinalHash != reverse.FinalHash)
        throw new InvalidOperationException($"Exact input-order regression: {scenario.Id}/{seed}/{configuration}");
    // Representative no-observer and repeat runs cover observer parity and cross-heat isolation.
    var checkObserver = first.Failed || mode is "pilot" or "smoke" or "repro" || (scenario.Id == "balanced-motoarena" && seed == 19);
    if (checkObserver)
    {
        var bare = Run(scenario, options, false, false, false);
        if (bare.FinalHash != first.FinalHash) throw new InvalidOperationException("Observer changed exact final state.");
        if (mode == "repro")
        {
            var reverseBare = Run(scenario, options, true, false, false);
            if (reverseBare.FinalHash != first.FinalHash)
                throw new InvalidOperationException("Reversed unobserved heat changed exact final state.");
        }
    }
    first.ReversedExact = true; first.ObserverExact = checkObserver ? true : null;
    observations.Add(first);
    // Persist each result before starting the next expensive heat; offline runs can resume without rerunning completed cases.
    using (var cached = new GZipStream(File.Create(cachePath), CompressionLevel.SmallestSize)) JsonSerializer.Serialize(cached, first, json);
    Console.WriteLine($"{scenario.Id}/{seed}/{configuration}: {(first.Failed ? "PRODUCTION FAILURE" : "completed")}, {first.Milliseconds:F0} ms, passes={first.Order.Passes.Count}, incidents={first.Incidents.Count}, applied={first.Applied.Count}");
    File.WriteAllText(Path.Combine(output, "manifest.json"), JsonSerializer.Serialize(new
    { Schema = "race-readiness-v1", SourceCommit = source, Mode = mode, Completed = false, Runs = observations.Count,
        WallSeconds = priorWallSeconds + begun.Elapsed.TotalSeconds }, json));
}
// Keep detailed numeric raw observations compressed; no duplicate giant snapshot dumps.
using (var stream = new GZipStream(File.Create(Path.Combine(output, "runs.json.gz")), CompressionLevel.SmallestSize))
    JsonSerializer.Serialize(stream, observations, json);
File.WriteAllText(Path.Combine(output, "manifest.json"), JsonSerializer.Serialize(new
{
    Schema = "race-readiness-v1", SourceCommit = source, Mode = mode, Completed = true,
    Environment = new { OS = RuntimeInformation.OSDescription, Runtime = RuntimeInformation.FrameworkDescription,
        Architecture = RuntimeInformation.ProcessArchitecture.ToString(), Environment.ProcessorCount,
        DecisionModelSeed = 1234, Stopwatch.Frequency }, BudgetMinutes = limit.TotalMinutes,
    WallSeconds = priorWallSeconds + begun.Elapsed.TotalSeconds, ThisInvocationSeconds = begun.Elapsed.TotalSeconds,
    ProductionFailures = observations.Count(r => r.Failed),
    CompletedHeats = observations.Count(r => !r.Failed), Cases = observations.Select(r => new { r.Id, r.Seed, r.Configuration,
        r.FinalHash, r.BehaviorHash, r.ReversedExact, r.ObserverExact, r.Milliseconds, r.AllocatedBytes, r.Failed }).ToArray(),
    Raw = "runs.json.gz", ExtraExecution = "Every case reversed; representative cases also run without observer. Degraded preparation uses a prior production heat.",
}, json));
if (mode == "batch")
{
    var controlled = PhysicalContactEvidence.Fixtures().Select(f => new { f.Name, f.Analysis,
        Applied = PhysicalContactConsequenceEvidence.Plan(f) }).ToArray();
    File.WriteAllText(Path.Combine(output, "controlled-contact.json"), JsonSerializer.Serialize(controlled, json));
    var dataset = RealWorldCalibrationDataset.ParseCsv(File.ReadAllText("data/calibration/pge/v1/pge_rider_heats.csv"));
    var venue = dataset.FilterByExactVenue(2026, "Motoarena im. Mariana Rosego");
    File.WriteAllText(Path.Combine(output, "real-reference.json"), JsonSerializer.Serialize(new
    {
        WholeDatasetRows = dataset.Rows.Count, VenueRows = venue.Rows.Count,
        Whole = dataset.Distributions.Values.Select(d => new { d.Definition, N = d.Observations.Count, d.Quantiles }),
        Motoarena2026 = venue.Distributions.Values.Select(d => new { d.Definition, N = d.Observations.Count, d.Quantiles }),
    }, json));
    var evaluations = new List<object>();
    foreach (var configuration in new[] { "A", "B", "C" })
    {
        var rows = observations.Where(r => r.Configuration == configuration && r.Track == "motoarena" && !r.Sweep).ToArray();
        var summaries = rows.SelectMany(r => r.Riders).Where(r => r.Status == RiderRaceStatus.Finished).ToArray();
        var complete = rows.Where(r => r.Riders.Count(x => x.Status == RiderRaceStatus.Finished) == 4 && r.Laps.Count == 16).ToArray();
        var series = new List<SimulationMetricSeries>
        {
            new("pge_clean_vmax", "km/h", summaries.Select(r => r.MaxSpeedMetersPerSecond * 3.6)),
            new("pge_clean_average_speed", "m/s", summaries.Select(r => (double)r.AverageSpeedMetersPerSecond!.Value)),
            new("pge_clean_l1_penalty", "s", rows.SelectMany(r => Penalties(r.Laps))),
            new("pge_clean_heat_time", "s", summaries.Select(r => (double)r.TotalTimeSeconds)),
            new("pge_clean_l1_time", "s", rows.SelectMany(r => r.Laps.Where(l => l.LapNumber == 1).Select(l => (double)l.LapTimeSeconds))),
            new("pge_four_rider_heat_time_spread", "s", complete.Select(r => (double)r.Riders.Max(x => x.TotalTimeSeconds) - r.Riders.Min(x => x.TotalTimeSeconds))),
            new("pge_four_rider_vmax_spread", "km/h", complete.Select(r => (r.Riders.Max(x => (double)x.MaxSpeedMetersPerSecond) - r.Riders.Min(x => x.MaxSpeedMetersPerSecond)) * 3.6)),
            new("pge_four_rider_l1_spread", "s", complete.Select(r => r.Laps.Where(l => l.LapNumber == 1).Max(l => (double)l.LapTimeSeconds) - r.Laps.Where(l => l.LapNumber == 1).Min(l => l.LapTimeSeconds))),
        };
        evaluations.Add(new { Configuration = configuration, Population = "all audit Motoarena standing-start cases; neutral/synthetic abilities, not mapped professionals",
            VenueEvaluation = RealWorldCalibrationEvaluator.Evaluate(venue, new("readiness-motoarena-" + configuration, series)),
            WholeDatasetContext = RealWorldCalibrationEvaluator.Evaluate(dataset, new("readiness-motoarena-" + configuration, series)) });
    }
    File.WriteAllText(Path.Combine(output, "pgee-evaluation.json"), JsonSerializer.Serialize(evaluations, json));
}

IEnumerable<double> Penalties(IReadOnlyList<CalibrationLapSummary> laps)
{
    foreach (var rider in laps.GroupBy(l => l.RiderId).Where(g => g.Count() == 4))
    { var flying = rider.Where(l => l.LapNumber > 1).Select(l => (double)l.LapTimeSeconds).Order().ToArray();
        yield return rider.Single(l => l.LapNumber == 1).LapTimeSeconds - flying[1]; }
}

RunEvidence Run(Experiment scenario, HeatSimulationOptions options, bool reverse, bool observe, bool fullTrace)
{
    var (track, surface, riders) = scenario.Create();
    if (reverse) riders.Reverse();
    var initial = riders.OrderBy(r => r.Profile.Id).Select(r => new { r.Profile, r.StartingPosition, r.Condition, r.Lane,
        r.Speed, r.Position, r.ElapsedTimeSeconds, r.ActiveSetup }).ToArray();
    var collector = new Collector(track, options);
    var allocation = GC.GetTotalAllocatedBytes(false); var timer = Stopwatch.StartNew();
    HeatResult result;
    try
    {
        result = new HeatSimulator(new AdaptiveDecisionModel()).SimulateHeat(track, surface, riders, options, 91, observe ? collector : null);
    }
    catch (Exception exception)
    {
        timer.Stop(); allocation = GC.GetTotalAllocatedBytes(false) - allocation;
        // A failed production heat is evidence, never a fabricated classification or an audit success.
        var frozen = new { Riders = riders.OrderBy(r => r.Profile.Id).ToArray(), Surface = surface.Snapshot(),
            ExceptionType = exception.GetType().FullName, exception.Message };
        var failure = new { exception.Message, Type = exception.GetType().FullName, exception.StackTrace,
            LastCompletedStep = collector.Steps.LastOrDefault()?.Snapshot.Step,
            PreFailureState = Exact.Leaves(frozen) };
        var partial = new { Steps = collector.Steps.Select(s => new { s.Snapshot, s.Changes, s.Motions, s.Diagnostics, s.Events, s.Interaction }).ToArray(), Failure = frozen };
        var configuration = options.EnablePhysicalContactConsequences ? "C" : options.EnableContestedSpaceResponses ? "B" : "A";
        if (observe)
        {
            using var failureStream = new GZipStream(File.Create(Path.Combine(output, $"failure-{scenario.Id}-{options.Seed}-{configuration}-{reverse}.json.gz")), CompressionLevel.SmallestSize);
            JsonSerializer.Serialize(failureStream, new { Trace = Exact.Leaves(partial), Failure = failure }, json);
        }
        return new() { Id = scenario.Id, Seed = options.Seed, Configuration = configuration, Population = scenario.Population,
            Description = scenario.Description, Sweep = scenario.Sweep, Geometry = track.Geometry,
            Track = scenario.Description.StartsWith("motoarena", StringComparison.Ordinal) ? "motoarena" : "synthetic",
            Options = Exact.Leaves(options), Initial = initial, Failed = true, Failure = failure,
            FinalHash = Exact.Hash(frozen), BehaviorHash = Exact.Hash(partial), Milliseconds = timer.Elapsed.TotalMilliseconds,
            AllocatedBytes = allocation, Exposure = new { Intervals = Array.Empty<object>() },
            Events = Array.Empty<object>(), Episodes = Array.Empty<object>(), Work = Array.Empty<object>(),
            Pairs = Array.Empty<object>(), Recoveries = Array.Empty<object>(), Generations = Array.Empty<object>(),
            Segments = Array.Empty<object>(), Starts = Array.Empty<object>(), UncertifiedFootprints = Array.Empty<object>(),
            Violations = new object[] { new { Kind = "uncaught production exception", failure.Type, exception.Message } } };
    }
    timer.Stop(); allocation = GC.GetTotalAllocatedBytes(false) - allocation;
    var final = new { result.Classification, result.Log, Riders = riders.OrderBy(r => r.Profile.Id).ToArray(), Surface = surface.Snapshot() };
    var finalHash = Exact.Hash(final);
    if (!observe) return new() { FinalHash = finalHash };
    var trace = collector.Calibration.Complete(result);
    var motions = collector.Steps.SelectMany(s => s.Motions).ToArray();
    var embedding = new TrackMetricEmbedding(track, options.EnablePhysicalContactConsequences);
    var pieces = OrderEvidence.Pieces(motions);
    var firstCorner = track.CornerTopology.Corners.First();
    var firstExit = motions.Any(m => m.ReactionTimeSeconds > 0) ? firstCorner.EndSegmentIndex + 1d : 0;
    var order = OrderEvidence.Analyze(pieces, firstExit);
    var exposureTimer = Stopwatch.StartNew();
    var physicalHistory = CommonTimePoseHistory.Stitch(motions.SelectMany(m => ResolvedBikePoses.FromMotion(m, track, embedding: embedding)));
    var physical = ContestedSpaceResolver.Observe(physicalHistory);
    var byRider = physicalHistory.GroupBy(p => p.RiderId).ToDictionary(g => g.Key, g => g.ToArray());
    var exposure = physical.Intervals.Where(r => r.NumericallyResolved && !r.Kind.HasFlag(SpaceConflictKind.BoundaryAmbiguous)
        && r.MinimumSeparationMeters <= options.ContestedSpaceParameters.CompetitiveReachMeters).Select(r =>
    {
        var t = r.MinimumSeparationCommonTimeSeconds;
        var a = byRider[r.RiderA].First(p => p.StartTimeSeconds <= t && p.EndTimeSeconds >= t);
        var b = byRider[r.RiderB].First(p => p.StartTimeSeconds <= t && p.EndTimeSeconds >= t);
        var relation = InteractionGeometryModel.Describe(r, a.Sample(t), b.Sample(t),
            a.RateBounds(a.StartTimeSeconds, a.EndTimeSeconds), b.RateBounds(b.StartTimeSeconds, b.EndTimeSeconds));
        return new { r.RiderA, r.RiderB, r.IntervalStartSeconds, r.IntervalEndSeconds,
            r.MinimumSeparationCommonTimeSeconds, r.MinimumSeparationMeters, r.FirstTouchCommonTimeSeconds,
            r.Kind, relation.AOverlap, relation.BOverlap, relation.ForwardBFromAMeters, relation.OutwardBFromAMeters };
    }).ToArray();
    exposureTimer.Stop();
    var boundaryOrders = new List<object>();
    for (var lap = 0; lap < options.Laps; lap++)
    foreach (var boundary in track.CornerTopology.Corners.SelectMany(c => new[] { c.StartSegmentIndex, c.EndSegmentIndex + 1 }).Append(track.Segments.Count).Distinct())
    {
        var p = lap * track.Segments.Count + (double)boundary;
        var arrivals = motions.GroupBy(m => m.RiderId).Select(g => new { Rider = g.Key, Time = Arrival(g, p) })
            .Where(x => x.Time.HasValue).OrderBy(x => x.Time).ToArray();
        boundaryOrders.Add(new { Lap = lap + 1, Boundary = boundary, CanonicalProgress = p, ArrivalGroups = arrivals.GroupBy(x => x.Time).Select(g =>
            new { Time = g.Key, Riders = g.Select(x => x.Rider).ToArray(), AmbiguousTie = g.Count() > 1 }).ToArray() });
    }
    var violations = new List<object>(); var uncertified = new List<object>();
    foreach (var motion in motions)
    {
        var poses = ResolvedBikePoses.FromMotion(motion, track, embedding: embedding);
        if (!ContestedSpaceInteractionCoordinator.WithinTrack(poses.ToArray(), track, embedding))
            uncertified.Add(new { motion.RiderId, motion.SegmentId, motion.StartElapsedTimeSeconds });
        foreach (var n in motion.Nodes)
        {
            if (!float.IsFinite(n.SpeedMetersPerSecond) || n.SpeedMetersPerSecond < 0)
                violations.Add(new { Kind = "speed", motion.RiderId, motion.SegmentId, Node = n });
            var width = track.Segments[motion.SegmentIndex].Type == SegmentType.Straight ? track.Geometry.StraightWidthMeters : track.Geometry.TurnWidthMeters;
            if (n.PhysicalOffsetFromInnerEdgeMeters < 0 || n.PhysicalOffsetFromInnerEdgeMeters > width)
                violations.Add(new { Kind = "reference point outside edge", motion.RiderId, motion.SegmentId, Node = n });
        }
    }
    var applied = collector.Steps.SelectMany(s => s.Interaction?.PhysicalContactConsequences?.Riders.Select(r => new
        { Step = s.Snapshot.Step.StepNumber, Consequence = r }) ?? Enumerable.Empty<object>()).ToArray();
    var pairs = collector.Steps.SelectMany(s => s.Interaction?.PhysicalContactConsequences?.AppliedPairs.Select(p => new
        { Step = s.Snapshot.Step.StepNumber, Pair = p, p.FrontierStartTimeSeconds, p.FrontierRiderIds }) ?? Enumerable.Empty<object>()).ToArray();
    var episodes = collector.Steps.SelectMany(s => s.Interaction?.Episodes ?? Array.Empty<InteractionEpisodeDiagnostic>())
        .GroupBy(e => e.EpisodeId).Select(g => g.Last()).ToArray();
    var recoveries = collector.Steps.SelectMany(s => s.Snapshot.Riders.Where(r => r.ContactRecovery is not null).Select(r => new
    {
        Step = s.Snapshot.Step.StepNumber, r.RiderId, Incoming = r.ContactRecovery, Duration = s.Motions.SingleOrDefault(m => m.RiderId == r.RiderId)?.TotalTimeSeconds,
        Outgoing = s.Changes.SingleOrDefault(c => c.RiderId == r.RiderId)?.ContactRecovery,
        Applied = s.Diagnostics.SingleOrDefault(d => d.RiderId == r.RiderId)?.PhysicalContactConsequence,
    })).ToArray();
    // Observe committed and staged ownership through the existing closure; never invoke it or mutate the tracker.
    var generations = collector.Generations;
    var behavior = new { Steps = collector.Steps.Select(s => new { s.Snapshot, s.Changes, s.Motions, s.Diagnostics, s.Events, s.Interaction }).ToArray(), Final = final };
    var behaviorHash = Exact.Hash(behavior);
    if (fullTrace)
    {
        using var stream = new GZipStream(File.Create(Path.Combine(output, $"trace-{scenario.Id}-{options.Seed}-{(options.EnablePhysicalContactConsequences ? "C" : options.EnableContestedSpaceResponses ? "B" : "A")}.json.gz")), CompressionLevel.SmallestSize);
        JsonSerializer.Serialize(stream, Exact.Leaves(behavior), json);
    }
    var starts = motions.Where(m => m.ReactionTimeSeconds > 0).Select(m => new
    {
        m.RiderId, m.ReactionTimeSeconds,
        InitialMovementSeconds = m.StartElapsedTimeSeconds + m.ReactionTimeSeconds,
        SpeedAt1Second = SpeedAt(motions.Where(x => x.RiderId == m.RiderId), 1),
        SpeedAt2Seconds = SpeedAt(motions.Where(x => x.RiderId == m.RiderId), 2),
        Profile = collector.Steps.SelectMany(s => s.Diagnostics).First(d => d.RiderId == m.RiderId && d.StandingStartLaunchProfile is not null).StandingStartLaunchProfile,
    }).ToArray();
    // Bounded aggregate observations; exact raw nodes/profiles are kept in the named full diagnostic traces.
    var segments = collector.Steps.SelectMany(s => s.Motions.Select(m => new
    {
        Step = s.Snapshot.Step.StepNumber, Lap = s.Snapshot.Step.LapIndex, m.RiderId, m.SegmentIndex, m.SegmentId,
        Type = s.Snapshot.Segment.Type, m.StartElapsedTimeSeconds, m.TotalTimeSeconds, m.TotalDistanceMeters,
        NodeCount = m.Nodes.Count, EntrySpeed = m.Initial.SpeedMetersPerSecond, ExitSpeed = m.Final.SpeedMetersPerSecond,
        MinSpeed = m.Nodes.Min(n => n.SpeedMetersPerSecond), MaxSpeed = m.Nodes.Max(n => n.SpeedMetersPerSecond),
        MinLateral = m.Nodes.Min(n => n.PhysicalOffsetFromInnerEdgeMeters), MaxLateral = m.Nodes.Max(n => n.PhysicalOffsetFromInnerEdgeMeters),
        MinCurvature = m.Nodes.Min(n => n.CurvaturePerMeter), MaxCurvature = m.Nodes.Max(n => n.CurvaturePerMeter),
        MinSampledGrip = m.Nodes.Min(n => s.Snapshot.TrackState.SampleSurface(m.SegmentIndex, n.LateralPosition).EffectiveGrip),
        MaxSampledGrip = m.Nodes.Max(n => s.Snapshot.TrackState.SampleSurface(m.SegmentIndex, n.LateralPosition).EffectiveGrip),
        Acceleration = AccelerationRange(m.Nodes.Zip(m.Nodes.Skip(1)).Where(p => p.Second.LocalTimeSeconds > p.First.LocalTimeSeconds)
            .Select(p => ((double)p.Second.SpeedMetersPerSecond - p.First.SpeedMetersPerSecond) /
                (p.Second.LocalTimeSeconds - p.First.LocalTimeSeconds)).DefaultIfEmpty()),
        m.StateTransitions, m.EntryBoundary, m.ExitBoundary,
        Correction = s.Diagnostics.Single(d => d.RiderId == m.RiderId).CornerSpeedCorrectionProfile,
        TurnExitAcceleration = s.Diagnostics.Single(d => d.RiderId == m.RiderId).TurnExitNetAccelerationMetersPerSecondSquared,
        Phase = s.Diagnostics.Single(d => d.RiderId == m.RiderId).CornerPhaseContext,
    })).ToArray();
    var finished = result.Classification.Where(r => r.Status == RiderRaceStatus.Finished).ToArray();
    if (!finished.Select(r => r.TimeSeconds).SequenceEqual(finished.Select(r => r.TimeSeconds).Order()))
        violations.Add(new { Kind = "finish order differs from physical finish times", result.Classification });
    foreach (var r in recoveries.Where(r => r.Outgoing is not null && r.Applied is null))
        violations.Add(new { Kind = "recovery retained without fresh contact", Recovery = r });
    return new()
    {
        Id = scenario.Id, Seed = options.Seed, Configuration = options.EnablePhysicalContactConsequences ? "C" : options.EnableContestedSpaceResponses ? "B" : "A",
        Population = scenario.Population, Description = scenario.Description, Sweep = scenario.Sweep,
        Track = scenario.Description.StartsWith("motoarena", StringComparison.Ordinal) ? "motoarena" : "synthetic", Geometry = track.Geometry,
        Options = Exact.Leaves(options), Initial = initial, FinalHash = finalHash, BehaviorHash = behaviorHash,
        Milliseconds = timer.Elapsed.TotalMilliseconds, AllocatedBytes = allocation,
        Riders = trace.RiderSummaries, Laps = trace.LapSummaries, Classification = result.Classification,
        Order = order, BoundaryOrders = boundaryOrders, Starts = starts, Segments = segments,
        Incidents = trace.StepSamples.Where(s => s.Outcome != SegmentOutcome.Ok).ToArray(),
        Events = collector.Steps.SelectMany(s => s.Events.Where(e => e.Type != SimulationEventType.SegmentResolved)).ToArray(),
        Episodes = episodes, Applied = applied, Pairs = pairs, Recoveries = recoveries, Generations = generations,
        Work = collector.Steps.Select(s => s.Interaction?.Work).Where(w => w is not null).ToArray(),
        Suppressions = collector.Steps.Sum(s => s.Interaction?.PhysicalContactConsequences?.RepeatedOverlapSuppressions ?? 0),
        UncertifiedFootprints = uncertified, Violations = violations,
        Exposure = new { Measurement = "native executed-pose interval brackets containing a verified near/alongside sample; duration is an upper bracket, not exact occupancy",
            ThresholdMeters = options.ContestedSpaceParameters.CompetitiveReachMeters, Intervals = exposure,
            physical.FrameCoverageGaps, physical.Work, ObservationMilliseconds = exposureTimer.Elapsed.TotalMilliseconds },
    };
}

static double? Arrival(IEnumerable<ResolvedRiderMotion> motions, double boundary)
    => motions.SelectMany(m => m.Nodes.Where(n => n.CanonicalProgress == boundary).Select(n => (double)m.StartElapsedTimeSeconds + n.LocalTimeSeconds)).Cast<double?>().FirstOrDefault();
static float? SpeedAt(IEnumerable<ResolvedRiderMotion> motions, double time)
{
    foreach (var m in motions)
    { var local = time - m.StartElapsedTimeSeconds;
        if (local >= 0 && local <= m.TotalTimeSeconds) return m.SampleAtTime((float)local).SpeedMetersPerSecond; }
    return null;
}
static object AccelerationRange(IEnumerable<double> values) => new { Min = values.Min(), Max = values.Max() };
internal static class PairOwnership
{
internal static object[] States(ResolvedSimulationStep step)
{
    var target = step.CommitInteractionState?.Target;
    if (target is null) return Array.Empty<object>();
    return target.GetType().GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
        .Where(f => f.FieldType == typeof(InteractionEpisodeTracker)).Select(f => new
        {
            Role = f.Name, Pairs = ((InteractionEpisodeTracker)f.GetValue(target)!).PhysicalPairs.OrderBy(p => p.Key).Select(p =>
                new { p.Key.A, p.Key.B, p.Value.Generation, p.Value.Consumed, p.Value.LastImpactSeconds,
                    p.Value.ObservedUntilSeconds, p.Value.ClearSinceSeconds, p.Value.ArmedAtSeconds }).ToArray(),
        }).Cast<object>().ToArray();
}
}

internal sealed class Collector : ISimulationStepObserver
{
    internal CalibrationTraceCollector Calibration { get; }
    internal List<ResolvedSimulationStep> Steps { get; } = new();
    internal List<object> Generations { get; } = new();
    internal Collector(Track track, HeatSimulationOptions options) => Calibration = new(track, options, 91);
    public void OnStepResolved(ResolvedSimulationStep step)
    {
        Calibration.OnStepResolved(step); Steps.Add(step);
        Generations.Add(new { Step = step.Snapshot.Step.StepNumber, States = PairOwnership.States(step) });
    }
}
internal sealed class RunEvidence
{
    public string Id { get; init; } = "";
    public int Seed { get; init; }
    public string Configuration { get; init; } = "";
    public string Population { get; init; } = "";
    public string Description { get; init; } = "";
    public string Track { get; init; } = "";
    public bool Sweep { get; init; }
    public bool Failed { get; init; }
    public object? Failure { get; init; }
    public object? Geometry { get; init; }
    public object? Options { get; init; }
    public object? Initial { get; init; }
    public string FinalHash { get; init; } = "";
    public string BehaviorHash { get; init; } = "";
    public bool ReversedExact { get; set; }
    public bool? ObserverExact { get; set; }
    public double Milliseconds { get; init; }
    public long AllocatedBytes { get; init; }
    public IReadOnlyList<CalibrationRiderSummary> Riders { get; init; } = Array.Empty<CalibrationRiderSummary>();
    public IReadOnlyList<CalibrationLapSummary> Laps { get; init; } = Array.Empty<CalibrationLapSummary>();
    public object? Classification { get; init; }
    public OrderAudit Order { get; init; } = new(Array.Empty<VerifiedPass>(), 0, 0);
    public object? BoundaryOrders { get; init; }
    public object? Starts { get; init; }
    public object? Segments { get; init; }
    public IReadOnlyList<object> Incidents { get; init; } = Array.Empty<object>();
    public object? Events { get; init; }
    public object? Episodes { get; init; }
    public IReadOnlyList<object> Applied { get; init; } = Array.Empty<object>();
    public object? Pairs { get; init; }
    public object? Recoveries { get; init; }
    public object? Generations { get; init; }
    public object? Work { get; init; }
    public object? Exposure { get; init; }
    public int Suppressions { get; init; }
    public object? UncertifiedFootprints { get; init; }
    public object? Violations { get; init; }
}
