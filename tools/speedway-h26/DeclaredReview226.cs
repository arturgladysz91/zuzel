using System.Diagnostics;
using System.Text.Json;

namespace SpeedwayHandling;

internal static partial class Study191
{
    private static void Align46(Lab38 lab, Geometry215 g, string output, string[] args)
    {
        var refs = References46(lab).Where(t => args.Length == 0 ? t.Name is "A225" or "B615" : args.Contains(t.Name)).ToArray();
        var entries = refs.AsParallel().AsOrdered().WithDegreeOfParallelism(4).Select(input =>
        {
            var archive = Read199<Tape45>(Source46(lab, 225, input.Archive));
            var active = input with { Until = archive.Terminal.Time, Finish = null };
            double[] reviews = active.Tape.Select(x => x.Requested).Where(t =>
                t > active.Seed.State.Time + 1e-10 && t < active.Until - 1e-10).Distinct().ToArray();
            var plan = PlanClock46(active) with { Review = new(reviews, null), RawEnd = active.Until };
            var run = ReplayCanonical46(lab, g, active, plan);
            var result = Digest46(lab, g, plan, run);
            var f = run.Motion.Flight;
            string key = input.Name + "-aligned";
            bool matchesHistorical = archive.Terminal == f.Final.State && archive.AcceptedSteps == f.Steps.Length;
            var scheduleReplay = ReplayCanonical46(lab, g, active, plan, run.Schedule);
            bool scheduleEqual = result == Digest46(lab, g, plan, scheduleReplay);
            int cut = run.Schedule.Length / 2;
            var checkpoint = new Check46(new(f.Steps[cut - 1].After, f.Distances[cut - 1]), run.Motion.Tape, plan);
            var json = new JsonSerializerOptions(Json) { WriteIndented = true };
            var restored = JsonSerializer.Deserialize<Check46>(JsonSerializer.SerializeToUtf8Bytes(checkpoint, json), json)!;
            var tail = ReplayCanonical46(lab, g, active with { Tape = restored.Prefix }, restored.Plan, resume: restored.State);
            bool resumed = run.Schedule[cut..].SequenceEqual(tail.Schedule) &&
                f.Steps[cut..].Select(s => s.After).SequenceEqual(tail.Motion.Flight.Steps.Select(s => s.After)) &&
                tail.Motion.Flight.Final == f.Final;
            bool equal = matchesHistorical && scheduleEqual && resumed;
            Save(Path.Combine(output, key + "-process-input.json.gz"),
                new ProcessInput46(active with { Name = key }, plan, result, run.Schedule), true);
            Save(Path.Combine(output, key + "-motion.json.gz"), new
            {
                input.Name, result, matchesHistorical, scheduleEqual, resumed, run.Schedule,
                Checkpoint = restored, Pending = Pending43(run.Motion, restored.State, false),
                States = f.Steps.Select((s, i) => new { s.After, Station = f.Distances[i], s.Contacts,
                    s.Forces.Front.Normal, RearNormal = s.Forces.Rear.Normal, s.EnergyResidual }),
                Brake = Score43(lab, g, run.Motion), Balance = Balance46(f, g),
                Rejected = DescribeRejection45(g, run.Rejected)
            }, true);
            Console.WriteLine($"{key}: position {result.End.Distance:F3}, historical identical {matchesHistorical}, replay checks {equal}");
            return new { input.Name, result, matchesHistorical, scheduleEqual, resumed,
                Early = AtPhase43(lab, g, run.Motion, 0), Finish = f.Final.Distance >= Pair45 ?
                    AtPhase43(lab, g, run.Motion, 2 * Frame.LapMeters) : null, PairHorizonMet = f.Final.Distance >= Pair45 };
        }).ToArray();
        Save(Path.Combine(output, "declared-review-alignment.json"), new { Base = Base46, Results = entries });
    }

    private static (ReferenceTape46 Input, ClockPlan46 Plan, double Anchor) LiveContract46(Lab38 lab, bool refine)
    {
        var historical = References46(lab).Single(x => x.Name == "A225");
        var carrier = Carrier46(lab);
        int i = 0;
        while (i < historical.Tape.Length && i < carrier.Tape.Length && historical.Tape[i] == carrier.Tape[i]) i++;
        if (i == 0 || i == historical.Tape.Length)
            throw new InvalidOperationException("Historical handoff could not be identified.");
        double anchor = historical.Tape[i].Requested;
        var prefix = historical.Tape[..i];
        var input = historical with { Tape = prefix, Until = historical.Seed.State.Time + 100, Finish = Four45,
            Name = "H45 declared-review carrier" };
        var clock = PlanClock46(input, refine: refine) with
        {
            Review = new(prefix.Select(x => x.Requested).Where(t => t > historical.Seed.State.Time + 1e-10).Distinct().ToArray(), anchor),
            RawEnd = input.Until
        };
        return (input, clock, anchor);
    }

    private static CanonicalRun46 LiveAligned46(Lab38 lab, Geometry215 g, ReferenceTape46 input, ClockPlan46 clock,
        double anchor, FeedbackPlan46 guide, CanonicalRun46? verifiedPrefix = null)
    {
        var engine = new Propagator46(lab, g, clock, input.Seed);
        if (verifiedPrefix is { } accepted) engine.ContinueVerifiedPrefix(accepted, anchor);
        var queue = input.Tape;
        long cycle = 0;
        double review = anchor;
        while (!engine.Done)
        {
            var at = engine.State;
            if (at.State.Time >= review - ReviewTolerance46)
            {
                if (Math.Abs(at.State.Time - review) > ReviewTolerance46)
                    throw new InvalidOperationException("The absolute review boundary was crossed.");
                long wall = Stopwatch.GetTimestamp(), cpu = ThreadTicks43();
                var last = engine.Rows.LastOrDefault()?.Forces;
                var p = guide.Route.TransitionMeters > 0
                    ? guide.Route with { Blend = guide.Route.Blend * Smooth37((at.Distance - guide.StartStation) / guide.Route.TransitionMeters) }
                    : guide.Route;
                var choice = Choose44(lab, g, engine.Model, H36Footprint, at, last, queue, p.Route, p);
                Command215? command = choice.Command is { } inputCommand && guide.Damping > 0
                    ? Stabilize46(lab, g, at, last, inputCommand, choice.Note, guide.Damping) : choice.Command;
                double latency = Stopwatch.GetElapsedTime(wall).TotalMilliseconds;
                engine.DecisionCpu += ThreadTicks43() - cpu;
                engine.Decisions.Add(choice.Note with { Drive = command?.Bike.Drive ?? choice.Note.Drive,
                    Steering = command?.Bike.SteeringRadians ?? choice.Note.Steering, Milliseconds = latency });
                if (command is null)
                {
                    engine.Stop = choice.Note.Refusal ?? "feedback refused";
                    break;
                }
                queue = [.. queue, new(review, review + ReactionSeconds216, command.Value)];
                review = anchor + ++cycle * Cadence38;
            }
            engine.Advance(queue);
        }
        return engine.Result(guide.Name, input.Seed, queue);
    }

    private static void AlignedFeedback46(Lab38 lab, Geometry215 g, string output, string[] args)
    {
        var (input, clock, anchor) = LiveContract46(lab, refine: true);
        var guides = Guides46(lab);
        bool supportRecheck = args is ["support-recheck"];
        if (supportRecheck)
        {
            var source = Read199<Stage45>(Source46(lab, 225, "support.json.gz"));
            guides = source.Screen.Where(p => p.Finished).Select(p => new FeedbackPlan46(
                $"historical-support-{p.Plan.Id}", p.Plan, 0, source.Fork, Four45)).ToArray();
        }
        else if (args.Length > 0) guides = guides.Where(p => args.Contains(p.Name)).ToArray();
        var prefixClock = clock with { RawEnd = anchor, EndTick = Clock46.Tick(anchor), FinishMeters = null };
        var prefix = ReplayCanonical46(lab, g, input, prefixClock);
        var results = guides.AsParallel().AsOrdered().WithDegreeOfParallelism(4).Select(p =>
        {
            var run = LiveAligned46(lab, g, input, clock, anchor, p, prefix);
            var f = run.Motion.Flight;
            var replayContract = StopClock46(clock, run);
            var exact = ReplayCanonical46(lab, g, input with { Tape = run.Motion.Tape }, replayContract);
            var online = Digest46(lab, g, replayContract, run);
            var replay = Digest46(lab, g, replayContract, exact);
            bool equal = SameMotion46(online, replay);
            string key = p.Name + "-declared";
            Save(Path.Combine(output, key + ".json.gz"), new
            {
                p, Clock = replayContract, Anchor = anchor, Source = input.Seed, online, replay, equal,
                run.Motion.Tape, run.Schedule, run.Decisions, Timing = Timing44(run.Decisions),
                run.ControllerCpu, run.IntegrationCpu, run.SweepRetries, Balance = Balance46(f, g),
                Position = RiderHistory45(lab, g, run.Motion, 16), Checkpoints = SeamStates45(lab, g, run.Motion),
                Rejected = DescribeRejection45(g, run.Rejected)
            }, true);
            if (equal && f.Final.Distance >= Four45 - .001)
                WriteEvidence39(lab, output, key + "-four", g, input.Seed, run.Motion.Tape, f);
            Console.WriteLine($"{key}: {f.Final.Distance:F3} m, exact {equal}, {f.Stop}");
            return new { Policy = p, online, replay, equal, Four = equal && f.Final.Distance >= Four45 - .001,
                Pair = equal && f.Final.Distance >= Pair45 - .001 };
        }).ToArray();
        var best = results.OrderByDescending(r => r.Four).ThenByDescending(r => r.Pair)
            .ThenByDescending(r => r.online.End.Distance).First();
        var starts = new[] { new HeldSeed38("declared fresh A", -.08, -.026, .0015),
            new HeldSeed38("declared fresh B", .12, .031, .0026),
            new HeldSeed38("declared fresh C", -.15, .040, .0037),
            new HeldSeed38("declared fresh D", .17, -.049, .0048) };
        var freeze = new Freeze46(best.Policy, "Choose declared-review nominal outcome before evaluating held starts.",
            starts, best.Four, input.Archive);
        Save(Path.Combine(output, "declared-freeze.json"), freeze);
        Save(Path.Combine(output, "declared-feedback-summary.json"), new { anchor, Clock = clock, Results = results, freeze,
            Prefix = new { prefix.Motion.Flight.Final, prefix.Motion.Flight.Stop, OfflineCpu = prefix.IntegrationCpu } });
        if (supportRecheck && !best.Four) return;

        var held = starts.AsParallel().AsOrdered().WithDegreeOfParallelism(4).Select(h =>
        {
            var equilibrium = SteadyCircle215(lab.Engine(g), g, lab.Fixture.Rest(g, NominalSupport216),
                (40 + h.SpeedKph) / 3.6, 0, input.Seed.State.Time, lab.Fixture.Ratio, balanceRider: true);
            if (equilibrium.Residual > .005) throw new InvalidOperationException("Fresh H45 equilibrium failed.");
            var seed = new Checkpoint38(Place36(equilibrium.State, lab.Bend.Entry + input.Seed.Distance, 8.2 + h.LaneMeters),
                input.Seed.Distance);
            var shifted = input with { Seed = seed, Tape = input.Tape.Select((q, i) => i < 8 ?
                q with { Time = q.Time + h.QueueDelaySeconds } : q).ToArray() };
            var online = LiveAligned46(lab, g, shifted, clock, anchor, freeze.Policy);
            var confirmation = StopClock46(clock, online);
            var replay = ReplayCanonical46(lab, g, shifted with { Tape = online.Motion.Tape }, confirmation);
            var a = Digest46(lab, g, confirmation, online);
            var b = Digest46(lab, g, confirmation, replay);
            bool exact = SameMotion46(a, b);
            if (exact && a.End.Distance >= Four45 - .001)
                WriteEvidence39(lab, output, "four-held-" + h.Name, g, seed, online.Motion.Tape, online.Motion.Flight);
            return new { h, Seed = seed, a, b, Exact = exact, Four = exact && a.End.Distance >= Four45 - .001,
                Pair = exact && a.End.Distance >= Pair45 - .001, online.Motion.Tape, online.Schedule, online.Decisions,
                Rejected = DescribeRejection45(g, online.Rejected) };
        }).ToArray();
        Save(Path.Combine(output, "declared-holdouts.json.gz"), new { freeze, Held = held }, true);
    }

    private static ClockPlan46 StopClock46(ClockPlan46 plan, CanonicalRun46 run)
    {
        if (run.Decisions.LastOrDefault()?.Refusal is null) return plan;
        double at = run.Motion.Flight.Final.State.Time;
        return plan with { EndTick = Clock46.Tick(at), RawEnd = plan.RawEnd.HasValue ? at : null };
    }

    private static bool SameMotion46(RunDigest46 a, RunDigest46 b) =>
        a.States == b.States && a.Schedule == b.Schedule && a.Dynamics == b.Dynamics && a.End == b.End;
}
