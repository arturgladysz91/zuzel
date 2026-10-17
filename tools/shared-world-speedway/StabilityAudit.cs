using System.Globalization;
using System.Text.Json;

namespace SharedWorldSpeedway;

public static class StabilityAudit
{
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
