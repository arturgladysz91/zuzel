using System.Numerics;
using CoreSim.PhysicalSpace;
using LearnedCorner166;
using Safety153;
using SharedWorldSpeedway;
using Scenario = LearnedCorner166.Scenario;

namespace FastTwoCorners167;

internal sealed record Bend(double Entry, double Exit);
internal sealed record BendResult(Cross? Entry, Cross? Apex, Cross? Exit, double? MinimumKph)
{
    public double? Seconds => Exit?.Seconds - Entry?.Seconds;
}

internal sealed record Terminal(double BetaDegrees, double BetaDotDegreesPerSecond, double Yaw,
    double ForwardMetersPerSecond, double LateralMetersPerSecond, double OutwardMetersPerSecond,
    double Inner, double Outer, object Input, object? Powertrain, object? Forces);

internal sealed record Result(string Id, Scenario Start, BendResult First, BendResult Second, double? StraightMaximumKph,
    Cross? Goal, bool Legal, bool ReservePass, double ReserveSeconds, string Reason, double? BadReference,
    double? BadSeconds, double EndReference, double MinimumPose, double? MinimumSwept, double MaximumBetaDegrees,
    Terminal? AtGoal, double CpuSeconds, double DecisionCpuSeconds, int Decisions, long ForecastContactQueries,
    string Fingerprint)
{
    public double? DecisionWallSeconds { get; init; }
    public bool Pass => Legal && ReservePass;
}

internal sealed class Course(TrackFrame frame)
{
    public Bend First { get; } = new(frame.SegmentStartMeters(1), frame.SegmentStartMeters(4));
    public Bend Second { get; } = new(frame.SegmentStartMeters(5), frame.SegmentStartMeters(8));
    public double Goal => Second.Exit + 20;
    public const double Reserve = 1;
    public const double ClearanceMeters = .5;
    public const double MaximumSeconds = 35;

    public Result Inspect(Run144 run, Scenario scenario, bool independent, long forecasts, double cpu)
    {
        var rider = run.Riders.Single();
        var samples = rider.Samples;
        var goalIndex = Array.FindIndex(samples, q => Runner.Ref(q) >= Goal);
        var goal = Runner.At(samples, Goal);
        var geometry = independent ? new RacingLineGeometry(frame) : null;
        (double Inner, double Outer) Gap(StartSample144 q) =>
            geometry?.Clearance(q.Input.Rider) ?? (q.Inner, q.Outer);
        var previous = Gap(samples[0]);
        double pose = Math.Min(previous.Inner, previous.Outer);
        double? swept = null;
        double? badTime = pose < ClearanceMeters ? samples[0].Input.Time : null;
        double? badReference = badTime.HasValue ? Runner.Ref(samples[0]) : null;
        string? reason = badTime.HasValue ? "initial footprint below 0.5 m" : null;

        for (int i = 1; i < samples.Length; i++)
        {
            var a = samples[i - 1];
            var b = samples[i];
            var gap = Gap(b);
            double dt = b.Input.Time - a.Input.Time;
            double? bound = independent
                ? Clearance153.SweepLowerBound(a.Input.Rider, b.Input.Rider, dt, previous, gap)
                : b.Certificate;
            previous = gap;
            double local = Math.Min(gap.Inner, gap.Outer);
            pose = Math.Min(pose, local);
            if (bound is double value) swept = Math.Min(swept ?? value, value);
            if (reason is not null) continue;
            reason = local < ClearanceMeters ? "pose below 0.5 m" :
                bound is null or < ClearanceMeters ? "swept below 0.5 m" :
                Math.Abs(b.Input.Rider.SlideRadians) > Math.PI / 4 ? "beta exceeds 45 degrees" :
                b.Forces is { } f && !FreeLearningExperiment.SteeringStepIsPhysical(f, (float)dt)
                    ? "actuator invariant" : null;
            if (reason is not null)
            {
                badTime = b.Input.Time;
                badReference = Runner.Ref(b);
            }
        }

        var end = samples[^1];
        if (reason is null && run.Outcome != "experiment timeout")
        {
            reason = run.Outcome;
            badTime = end.Input.Time;
            badReference = Runner.Ref(end);
        }
        if (run.Contacts.Length > 0 && (badTime is null || run.Contacts[0].TimeSeconds < badTime))
        {
            reason = "native contact";
            badTime = run.Contacts[0].TimeSeconds;
            badReference = Runner.Ref(samples.First(q => q.Input.Time >= badTime));
        }

        var goalSample = goalIndex < 0 ? null : samples[goalIndex];
        bool legal = goalSample is not null && samples[0].Input.Rider.Speed == 0 &&
            (badTime is null || badTime > goalSample.Input.Time);
        double reserveSeconds = goalSample is null ? 0 : end.Input.Time - goalSample.Input.Time;
        bool reserve = legal && reason is null && reserveSeconds >= Reserve - 1e-8 &&
            rider.Decisions.Any(d => d.Choice.Time > goalSample!.Input.Time);

        Terminal? terminal = null;
        if (goalSample is not null && goalIndex > 0)
        {
            var body = goalSample.Input.Rider;
            var prior = samples[goalIndex - 1].Input;
            var dt = goalSample.Input.Time - prior.Time;
            var betaDot = BikeAngles.Wrap(body.SlideRadians - prior.Rider.SlideRadians) / dt;
            var gap = Gap(goalSample);
            var lateral = Vector2.Dot(body.Velocity, new(-MathF.Sin(body.Heading), MathF.Cos(body.Heading)));
            terminal = new(body.SlideRadians * 180 / Math.PI, betaDot * 180 / Math.PI, body.AngularVelocity,
                body.ForwardSpeed, lateral, Vector2.Dot(body.Velocity, body.Road.Outward), gap.Inner, gap.Outer,
                goalSample.Input, goalSample.Powertrain, goalSample.Forces);
        }

        var first = Measure(samples, First);
        var second = Measure(samples, Second);
        var straight = samples.Where(q => Runner.Ref(q) >= First.Exit && Runner.Ref(q) <= Second.Entry)
            .Select(q => (double?)q.Input.Rider.Speed * 3.6).Append(first.Exit?.Kph).Append(second.Entry?.Kph).Max();
        return new(run.Name, scenario, first, second, straight, goal, legal, reserve, reserveSeconds,
            reason ?? (reserve ? "reached goal and reserve" : legal ? "short reserve" : "did not reach goal"),
            badReference, badTime, Runner.Ref(end), pose, swept,
            samples.Max(q => Math.Abs(q.Input.Rider.SlideRadians)) * 180 / Math.PI,
            terminal, cpu, run.ControllerCpuSeconds, rider.Decisions.Length, forecasts, run.Fingerprint);
    }

    private static BendResult Measure(StartSample144[] samples, Bend bend)
    {
        var entry = Runner.At(samples, bend.Entry);
        var exit = Runner.At(samples, bend.Exit);
        var inside = samples.Where(q => Runner.Ref(q) >= bend.Entry && Runner.Ref(q) <= bend.Exit).ToArray();
        var trim = .1 * (bend.Exit - bend.Entry);
        var apex = inside.Where(q => Runner.Ref(q) >= bend.Entry + trim && Runner.Ref(q) <= bend.Exit - trim)
            .MinBy(q => q.Input.Rider.Road.OffsetMeters);
        Cross? measuredApex = apex is null ? null : new(apex.Input.Time, apex.Input.Rider.Speed * 3.6,
            apex.Input.Rider.Position.X, apex.Input.Rider.Position.Y, apex.Input.Rider.DistanceMeters);
        var minimum = inside.Select(q => (double?)q.Input.Rider.Speed * 3.6)
            .Append(entry?.Kph).Append(exit?.Kph).Min();
        return new(entry, measuredApex, exit, minimum);
    }
}
