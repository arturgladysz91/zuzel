using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text.Json;
using CoreSim;
using CoreSim.Analysis;
using CoreSim.Decisions;
using CoreSim.Logging;
using CoreSim.Race;
using RaceReadiness;
using TrajectoryIntent = CoreSim.Decisions.TrajectoryIntent;

CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
var mode = args.ElementAtOrDefault(0) ?? "capture";
var output = args.ElementAtOrDefault(1) ?? "results/rea009/physical.json";
Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(output))!);
if (mode == "capture")
{
    var rows = new SortedDictionary<string, object>();
    foreach (var fraction in new[] { 0f, .1f, .2f, .4f, .5f, .9f, .125f, .375f, MathF.BitDecrement(1f), 1e-8f })
    foreach (var index in new[] { 0, 1, 2, 3, 8 })
    foreach (var moving in new[] { false, true })
        Capture(fraction, index, moving, false);
    foreach (var fraction in new[] { .1f, .4f })
    foreach (var moving in new[] { false, true }) Capture(fraction, 7, moving, true);
    Write(new { Schema = "rea009-physical-v1", Rows = rows });
    return;
    void Capture(float fraction, int index, bool moving, bool crash)
    {
        var track = Track.CreateStandingStartExample();
        var rider = new RiderState(RiderProfile.CreateDefault(17), crash ? 4 : 2)
        { Speed = crash ? 80f : 18f, ElapsedTimeSeconds = 11f };
        rider.RestorePosition(RiderPosition.Create(4, index, fraction, track.Segments.Count, 100f));
        if (moving) rider.LateralPosition = crash ? 3.99f : 2.21f;
        var surface = TrackState.CreateDefault(track);
        var engine = new SimulationEngine(new Hold());
        var input = engine.CaptureSnapshot(track, surface, [rider], new(91, 0, 3, index, 19, 4));
        var options = new HeatSimulationOptions { IncidentFrequency = 0f };
        var resolved = engine.Resolve(input, engine.Decide(input), options);
        var change = resolved.Changes.Single();
        var lean = SimulationEngine.ResolveSoloProjection(input, input.Riders[0], new(rider.Lane), options);
        if (change != lean.Change) throw new InvalidOperationException("Rich/lean single-segment divergence.");
        engine.Commit(resolved, [rider], surface, new SimLog(false));
        rows.Add($"{BitConverter.SingleToInt32Bits(fraction):X8}/{index}/{moving}/{crash}", Exact.Leaves(new
        {
            Physical = new { change.Lane, change.LateralPosition, change.Speed, change.Risk, change.ElapsedTimeSeconds,
                change.Morale, change.Outcome, change.EntrySpeed, change.PhysicsSpeed, change.ApplySurfaceWear,
                change.Position.DistanceMeters, Diagnostics = resolved.Diagnostics, Surface = surface.Snapshot() },
            Initial = input.Riders[0].Position, Final = change.Position, change.Status, Motion = resolved.Motions.Single(),
        }));
    }
}
if (mode != "benchmark") throw new ArgumentException("capture|benchmark output [samples]");
var samples = int.Parse(args.ElementAtOrDefault(2) ?? "9", CultureInfo.InvariantCulture);
if (samples is < 3 or > 30) throw new ArgumentOutOfRangeException(nameof(samples));
var measurements = new List<object>();
foreach (var name in new[] { "balanced-motoarena", "contact-heavy", "three-squeeze", "three-squeeze-binary", "fixed-line", "rich-trajectory", "lean-cached-trajectory", "decimal-single-segment" })
{
    try
    {
        var startWarmup = Stopwatch.GetTimestamp(); var warmups = 0;
        do { Run(name); warmups++; } while (warmups < 5 || Stopwatch.GetElapsedTime(startWarmup).TotalSeconds < 2);
        var rows = new List<object>();
        for (var sample = 0; sample < samples; sample++)
        {
            var allocated = GC.GetTotalAllocatedBytes(true); var start = Stopwatch.GetTimestamp();
            var work = Run(name);
            rows.Add(new { Milliseconds = Stopwatch.GetElapsedTime(start).TotalMilliseconds,
                AllocatedBytes = GC.GetTotalAllocatedBytes(true) - allocated });
        }
        measurements.Add(new { Name = name, Failed = false, Warmups = warmups, CountedWork = Run(name, count: true), Samples = rows });
    }
    catch (InvalidOperationException exception)
    { measurements.Add(new { Name = name, Failed = true, exception.Message }); }
    Console.WriteLine($"Measured {name}.");
    Save();
}
Save();

Work Run(string name, bool count = false)
{
    var work = new Work();
    var previous = ProjectionCaptureAudit.Observer;
    if (count) ProjectionCaptureAudit.Observer = (kind, n) =>
    {
        if (kind == ProjectionMaterialization.PhysicalEvaluation) work.SynchronousResolutions += n;
        if (kind == ProjectionMaterialization.PrefixCacheHit) work.PrefixCacheHits += n;
    };
    try
    {
        if (name is "rich-trajectory" or "lean-cached-trajectory" or "decimal-single-segment")
        {
            var track = Track.CreateStandingStartExample();
            var rider = new RiderState(RiderProfile.CreateDefault(17), 2) { Speed = 20f, LateralPosition = 2.21f };
            rider.RestorePosition(RiderPosition.Create(1, 0, name == "decimal-single-segment" ? .1f : .125f, track.Segments.Count));
            var engine = new SimulationEngine(new Hold());
            var snapshot = engine.CaptureSnapshot(track, TrackState.CreateDefault(track), [rider], new(91, 0, 0, 0, 19, 4));
            if (name == "decimal-single-segment")
            {
                work.Observe(engine.Resolve(snapshot, engine.Decide(snapshot), new() { IncidentFrequency = 0f }));
                return work;
            }
            var evaluator = new TrajectoryEvaluator(new(snapshot, snapshot.Riders[0]));
            var candidates = TrajectoryCandidates.Generate(SegmentType.Straight);
            for (var repeat = 0; repeat < (name == "lean-cached-trajectory" ? 2 : 1); repeat++)
            foreach (var intent in candidates)
            {
                var result = evaluator.Evaluate(intent, name == "rich-trajectory");
                work.MotionNodes += result.ResolvedMotions.Sum(m => m.Nodes.Count);
            }
            work.ProjectionResolutions = evaluator.ProductionResolutionCount;
            work.CandidateTraversals = evaluator.CandidateTraversalCount;
            return work;
        }
        var id = name == "fixed-line" ? "balanced-motoarena" : name;
        var (heatTrack, surface, riders) = Catalog.All().Single(s => s.Id == id).Create();
        if (name == "fixed-line")
            foreach (var rider in riders) { rider.RestoreStartingPosition(null); rider.Lane = 2; rider.LateralPosition = 2f; rider.Speed = 20f; }
        var model = new Counting(work);
        new HeatSimulator(name == "fixed-line" ? new Hold() : model).SimulateHeat(heatTrack, surface, riders,
            new() { Seed = 19, IncidentFrequency = 0f, EnableLogging = false,
                EnableContestedSpaceResponses = name == "contact-heavy", EnablePhysicalContactConsequences = name == "contact-heavy" },
            91, work);
        return work;
    }
    finally { ProjectionCaptureAudit.Observer = previous; }
}
void Save() => Write(new { Schema = "rea009-benchmark-v1", Samples = samples,
    Environment = new { OS = RuntimeInformation.OSDescription, Runtime = RuntimeInformation.FrameworkDescription, Environment.ProcessorCount },
    Warmup = "At least five runs and two seconds; nine samples by default; process-wide allocations include workers",
    TimedDecisionDegree = Math.Min(4, Environment.ProcessorCount), ProjectionCounterDegree = 1,
    CounterProtocol = "One separate untimed run uses the existing synchronous capture hook; timings retain default parallel scheduling",
    Measurements = measurements });
void Write(object value) => File.WriteAllText(output, JsonSerializer.Serialize(value, new JsonSerializerOptions { WriteIndented = true }) + "\n");
sealed class Hold : IRiderDecisionModel { public RiderDecision Decide(TrackSegment segment, RiderState rider) => new(rider.Lane); }
sealed class Counting(Work work) : IRiderDecisionModel
{
    private readonly AdaptiveDecisionModel model = new();
    public RiderDecision Decide(TrackSegment segment, RiderState rider) => model.Decide(segment, rider);
    public RiderDecision Decide(RiderDecisionContext context)
    {
        var result = model.EvaluateDecision(context);
        work.ProjectionResolutions += result.ProductionResolutions;
        work.CandidateTraversals += result.CandidateTraversals;
        return result.Decision;
    }
}
sealed class Work : ISimulationStepObserver
{
    public int SynchronousResolutions { get; set; }
    public int ProjectionResolutions { get; set; }
    public int CandidateTraversals { get; set; }
    public int PrefixCacheHits { get; set; }
    public int MotionNodes { get; set; }
    public int CommittedSegments { get; set; }
    public int ContactEvaluations { get; set; }
    public void OnStepResolved(ResolvedSimulationStep step) => Observe(step);
    internal void Observe(ResolvedSimulationStep step)
    {
        CommittedSegments += step.Changes.Count;
        MotionNodes += step.Motions.Sum(m => m.Nodes.Count);
        ContactEvaluations += step.Interaction?.Work.NarrowPhaseEvaluations ?? 0;
    }
}
