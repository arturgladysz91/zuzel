using System.Diagnostics;
using System.Globalization;
using System.Numerics;
using System.Text.Json;
using CoreSim;

namespace SharedWorldSpeedway;

public sealed record DynamicsCase(Scenario Scenario, ControlCommand[] Controls);

/// <summary>Offline observation of the real world runner. No alternative motion integrator.</summary>
public static class DynamicsAudit
{
    public static IReadOnlyList<DynamicsCase> Cases()
    {
        var heat = Scenarios.A(4); var rider = heat.Riders[0] with { OffsetMeters = 3, TargetOffsetMeters = 3 };
        var frame = new TrackFrame(CoreSim.Analysis.MatchedVenueProfiles.CreateMotoarenaStandingStartTrack());
        var grip = TrackState.CreateDefault(frame.Track).Snapshot().SampleSurface(1, 1);
        var safe = SegmentPhysics.MaxSafeTurnSpeedForRadius(frame.Track.Geometry.InnerRadiusMeters + 3,
            grip, rider.Profile.Skills, rider.Setup);
        return new[] {
            new DynamicsCase(new("A-straight", new[]{rider}, 2.4), Array.Empty<ControlCommand>()),
            new DynamicsCase(new("B-arc-18", new[]{rider with {ReferenceMeters = 45, SpeedMetersPerSecond = 18}}, 1.5), Array.Empty<ControlCommand>()),
            new DynamicsCase(new("B-arc-capability", new[]{rider with {ReferenceMeters = 45, SpeedMetersPerSecond = safe}}, 1.5), Array.Empty<ControlCommand>()),
            new DynamicsCase(new("C-lap-scripted", new[]{rider}, 60, 1), new[]{new ControlCommand(1, 8.013, 3, .9f), new(1, 8.347, 3, 1)}),
            new DynamicsCase(heat with {Name = "D-heat-scripted"}, heat.Riders.Select(r => new ControlCommand(r.Profile.Id, 0, r.TargetOffsetMeters, 1)).ToArray()),
            new DynamicsCase(heat with {Name = "D-heat-AI", Riders = heat.Riders.Select(r => r with {TrafficEnabled = true}).ToArray()}, Array.Empty<ControlCommand>()),
            new DynamicsCase(Scenarios.DenseStart(), Array.Empty<ControlCommand>())
        };
    }
    public static RunResult Execute(DynamicsCase c, double step, WorldDiagnostics? diagnostics = null,
        Action<WorldSnapshot>? observer = null)
    {
        using var world = new WorldRunner(c.Scenario.Riders, diagnostics: diagnostics, controls: c.Controls);
        return world.Run(c.Scenario.Name, step, c.Scenario.DurationSeconds, c.Scenario.Laps, observer);
    }
    private sealed record Trace(RunResult Result, SortedDictionary<int, WorldSnapshot> States,
        Dictionary<(int Time, int Rider), ForceObservation> Forces, double MaximumGrip, double MaximumOffsetError);
    public static void Run(string directory)
    {
        Directory.CreateDirectory(directory);
        using var comparison = File.CreateText(Path.Combine(directory, "comparison.csv"));
        using var sections = File.CreateText(Path.Combine(directory, "sections.csv"));
        using var windows = File.CreateText(Path.Combine(directory, "windows.csv"));
        using var commands = File.CreateText(Path.Combine(directory, "commands.csv"));
        comparison.WriteLine("scenario,step_s,reference_step_s,aligned_samples,max_position_m,max_vector_velocity_mps,max_speed_mps,max_heading_deg,max_slide_deg,max_finish_s,max_grip_ratio,max_offset_error_m,first_difference_s,fastest_error_growth_s,fastest_error_growth_segment,world_calls");
        sections.WriteLine("scenario,step_s,reference_segment,segment_type,max_position_m,max_velocity_mps,max_heading_deg,max_position_error_growth_mps");
        windows.WriteLine("scenario,step_s,time_s,rider,segment,progress_m,offset_m,vx_mps,vy_mps,speed_mps,heading_rad,spin_radps,slide_rad,drive,target_m,desired_inward_n,applied_inward_n,grip_budget_n,tangential_n");
        commands.WriteLine("scenario,step_s,rider,opponent,action,observed_s,requested_s,activated_s,target_m,drive");
        var results = new List<RunResult>();
        foreach (var c in Cases())
        {
            var traces = new List<Trace>();
            foreach (var step in new[] {.02, .01, .005})
            {
                var states = new SortedDictionary<int, WorldSnapshot>();
                var forces = new Dictionary<(int, int), ForceObservation>();
                double maxGrip = 0, maxOffset = 0;
                var diagnostics = new WorldDiagnostics { Force = f => {
                    if (f.GripBudgetNewtons is > 0) maxGrip = Math.Max(maxGrip, f.ForceNewtons.Length() / f.GripBudgetNewtons.Value);
                    if (Common(f.TimeSeconds)) forces[(Key(f.TimeSeconds), f.Rider.Id)] = f;
                }};
                var result = Execute(c, step, diagnostics, s => {
                    if (Common(s.TimeSeconds)) states[Key(s.TimeSeconds)] = s;
                    foreach (var v in s.Riders) maxOffset = Math.Max(maxOffset, Math.Abs(v.Road.OffsetMeters - v.TargetOffsetMeters));
                });
                results.Add(result); traces.Add(new(result, states, forces, maxGrip, maxOffset));
                foreach (var command in result.Decisions)
                    Row(commands, result.Name, step, command.RiderId, command.OpponentId, command.Action,
                        command.ObservedAt, command.RequestedActivation, command.ActualActivation, command.TargetOffsetMeters, command.Drive);
            }
            var reference = traces[^1]; var windowTimes = new HashSet<int>();
            foreach (var trace in traces)
            {
                double pos = 0, vel = 0, speed = 0, heading = 0, slide = 0, maxGrowth = 0;
                double? first = null, growthTime = null; int? growthSegment = null;
                var bySegment = new Dictionary<int, double[]>(); var previousErrors = new double[c.Scenario.Riders.Length];
                var count = 0;
                foreach (var (key, sample) in trace.States)
                {
                    if (!reference.States.TryGetValue(key, out var other)) continue;
                    count++;
                    for (var i = 0; i < sample.Riders.Count; i++)
                    {
                        var a = sample.Riders[i]; var b = other.Riders[i];
                        var dp = Vector2.Distance(a.Position, b.Position); var dv = Vector2.Distance(a.Velocity, b.Velocity);
                        var dh = Math.Abs(CoreSim.PhysicalSpace.BikeAngles.Wrap(a.Heading - b.Heading)) * 180 / Math.PI;
                        var ds = Math.Abs(CoreSim.PhysicalSpace.BikeAngles.Wrap(a.SlideRadians - b.SlideRadians)) * 180 / Math.PI;
                        pos = Math.Max(pos, dp); vel = Math.Max(vel, dv); speed = Math.Max(speed, Math.Abs(a.Speed - b.Speed));
                        heading = Math.Max(heading, dh); slide = Math.Max(slide, ds);
                        if (first is null && (dp > .01 || dv > .05 || dh > .1)) first = sample.TimeSeconds;
                        var growth = (dp - previousErrors[i]) / .02; previousErrors[i] = dp;
                        if (growth > maxGrowth) { maxGrowth = growth; growthTime = sample.TimeSeconds; growthSegment = b.Road.Segment; }
                        if (!bySegment.TryGetValue(b.Road.Segment, out var maxima)) bySegment[b.Road.Segment] = maxima = new double[4];
                        maxima[0] = Math.Max(maxima[0], dp); maxima[1] = Math.Max(maxima[1], dv);
                        maxima[2] = Math.Max(maxima[2], dh); maxima[3] = Math.Max(maxima[3], growth);
                    }
                }
                foreach (var (segment, maxima) in bySegment.OrderBy(p => p.Key))
                    Row(sections, c.Scenario.Name, trace.Result.StepSeconds, segment,
                        CoreSim.Analysis.MatchedVenueProfiles.CreateMotoarenaStandingStartTrack().Segments[segment].Type,
                        maxima[0], maxima[1], maxima[2], maxima[3]);
                double? finish = trace.Result.FinishTimes.Count == 0 ? null : trace.Result.FinishTimes
                    .Max(p => Math.Abs(p.Value - reference.Result.FinishTimes[p.Key]));
                Row(comparison, c.Scenario.Name, trace.Result.StepSeconds, .005, count, pos, vel, speed, heading, slide,
                    finish, trace.MaximumGrip, trace.MaximumOffsetError, first, growthTime, growthSegment, trace.Result.Work.SolverCalls);
                foreach (var t in new[] {first, growthTime}) if (t is { } time)
                    for (var key = Key(time) - 5; key <= Key(time) + 10; key++) windowTimes.Add(key);
                // Also retain the first actual straight/turn crossing, independent of error threshold.
                var crossing = trace.States.Values.FirstOrDefault(s => s.Riders[0].Road.IsTurn);
                if (crossing is not null && c.Scenario.Riders[0].ReferenceMeters == 0)
                    for (var key = Key(crossing.TimeSeconds) - 5; key <= Key(crossing.TimeSeconds) + 10; key++) windowTimes.Add(key);
            }
            foreach (var trace in traces) foreach (var (key, s) in trace.States)
            {
                if (!windowTimes.Contains(key)) continue;
                foreach (var v in s.Riders)
                {
                    trace.Forces.TryGetValue((key, v.Id), out var f);
                    Row(windows, c.Scenario.Name, trace.Result.StepSeconds, s.TimeSeconds, v.Id, v.Road.Segment,
                        v.TotalReferenceMeters, v.Road.OffsetMeters, v.Velocity.X, v.Velocity.Y, v.Speed, v.Heading,
                        v.AngularVelocity, v.SlideRadians, v.Drive, v.TargetOffsetMeters,
                        f.DesiredInwardNewtons, f.AppliedInwardNewtons, f.GripBudgetNewtons, f.TangentialNewtons);
                }
            }
        }
        File.WriteAllText(Path.Combine(directory, "runs.json"), JsonSerializer.Serialize(results, Audit.Json));
        Console.WriteLine("Saved 21 physical-time executions and bounded diagnostic windows to " + Path.GetFullPath(directory));
    }
    public static void Benchmark(string directory)
    {
        Directory.CreateDirectory(directory); var rows = new List<object>();
        foreach (var c in Cases()) foreach (var step in new[] {.02, .01, .005})
        {
            Execute(c, step); Execute(c, step);
            using var process = Process.GetCurrentProcess(); var elapsed = new List<double>();
            double cpu = 0; long allocated = 0; RunResult result = null!;
            const int samples = 5, batch = 2;
            for (var sample = 0; sample < samples; sample++)
            {
                var allocation = GC.GetAllocatedBytesForCurrentThread(); var startCpu = process.TotalProcessorTime;
                var start = Stopwatch.GetTimestamp();
                for (var n = 0; n < batch; n++) result = Execute(c, step);
                elapsed.Add(Stopwatch.GetElapsedTime(start).TotalMilliseconds / batch);
                cpu += (process.TotalProcessorTime - startCpu).TotalMilliseconds;
                allocated += GC.GetAllocatedBytesForCurrentThread() - allocation;
            }
            elapsed.Sort(); rows.Add(new {c.Scenario.Name, StepSeconds = step, ElapsedMilliseconds = elapsed[samples / 2],
                CpuMilliseconds = cpu / (samples * batch), AllocatedBytes = allocated / (samples * batch), Samples = samples,
                Batch = batch, result.TimeSeconds, result.Work, result.Fingerprint});
        }
        File.WriteAllText(Path.Combine(directory, "performance.json"), JsonSerializer.Serialize(rows, Audit.Json));
    }
    private static bool Common(double t) => Math.Abs(t / .02 - Math.Round(t / .02)) < 1e-8;
    private static int Key(double t) => (int)Math.Round(t / .02);
    private static void Row(TextWriter output, params object?[] values) => output.WriteLine(string.Join(",",
        values.Select(v => v is IFormattable f ? f.ToString(null, CultureInfo.InvariantCulture) : v?.ToString() ?? "")));
}
