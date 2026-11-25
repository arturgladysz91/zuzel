
using SharedWorldSpeedway;

namespace SpeedwayHandling;

internal static partial class Study191
{
    private static void StraightEvidence38(Lab38 lab, string output)
    {
        const double comparisonSeconds = 2.10;
        string root = Path.Combine(lab.Root, "data/experiments/speedway218");
        var baseline = Read199<Runout37>(Path.Combine(root, "terminal-80-runout.json.gz"));
        var b = ReadArchive38(lab, "78");
        double location = b.Distances[^1], prior = lab.Course.Mark(b.Run.Trace[^1].After, b.Geometry).Road.ReferenceMeters;
        double? baselineTwenty = null;
        foreach (var row in baseline.Trace)
        {
            var after = lab.Course.Mark(row.After, b.Geometry);
            location += Frame.UnwrapDelta(prior, after.Road.ReferenceMeters);
            prior = after.Road.ReferenceMeters;
            if (baselineTwenty is null && location >= lab.Bend.Length + 20)
                baselineTwenty = Speed38(row.After, b.Geometry);
        }

        var g = lab.Geometry(true);
        var origin = SteadyCircle215(lab.Engine(g), g, lab.Fixture.Rest(g, NominalSupport216), 40 / 3.6, 0,
            lab.Fixture.Release + 1, lab.Fixture.Ratio, balanceRider: true);
        var parent = ReadPlain214<StageTrials37>(Path.Combine(root, "stage-80.json")).Outcomes.Single(x => x.Setup.Id == 30).Setup;
        var results = new List<object>();
        foreach (var name in new[] { "nominal", "holdout-1", "holdout-2", "holdout-3", "holdout-4" })
        {
            var priorRun = Read199<FullLog38>(Path.Combine(output, $"recourse-{name}.json.gz"));
            var marker = priorRun.States.FirstOrDefault(s => s.Distance >= lab.Bend.Length + 8);
            var final = priorRun.States.LastOrDefault();
            if (marker is null || final is null || final.Distance < lab.Bend.Length + 20)
            {
                results.Add(new { Name = name, Complete = false, Reason = "No valid physical +20 m state" });
                continue;
            }
            double initialEdge = H36Footprint.Clearance(lab.Track, g, final.State, false);
            double initialGround = H36Footprint.GroundMargin(g, final.State);
            var state = new Checkpoint38(final.State, final.Distance);
            var original = state;
            var pending = priorRun.Commands;
            var queue = pending;
            var steps = new List<Step215>();
            var distances = new List<double>();
            double start = marker.State.Time, end = start + comparisonSeconds;
            var model = lab.Engine(g);
            double laneRequestTime = pending.First(c => c.Requested >= start - 1e-10).Requested;
            var laneMark = priorRun.States.First(s => s.State.Time >= laneRequestTime - 1e-10).State;
            var tune = parent.Tune with
            {
                Lane = lab.Course.Mark(laneMark, g).Road.OffsetMeters - .5, Lookahead = 23, RollGain = 17,
                Damping = 8, Fore = 0, Side = 0, Vertical = 0, Boot = 0, ApexPaceDropKph = 0, ApproachSpeedCorrectionKph = 0
            };
            string stop = "verified interval";
            int predictions = 0;
            double finishAt = lab.Bend.NextEntry - lab.Bend.Entry - 2;
            double next = pending[^1].Requested + Cadence38;
            while (state.State.Time < end - 1e-9 && state.Distance < finishAt && stop == "verified interval")
            {
                if (state.State.Time >= next - 1e-10)
                {
                    var mark = lab.Course.Mark(state.State, g);
                    var input = H36Control(state.State, g, model, mark, lab.Bend.Entry + state.Distance, lab.Bend,
                        tune, 81, origin.State.Command.Bike.Drive, queue, lab.Course, rollReferenceScale: .6);
                    predictions += input.Steps;
                    if (input.Command is not { } c) { stop = input.Stop ?? "straight controller abstention"; break; }
                    queue = Submit38(state, queue, c);
                    next += Cadence38;
                }
                var window = Window38(lab, g, model, state, queue,
                    Math.Min(next, end) - state.State.Time, finishAt: finishAt);
                steps.AddRange(window.Steps);
                distances.AddRange(window.Distances);
                state = window.Final;
                stop = window.Stop;
                initialGround = Math.Min(initialGround, window.Ground);
                initialEdge = Math.Min(initialEdge, window.Boundary);
            }
            var lastInterval = steps.Where(s => s.After.Time > end - .25).ToArray();
            var finalSpeed = Speed38(state.State, g);
            bool stable = lastInterval.Length > 0 && state.State.Time >= end - 1e-9 &&
                lastInterval.All(s => Math.Abs(s.After.Pose.Roll) <= .22 &&
                    Math.Abs(lab.Course.Mark(s.After, g).AngularVelocity) <= .28) &&
                finalSpeed >= Speed38(lastInterval[0].Before, g) &&
                finalSpeed >= Speed38(marker.State, g) + 1;
            var trail = new Flight38(state, steps.ToArray(), distances.ToArray(), stop, initialEdge, initialGround);
            var run = new Attempt38(priorRun.Result.Plan, original, original, pending, queue, trail, predictions, false);
            WriteRun38(lab, output, $"qualified-straight-{name}", g, run);
            results.Add(new { Name = name, Complete = stable, Stop = stop, Distance = state.Distance, StartKph = Speed38(marker.State, g),
                TwentyKph = priorRun.Result.TwentyKph, FinalKph = finalSpeed, Roll = state.State.Pose.Roll,
                YawRate = lab.Course.Mark(state.State, g).AngularVelocity, Boundary = initialEdge, Ground = initialGround,
                Steps = steps.Count, ForecastSteps = predictions });
        }
        Save(Path.Combine(output, "straight-comparison.json"), new { BaselineTwentyKph = baselineTwenty, BaselineStraightKph = baseline.FinalKph,
            BaselineDistance = location, ComparisonSeconds = comparisonSeconds, Final = results });
    }
}
