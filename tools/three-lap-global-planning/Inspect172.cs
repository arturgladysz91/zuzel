using CoreSim.PhysicalSpace;
using LearnedCorner166;
using Safety153;
using SharedWorldSpeedway;
using WholeCorner171;
using Scenario = LearnedCorner166.Scenario;

namespace Cyclic172;

internal sealed record Outcome172(string Id, string Stage, string Controller, Scenario Start, bool ReachedStop,
    bool S3, bool Reserve, string Reason, double Stop, double EndReference, double EndTime, double? FinishTime,
    int Crossings, double MinPose, double MinSweep, double MaxBetaDegrees, double? FirstBadTime,
    double? FirstBadReference, double CpuSeconds, double WorldCpuSeconds, double ControllerCpuSeconds,
    int Decisions, int Plans, int Replans, double PlanCpuSeconds, string Fingerprint)
{
    public int AcceptedBends { get; init; }
}

internal static class Inspect172
{
    public static Outcome172 Analyze(Run144 run, string stage, string controller, Scenario scenario, TrackFrame frame,
        double stop, CyclicDecision172[] decisions, Planning172[] plans, double cpu)
    {
        var geometry = new RacingLineGeometry(frame);
        var rider = run.Riders.Single();
        var samples = rider.Samples;
        var before = geometry.Clearance(samples[0].Input.Rider);
        double pose = Math.Min(before.Inner, before.Outer), sweep = double.PositiveInfinity;
        double? badTime = pose < .5 ? 0 : null, badReference = pose < .5 ? samples[0].Input.Rider.TotalReferenceMeters : null;
        string? reason = pose < .5 ? "initial footprint" : null;
        for (int i = 1; i < samples.Length; i++)
        {
            var a = samples[i - 1];
            var b = samples[i];
            var gap = geometry.Clearance(b.Input.Rider);
            double dt = b.Input.Time - a.Input.Time;
            double swept = Clearance153.SweepLowerBound(a.Input.Rider, b.Input.Rider, dt, before, gap);
            before = gap;
            double local = Math.Min(gap.Inner, gap.Outer);
            pose = Math.Min(pose, local);
            sweep = Math.Min(sweep, swept);
            if (reason is not null) continue;
            reason = local < .5 ? "pose below 0.5 m" : swept < .5 ? "swept below 0.5 m" :
                Math.Abs(b.Input.Rider.SlideRadians) > Math.PI / 4 ? "beta exceeds 45 degrees" :
                b.Forces is { } f && !FreeLearningExperiment.SteeringStepIsPhysical(f, (float)dt) ? "actuator" : null;
            if (reason is not null)
            {
                badTime = b.Input.Time;
                badReference = b.Input.Rider.TotalReferenceMeters;
            }
        }
        if (run.Contacts.Length > 0 && (badTime is null || run.Contacts[0].TimeSeconds < badTime))
        {
            reason = "native contact";
            badTime = run.Contacts[0].TimeSeconds;
            badReference = samples.First(q => q.Input.Time >= badTime).Input.Rider.TotalReferenceMeters;
        }
        if (reason is null && run.Outcome is not ("experiment timeout" or "completed"))
        {
            reason = run.Outcome;
            badTime = samples[^1].Input.Time;
            badReference = samples[^1].Input.Rider.TotalReferenceMeters;
        }
        bool standing = samples[0].Input.Rider.Speed == 0 && samples[0].Input.Omega == 0;
        bool reached = samples[^1].Input.Rider.TotalReferenceMeters >= stop && reason is null && standing;
        double goal = 3 * frame.LapMeters;
        var goalSample = samples.FirstOrDefault(q => q.Input.Rider.TotalReferenceMeters >= goal);
        bool crossings = run.Crossings.Select(q => q.Lap).SequenceEqual(new[] { 1, 2, 3 }) &&
            run.Crossings.All(q => q.BeforeReference < q.Lap * frame.LapMeters && q.AfterReference >= q.Lap * frame.LapMeters);
        bool full = goalSample is not null && rider.FinishSeconds is not null && crossings && standing &&
            (badTime is null || badTime > goalSample.Input.Time);
        bool reserve = full && reason is null && samples[^1].Input.Time - goalSample!.Input.Time >= 1 - 1e-8 &&
            rider.Decisions.Any(d => d.Choice.Time > goalSample.Input.Time) &&
            samples[^1].Input.Rider.Activity == RiderActivity.Racing;
        return new(run.Name, stage, controller, scenario, reached, full, reserve,
            reason ?? (reserve ? "three laps and reserve" : reached ? "integration goal" : "incomplete"),
            stop, samples[^1].Input.Rider.TotalReferenceMeters, samples[^1].Input.Time, rider.FinishSeconds,
            run.Crossings.Length, pose, sweep, samples.Max(q => Math.Abs(q.Input.Rider.SlideRadians)) * 180 / Math.PI,
            badTime, badReference, cpu, run.TotalCpuSeconds, run.ControllerCpuSeconds, decisions.Length,
            plans.Count(p => p.Reason == "approach"), plans.Count(p => p.Reason == "deviation"),
            plans.Sum(p => p.CpuSeconds), run.Fingerprint)
        {
            AcceptedBends = plans.Where(p => p.Chosen is not null && p.Plans[0].Visit.Lap is >= 0 and < 3)
                .Select(p => (p.Plans[0].Visit.Lap, p.Plans[0].Visit.Index)).Distinct().Count()
        };
    }
}

