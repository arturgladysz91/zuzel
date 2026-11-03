using System.Diagnostics;
using System.Numerics;
using CoreSim;
using CoreSim.PhysicalSpace;
using Safety153;
using SharedWorldSpeedway;

namespace LearnedCorner166;

internal sealed record Scenario(string Id, string Gate, double Start, float Offset, string? Reference161 = null)
{
    public StartingGate Field => Enum.Parse<StartingGate>(Gate);

    public RiderSpec Spec => NativeWorld166.Rider(Field) with
    {
        ReferenceMeters = Start, OffsetMeters = Offset, TargetOffsetMeters = Offset
    };
}

internal sealed record Cross(double Seconds, double Kph, double X, double Y, double Distance);
internal sealed record Outcome(string Id, Scenario Scenario, bool Legal, bool TerminalScreen, bool ReservePass,
    string Reason, double CpuSeconds, double EndReference, double EndSeconds, double MinPose, double? MinSweep,
    double MaxBetaDegrees, double MaxYaw, double MaxSlip, double? FirstBadTime, double? FirstBadReference,
    Cross? Entry, Cross? Apex, Cross? Exit, Cross? After20, Cross? After40, Cross? After62,
    double? MinBendKph, double? SectionSeconds, double? BendSeconds, double? TerminalBetaDegrees,
    double? TerminalBetaDotDegreesPerSecond, double? TerminalOutwardMetersPerSecond, double? TerminalYaw,
    double? TerminalPose, double? TerminalWheelSlip, long ForecastContactQueries, string Fingerprint);

internal sealed class Runner(TrackFrame frame, Teacher a, Teacher b)
{
    public readonly double Entry = frame.SegmentStartMeters(1);
    public readonly double Exit = frame.SegmentStartMeters(4);
    public readonly double Goal = frame.SegmentStartMeters(5);
    private readonly RacingLineGeometry geometry = new(frame);

    public (Run144 Run, Outcome Result) Execute(string id, Scenario scenario, Weights w, Action guard,
        bool verify = false, bool perturb = false, bool pilot = false)
    {
        var clock = Process.GetCurrentProcess();
        var start = clock.TotalProcessorTime.TotalSeconds;
        Policy? policy = null;
        bool displaced = false;
        Decision144 Decide(FreeRacingInput input, CrankClutch144 crank, RiderSpec spec, TrackFrame track,
            TrackStateSnapshot surface, WorldParameters parameters, float mass, float inertia, string variant)
        {
            guard();
            policy ??= new(a, b, w, spec, track, surface, parameters, mass, inertia, pilot);
            var action = policy.Choose(input, crank);
            if (perturb && !displaced && input.Rider.TotalReferenceMeters >= Entry + (Exit - Entry) * .46)
            {
                // One physical steering command goes through the same pending queue and actuator.
                var limit = TwoContactBikeParameters.Default.MaximumSteeringRadians;
                action = action with { SteeringRadians = Math.Clamp(action.SteeringRadians + .014f, -limit, limit) };
                displaced = true;
            }
            action.Validate();
            return new(new(input.Time, action, FreeActionSafety.SafeInHorizon, false, [], 0, 0), 0);
        }

        using var world = new NativeWorld166(new(Enabled: true, RequiredLaps: 1, MaximumSeconds: 27,
            SoloGate: scenario.Field), diagnosticFrame: frame, diagnosticRider: scenario.Spec,
            safetyHalfMeter: true, stopReference: Goal, postSeconds: verify ? .9 : 0);
        var run = world.Run(id, "K1", Decide);
        var result = Analyze(run, scenario, verify, policy?.ForecastContactQueries ?? 0,
            clock.TotalProcessorTime.TotalSeconds - start);
        return (run, result with { CpuSeconds = clock.TotalProcessorTime.TotalSeconds - start });
    }

    public Outcome Analyze(Run144 run, Scenario scenario, bool independent, long queries = 0, double cpu = 0)
    {
        var rider = run.Riders.Single();
        var samples = rider.Samples;
        var goalIndex = Array.FindIndex(samples, q => Ref(q) >= Goal);
        var endIndex = goalIndex >= 0 ? goalIndex : samples.Length - 1;
        var end = samples[endIndex];
        var previous = samples[Math.Max(0, endIndex - 1)];
        var core = samples.Take(endIndex + 1).ToArray();
        var (pose, swept, bad, badReason) = Audit(samples, independent);
        var start = At(samples, Entry - 15);
        var entry = At(core, Entry);
        var exit = At(core, Exit);
        var goal = At(core, Goal);
        var bend = core.Where(q => Ref(q) >= Entry && Ref(q) <= Exit).ToArray();
        var minimum = exit is null || entry is null ? null : (double?)bend.Select(q => q.Input.Rider.Speed * 3.6)
            .Append(entry.Kph).Append(exit.Kph).Min();
        var apex = exit is null ? null : bend.Where(q => Ref(q) >= Entry + (Exit - Entry) * .1 &&
            Ref(q) <= Exit - (Exit - Entry) * .1).MinBy(q => q.Input.Rider.Road.OffsetMeters);
        var body = end.Input.Rider;
        var dt = end.Input.Time - previous.Input.Time;
        var beta = body.SlideRadians * 180 / Math.PI;
        var betaDot = dt <= 0 ? 0 : BikeAngles.Wrap(body.SlideRadians - previous.Input.Rider.SlideRadians)
            * 180 / Math.PI / dt;
        var outward = Vector2.Dot(body.Velocity, body.Road.Outward);
        var endingGap = Math.Min(end.Inner, end.Outer);
        bool continuous = run.Contacts.Length == 0 && samples[0].Input.Rider.Speed == 0;
        bool reaches = goal is not null && continuous && (bad is null || bad.Input.Time > end.Input.Time);
        bool screen = reaches && endingGap >= 1.25 && Math.Abs(beta) < 20 && Math.Abs(outward) < 3
            && Math.Abs(body.AngularVelocity) < 1.25 && Math.Abs(betaDot) < 95;
        bool reserve = independent && reaches && bad is null && run.Outcome == "experiment timeout"
            && samples[^1].Input.Time - end.Input.Time >= .9 - 1e-8;
        string reason = badReason ?? (reaches && run.Outcome == "experiment timeout" ? "reached goal" : run.Outcome);
        return new(run.Name, scenario, reaches, screen, reserve, reason, cpu, Ref(samples[^1]),
            samples[^1].Input.Time, pose, swept, samples.Max(q => Math.Abs(q.Input.Rider.SlideRadians)) * 180 / Math.PI,
            samples.Max(q => Math.Abs(q.Input.Rider.AngularVelocity)),
            samples.Max(q => Math.Abs(q.Powertrain?.Wheel.InstantSlipRatio ?? 0)),
            bad?.Input.Time, bad is null ? null : Ref(bad), entry, apex is null ? null : AsCross(apex), exit,
            At(core, Exit + 20), At(core, Exit + 40), goal, minimum, goal?.Seconds - start?.Seconds,
            exit?.Seconds - entry?.Seconds, reaches ? beta : null, reaches ? betaDot : null,
            reaches ? outward : null, reaches ? body.AngularVelocity : null, reaches ? endingGap : null,
            reaches ? end.Powertrain?.Wheel.InstantSlipRatio : null, queries, run.Fingerprint);
    }

    private (double Pose, double? Swept, StartSample144? Bad, string? Reason) Audit(
        StartSample144[] samples, bool independent)
    {
        StartSample144? bad = null;
        string? reason = null;
        var first = samples[0];
        var previous = independent ? geometry.Clearance(first.Input.Rider) : (first.Inner, first.Outer);
        var pose = Math.Min(previous.Item1, previous.Item2);
        if (pose < .5) { bad = first; reason = "initial footprint"; }
        double? swept = null;
        for (int i = 1; i < samples.Length; i++)
        {
            var a = samples[i - 1];
            var b = samples[i];
            var gap = independent ? geometry.Clearance(b.Input.Rider) : (b.Inner, b.Outer);
            double dt = b.Input.Time - a.Input.Time;
            var bound = independent
                ? Clearance153.SweepLowerBound(a.Input.Rider, b.Input.Rider, dt, previous, gap)
                : b.Certificate;
            previous = gap;
            double local = Math.Min(gap.Item1, gap.Item2);
            pose = Math.Min(pose, local);
            if (bound.HasValue) swept = Math.Min(swept ?? bound.Value, bound.Value);
            if (bad is not null) continue;
            reason = local < .5 ? "pose below 0.5 m" :
                bound is null or < .5 ? "swept below 0.5 m" :
                Math.Abs(b.Input.Rider.SlideRadians) > Math.PI / 4 ? "beta exceeds 45 degrees" :
                b.Forces is { } f && !FreeLearningExperiment.SteeringStepIsPhysical(f, (float)dt) ? "actuator" : null;
            if (reason is not null) bad = b;
        }
        return (pose, swept, bad, reason);
    }

    public static double Ref(StartSample144 sample) => sample.Input.Rider.TotalReferenceMeters;
    private static Cross AsCross(StartSample144 q) => new(q.Input.Time, q.Input.Rider.Speed * 3.6,
        q.Input.Rider.Position.X, q.Input.Rider.Position.Y, q.Input.Rider.DistanceMeters);

    public static Cross? At(StartSample144[] samples, double target)
    {
        for (int i = 1; i < samples.Length; i++)
        {
            var lo = Ref(samples[i - 1]);
            var hi = Ref(samples[i]);
            if (lo >= target || hi < target) continue;
            var a = AsCross(samples[i - 1]);
            var b = AsCross(samples[i]);
            var u = (target - lo) / (hi - lo);
            double Mix(double p, double q) => p + (q - p) * u;
            return new(Mix(a.Seconds, b.Seconds), Mix(a.Kph, b.Kph), Mix(a.X, b.X),
                Mix(a.Y, b.Y), Mix(a.Distance, b.Distance));
        }
        return null;
    }
}
