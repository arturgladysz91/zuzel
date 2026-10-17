using System.Globalization;
using System.Text.Json;

namespace SharedWorldSpeedway;

public static class StabilityAudit
{
    public static IReadOnlyList<Scenario> Cases()
    {
        var dense = Scenarios.DenseStart(); var bend = Scenarios.D();
        return new[] { Scenarios.A(4), Scenarios.C(), Scenarios.Support(), bend, dense,
            dense with { Name = "dense-no-traffic-response", Riders = dense.Riders.Select(r => r with { TrafficEnabled = false }).ToArray() },
            Density(.98f), Density(1.02f), CrossSpeed(-.05f), CrossSpeed(.05f), BendSpeed(.95f), BendSpeed(1.05f) };
        Scenario Density(float factor) => dense with { Name = "dense-spacing-" + factor.ToString("F2", CultureInfo.InvariantCulture),
            Riders = dense.Riders.Select(r => r with { OffsetMeters = 4 + (r.OffsetMeters - 4) * factor }).ToArray() };
        Scenario CrossSpeed(float delta) => dense with { Name = "dense-cross-speed-" + delta.ToString("F2", CultureInfo.InvariantCulture),
            Riders = dense.Riders.Select(r => r with { InitialOutwardSpeed = r.Profile.Id % 2 == 1 ? delta : -delta }).ToArray() };
        Scenario BendSpeed(float factor) => bend with { Name = "bend-closing-" + factor.ToString("F2", CultureInfo.InvariantCulture),
            Riders = bend.Riders.Select((r, i) => i == 0 ? r with { InitialOutwardSpeed = r.InitialOutwardSpeed * factor } : r).ToArray() };
    }
    public static void Validate(string directory)
    {
        Directory.CreateDirectory(directory);
        using var comparison = File.CreateText(Path.Combine(directory, "comparison.csv"));
        using var force = File.CreateText(Path.Combine(directory, "force-budgets.csv"));
        using var context = File.CreateText(Path.Combine(directory, "contexts.csv"));
        using var point = File.CreateText(Path.Combine(directory, "first-impact-points.csv"));
        comparison.WriteLine("scenario,step_s,reference_step_s,aligned_samples,max_position_difference_m,max_velocity_difference_mps,max_heading_difference_rad,pre_contact_position_difference_m,pre_contact_velocity_difference_mps,max_finish_difference_s,first_contact_s,reference_first_contact_s,first_ratio_a,reference_first_ratio_a");
        force.WriteLine("scenario,step_s,max_force_to_grip_ratio");
        context.WriteLine("scenario,step_s,time_s,rider,context,segment,reference_progress_m");
        point.WriteLine("scenario,step_s,time_s,rider_a,rider_b,closing_mps,impulse_ns,persisted");
        var results = new List<RunResult>();
        foreach (var scenario in Cases())
        {
            var runs = new List<(RunResult Result, SortedDictionary<long, WorldSnapshot> Samples)>();
            foreach (var step in new[] { .02, .01, .005 })
            {
                var samples = new SortedDictionary<long, WorldSnapshot>();
                var phases = new Dictionary<int, RacingContext>();
                double maxForceRatio = 0; double? firstImpact = null;
                var diagnostics = new WorldDiagnostics {
                    Force = o => { if (o.GripBudgetNewtons is > 0)
                        maxForceRatio = Math.Max(maxForceRatio, o.ForceNewtons.Length() / o.GripBudgetNewtons.Value); },
                    ContactPoint = o => {
                        if (o.TotalNormalImpulseNewtonSeconds <= 0) return;
                        firstImpact ??= o.TimeSeconds;
                        if (o.TimeSeconds <= firstImpact + .04)
                            Row(point, scenario.Name, step, o.TimeSeconds, o.RiderA, o.RiderB,
                                -o.NormalVelocityMetersPerSecond, o.TotalNormalImpulseNewtonSeconds, o.Persisted);
                    }
                };
                using var world = new WorldRunner(scenario.Riders, diagnostics: diagnostics);
                var result = world.Run(scenario.Name, step, scenario.DurationSeconds, scenario.Laps, s => {
                    if (CommonTime(s.TimeSeconds)) samples[(long)Math.Round(s.TimeSeconds * 1e6)] = s;
                    foreach (var v in s.Riders)
                    {
                        if (!phases.TryGetValue(v.Id, out var last) || last != v.Context)
                            Row(context, scenario.Name, step, s.TimeSeconds, v.Id, v.Context, v.Road.Segment, v.TotalReferenceMeters);
                        phases[v.Id] = v.Context;
                    }
                });
                results.Add(result); runs.Add((result, samples));
                Row(force, scenario.Name, step, maxForceRatio);
            }
            var reference = runs[^1];
            foreach (var run in runs.Take(2))
            {
                var touch = run.Result.Contacts.FirstOrDefault(c => c.Kind == "begin");
                var refTouch = reference.Result.Contacts.FirstOrDefault(c => c.Kind == "begin");
                var before = Math.Min(touch?.TimeSeconds ?? double.PositiveInfinity, refTouch?.TimeSeconds ?? double.PositiveInfinity);
                double pos = 0, vel = 0, heading = 0, prePos = 0, preVel = 0;
                var count = 0;
                foreach (var (key, sample) in run.Samples)
                {
                    if (!reference.Samples.TryGetValue(key, out var other)) continue;
                    count++;
                    for (var i = 0; i < sample.Riders.Count; i++)
                    {
                        var a = sample.Riders[i]; var b = other.Riders[i];
                        var dp = System.Numerics.Vector2.Distance(a.Position, b.Position);
                        var dv = System.Numerics.Vector2.Distance(a.Velocity, b.Velocity);
                        pos = Math.Max(pos, dp); vel = Math.Max(vel, dv);
                        heading = Math.Max(heading, Math.Abs(CoreSim.PhysicalSpace.BikeAngles.Wrap(a.Heading - b.Heading)));
                        if (sample.TimeSeconds < before) { prePos = Math.Max(prePos, dp); preVel = Math.Max(preVel, dv); }
                    }
                }
                var finishDifference = run.Result.FinishTimes.Count == 0 ? (double?)null : run.Result.FinishTimes
                    .Max(pair => Math.Abs(pair.Value - reference.Result.FinishTimes[pair.Key]));
                Row(comparison, scenario.Name, run.Result.StepSeconds, reference.Result.StepSeconds, count,
                    pos, vel, heading, prePos, preVel, finishDifference, touch?.TimeSeconds, refTouch?.TimeSeconds,
                    touch?.SeverityRatioA, refTouch?.SeverityRatioA);
            }
        }
        File.WriteAllText(Path.Combine(directory, "scenarios.json"), JsonSerializer.Serialize(results, Audit.Json));
        Console.WriteLine("Saved 36 targeted executions and aligned physical-time comparisons to " + Path.GetFullPath(directory));
    }
    // Deliberately bounded evidence: first control divergence and first impact only.
    public static void Diagnose(string directory)
    {
        Directory.CreateDirectory(directory);
        using var state = File.CreateText(Path.Combine(directory, "common-time.csv"));
        using var force = File.CreateText(Path.Combine(directory, "forces.csv"));
        using var point = File.CreateText(Path.Combine(directory, "contact-points.csv"));
        using var consequence = File.CreateText(Path.Combine(directory, "consequences.csv"));
        state.WriteLine("step_s,time_s,rider,x_m,y_m,vx_mps,vy_mps,heading_rad,spin_radps,slide_rad,drive,target_m,status");
        force.WriteLine("step_s,time_s,rider,desired_inward_n,applied_inward_n,grip_budget_n,tangential_n,fx_n,fy_n,torque_nm,phase");
        point.WriteLine("step_s,time_s,local_step_s,rider_a,rider_b,component_a,component_b,point_x_m,point_y_m,normal_x,normal_y,separation_m,normal_velocity_mps,pre_step_point_normal_velocity_mps,normal_impulse_ns,total_normal_impulse_ns,tangent_impulse_ns,persisted");
        consequence.WriteLine("step_s,time_s,rider,opponent,closing_mps,first_step_impulse_ns,reserve_mps,severity_ratio,severity");
        var summaries = new List<RunResult>();
        foreach (var step in new[] { .02, .01, .005 })
        {
            var diagnostics = new WorldDiagnostics
            {
                Force = o => {
                    if (Window(o.TimeSeconds) && CommonTime(o.TimeSeconds))
                        Row(force, step, o.TimeSeconds, o.Rider.Id, o.DesiredInwardNewtons, o.AppliedInwardNewtons,
                            o.GripBudgetNewtons, o.TangentialNewtons, o.ForceNewtons.X, o.ForceNewtons.Y, o.TorqueNewtonMeters, o.Phase);
                },
                ContactPoint = o => {
                    if (o.TimeSeconds < .70 || o.TimeSeconds > 1.08) return;
                    Row(point, step, o.TimeSeconds, o.StepSeconds, o.RiderA, o.RiderB, o.ComponentA, o.ComponentB,
                        o.PointMeters.X, o.PointMeters.Y, o.NormalAtoB.X, o.NormalAtoB.Y, o.SeparationMeters,
                        o.NormalVelocityMetersPerSecond, o.PreStepPointNormalVelocityMetersPerSecond,
                        o.NormalImpulseNewtonSeconds, o.TotalNormalImpulseNewtonSeconds, o.TangentImpulseNewtonSeconds, o.Persisted);
                },
                Consequence = o => {
                    if (o.TimeSeconds <= 1.1) Row(consequence, step, o.TimeSeconds, o.RiderId, o.OpponentId,
                        o.ClosingSpeed, o.ImpulseNewtonSeconds, o.Reserve, o.SeverityRatio, o.Severity);
                }
            };
            var scenario = Scenarios.DenseStart(); using var world = new WorldRunner(scenario.Riders, diagnostics: diagnostics);
            summaries.Add(world.Run(scenario.Name, step, scenario.DurationSeconds, acceptedObserver: s => {
                if (!Window(s.TimeSeconds) || !CommonTime(s.TimeSeconds)) return;
                foreach (var v in s.Riders)
                    Row(state, step, s.TimeSeconds, v.Id, v.Position.X, v.Position.Y, v.Velocity.X, v.Velocity.Y,
                        v.Heading, v.AngularVelocity, v.SlideRadians, v.Drive, v.TargetOffsetMeters, v.Activity);
            }));
        }
        File.WriteAllText(Path.Combine(directory, "summary.json"), JsonSerializer.Serialize(summaries, Audit.Json));
        Console.WriteLine("Saved bounded force/contact diagnosis to " + Path.GetFullPath(directory));
    }
    private static bool Window(double t) => t >= .23999999 && t <= .42000001 || t >= .69999999 && t <= 1.08000001;
    private static bool CommonTime(double t) => Math.Abs(t / .02 - Math.Round(t / .02)) < 1e-8;
    private static void Row(TextWriter output, params object?[] values)
        => output.WriteLine(string.Join(",", values.Select(v => v is IFormattable f ? f.ToString(null, CultureInfo.InvariantCulture) : v?.ToString() ?? "")));
}
