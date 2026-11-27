using System.Diagnostics;

namespace SpeedwayHandling;

internal static partial class Study191
{
    private sealed record FeedbackPlan46(string Name, Plan45 Route, double Damping, double StartStation,
        double FinishStation);
    private sealed record Freeze46(FeedbackPlan46 Policy, string Selection, HeldSeed38[] Holdouts,
        bool FourQualifiedNominal, string Basis);

    private static CanonicalRun46 Live46(Lab38 lab, Geometry215 g, ReferenceTape46 reference, ClockPlan46 clock,
        FeedbackPlan46 guide)
    {
        var admitted = reference.Tape.Select(Clock46.Import).ToArray();
        var commands = admitted;
        var engine = new Propagator46(lab, g, clock, reference.Seed);
        bool taken = false;
        long review = clock.Origin;
        while (!engine.Done)
        {
            long now = Clock46.Tick(engine.State.State.Time);
            if (now >= review)
            {
                review += Clock46.ReviewTicks;
                if (now != review - Clock46.ReviewTicks)
                    throw new InvalidOperationException("H45 review must coincide with a physical epoch");
                if (engine.State.Distance >= guide.StartStation)
                {
                    if (!taken)
                    {
                        commands = admitted.Where(x => Clock46.Tick(x.Requested) < now).ToArray();
                        taken = true;
                    }
                    long cpu = ThreadTicks43(), start = Stopwatch.GetTimestamp();
                    var previous = engine.Rows.LastOrDefault()?.Forces;
                    var at = engine.State;
                    var config = guide.Route.TransitionMeters > 0
                        ? guide.Route with { Blend = guide.Route.Blend *
                            Smooth37((at.Distance - guide.StartStation) / guide.Route.TransitionMeters) } : guide.Route;
                    var next = Choose44(lab, g, engine.Model, H36Footprint, at, previous, commands, config.Route, config);
                    Command215? command = next.Command is { } primary && guide.Damping > 0
                        ? Stabilize46(lab, g, at, previous, primary, next.Note, guide.Damping) : next.Command;
                    engine.Decisions.Add(next.Note with { Drive = command?.Bike.Drive ?? next.Note.Drive,
                        Steering = command?.Bike.SteeringRadians ?? next.Note.Steering,
                        Milliseconds = Stopwatch.GetElapsedTime(start).TotalMilliseconds });
                    engine.DecisionCpu += ThreadTicks43() - cpu;
                    if (command is null)
                    {
                        engine.Stop = next.Note.Refusal ?? "feedback refused";
                        break;
                    }
                    commands = [.. commands, Clock46.Command(now, command.Value)];
                }
            }
            engine.Advance(commands);
        }
        return engine.Result(guide.Name, reference.Seed, commands);
    }

    private static Command215 Stabilize46(Lab38 lab, Geometry215 g, Checkpoint38 at, Forces215? last,
        Command215 primary, StateDecision44 guide, double gain)
    {
        var s = at.State;
        var kin = g.Motion(s);
        double rate = RollRate36(s, kin.BodyRate);
        double gap = H36Footprint.GroundMargin(g, s);
        double front = last?.Front.Normal ?? 650;
        double slip = Math.Abs(last?.RearSlip ?? 0);
        double pitchRate = kin.WorldRate.Cross(s.Pose.Apply(Vec215.Forward)).Z;
        double unloading = Smooth37((295 - front) / 215) * Smooth37((pitchRate + .06) / .22);
        double rearTravel = g.Neutral.X - s.Rider.X - .16 * kin.RiderRate.X;
        double posture = Smooth37((rearTravel - .025) / .13);
        double direction = Difference38(Heading38(s), lab.Course.Mark(s, g).Road.Heading);
        // Check actual travel direction and rate before attenuating a correction.
        // Near the ground, leave the existing inverse its full steering authority.
        double quiet = Smooth37((gap - .022) / .10) *
            (1 - Smooth37((Math.Abs(rate) - .19) / .56)) *
            (1 - Smooth37((Math.Abs(direction) - .055) / .22)) *
            (1 - Smooth37((Math.Abs(guide.TargetRoll - s.Pose.Roll) - .055) / .19));
        double steer = primary.Bike.SteeringRadians +
            gain * quiet * .46 * (s.Steer - primary.Bike.SteeringRadians);
        double drive = primary.Bike.Drive * (1 - gain * Math.Max(.82 * unloading, .55 * posture) *
            (1 - .25 * Smooth37((slip - .11) / .31)));
        var target = primary.Target + new Vec215(.054, 0, -.015) * (gain * Math.Max(unloading, posture));
        return primary with { Bike = new((float)Steering39(steer), (float)Math.Clamp(drive, 0, 1)),
            Target = target with { X = Math.Clamp(target.X, -Command215.MaximumForwardTarget, Command215.MaximumForwardTarget),
                Z = Math.Clamp(target.Z, -Command215.MaximumVerticalTarget, Command215.MaximumVerticalTarget) } };
    }

    private static FeedbackPlan46[] Guides46(Lab38 lab)
    {
        var lines = Read199<Stage45>(Source46(lab, 225, "transition.json.gz"));
        var sustain = Read199<Stage45>(Source46(lab, 225, "sustain.json.gz"));
        var best = Read199<Stage45>(Source46(lab, 225, "support.json.gz"));
        var a = best.Exact.Single(x => x.Plan.Id == 225).Plan;
        var b = lines.Exact.Single(x => x.Plan.Id == 615).Plan;
        var d = sustain.Exact.Single(x => x.Plan.Id == 802).Plan;
        var frozen = Baseline45(lab);
        var a224 = Read199<SupportCertificate45>(Source46(lab, 225, "support-exact.json.gz")).Proofs
            .Single(x => x.Id == 224).Exact.Plan;
        return [new("H43-A", frozen, 0, lines.Fork, Four45), new("H44-A224", a224, 0, lines.Fork, Four45),
            new("H44-A225", a, 0, lines.Fork, Four45), new("H44-B615", b, 0, lines.Fork, Four45),
            new("H44-D802", d, 0, lines.Fork, Four45),
            new("H45-A225", a, .28, lines.Fork, Four45),
            new("H45-B615", b, .40, lines.Fork, Four45),
            new("H45-D802", d, .40, lines.Fork, Four45)];
    }

    private static ReferenceTape46 Carrier46(Lab38 lab)
    {
        var e = Read199<Evidence39>(Source46(lab, 222, "qualification/nominal.json.gz"));
        return new("archived H41 initial carrier", new(e.Seed, -PreparationStart37), e.Commands, e.Seed.Time + 100,
            Four45, "qualification/nominal.json.gz");
    }

    private static void Feedback46(Lab38 lab, Geometry215 g, string output, string[] args)
    {
        var input = Carrier46(lab);
        var clock = PlanClock46(input, refine: true);
        var guides = Guides46(lab);
        if (args.Length > 0) guides = guides.Where(x => args.Contains(x.Name)).ToArray();
        double gate = guides[0].StartStation;
        var reachToHandoff = ReplayCanonical46(lab, g, input with { Name = "canonical common carrier" },
            clock with { FinishMeters = gate });
        var old = Read199<Evidence39>(Source46(lab, 225, "transition-615-full.json.gz"));
        var fixedAt = old.Rows.First(x => x.ReferenceMeters >= gate + 1.8);
        var anchor = new Checkpoint38(fixedAt.State, fixedAt.ReferenceMeters);
        var localTour = new ReferenceTape46("H44 reached handoff", anchor,
            old.Commands.Where(c => c.Requested < anchor.State.Time - 1e-10).ToArray(), anchor.State.Time + 28,
            null, "transition-615-full");
        var localClock = PlanClock46(localTour, refine: true) with { FinishMeters = 2 * Frame.LapMeters + PostMarkerMeters43 };
        var prospective = guides.AsParallel().AsOrdered().WithDegreeOfParallelism(4).Select(p =>
        {
            bool reachable = reachToHandoff.Motion.Flight.Final.Distance >= gate;
            var source = reachable ? input : localTour;
            var timing = reachable ? clock : localClock;
            var open = Live46(lab, g, source, timing, p);
            var replayClock = open.Decisions.LastOrDefault()?.Refusal is not null
                ? timing with { EndTick = Clock46.Tick(open.Motion.Flight.Final.State.Time) } : timing;
            var replay = ReplayCanonical46(lab, g, source with { Tape = open.Motion.Tape }, replayClock);
            var run = Digest46(lab, g, timing, open);
            var independent = Digest46(lab, g, timing, replay);
            // A refusal is a controller event, so its application stops the live caller.
            // A physical horizon/rejection must otherwise replay with identical accepted states.
            bool same = run.States == independent.States && run.Schedule == independent.Schedule &&
                run.Dynamics == independent.Dynamics && run.End == independent.End;
            bool four = reachable && same && run.End.Distance >= Four45 - .001;
            string label = p.Name;
            Save(Path.Combine(output, label + "-feedback.json.gz"), new
            {
                p, ReachableFromRoot = reachable, Source = source.Seed, run, independent, Same = same, Four = four,
                open.Motion.Tape, open.Schedule, open.Decisions, Timing = Timing44(open.Decisions),
                open.ControllerCpu, open.IntegrationCpu, Energy = Balance46(open.Motion.Flight, g),
                Rejected = DescribeRejection45(g, open.Rejected),
                Profile = RiderHistory45(lab, g, open.Motion, 14),
                Checkpoints = reachable ? SeamStates45(lab, g, open.Motion) : null
            }, true);
            Console.WriteLine($"{label}: {run.End.Distance:F3} m, from root {reachable}, equal {same}, {run.Stop}");
            return new { Guide = p, fromRoot = reachable, run, same, four, Timing = Timing44(open.Decisions),
                open.ControllerCpu, open.IntegrationCpu };
        }).ToArray();
        var chosen = prospective.OrderByDescending(x => x.four).ThenByDescending(x => x.run.End.Distance).First();
        var fresh = new[] { new HeldSeed38("H45 fresh A", -.11, -.034, .0012),
            new HeldSeed38("H45 fresh B", .16, .045, .0023), new HeldSeed38("H45 fresh C", -.19, .027, .0031),
            new HeldSeed38("H45 fresh D", .21, -.053, .0041) };
        var freeze = new Freeze46(chosen.Guide, "Choose among nominal results and freeze before fresh seeds.",
            fresh, chosen.four, input.Archive);
        Save(Path.Combine(output, "feedback-summary.json"), new { RootReach = reachToHandoff.Motion.Flight.Final,
            RootStop = reachToHandoff.Motion.Flight.Stop, DiagnosticAnchor = anchor, Candidates = prospective, freeze });
        Save(Path.Combine(output, "feedback-freeze.json"), freeze);

        var holds = fresh.AsParallel().AsOrdered().WithDegreeOfParallelism(4).Select(h =>
        {
            var root = SteadyCircle215(lab.Engine(g), g, lab.Fixture.Rest(g, NominalSupport216),
                (40 + h.SpeedKph) / 3.6, 0, input.Seed.State.Time, lab.Fixture.Ratio, balanceRider: true);
            if (root.Residual > .005) throw new InvalidOperationException("Fresh H45 launch equilibrium failed");
            var placed = Place36(root.State, lab.Bend.Entry + input.Seed.Distance, 8.2 + h.LaneMeters);
            Activation215[] carry = input.Tape.Select((q, i) => i < 8 ? q with { Time = q.Time + h.QueueDelaySeconds } : q).ToArray();
            var held = input with { Name = h.Name, Seed = new(placed, input.Seed.Distance), Tape = carry };
            var heldClock = PlanClock46(held, refine: true);
            var live = Live46(lab, g, held, heldClock, freeze.Policy with { Name = h.Name });
            bool four = live.Motion.Flight.Final.Distance >= Four45 - .001;
            CanonicalRun46? replay = four ? ReplayCanonical46(lab, g, held with { Tape = live.Motion.Tape }, heldClock) : null;
            var result = Digest46(lab, g, heldClock, live);
            return new { Seed = h, held.Seed.State, live.Motion.Tape, Result = result, live.Decisions,
                Replayed = replay is null ? (bool?)null : result == Digest46(lab, g, heldClock, replay),
                FourPass = four, Timing = Timing44(live.Decisions), live.ControllerCpu, live.IntegrationCpu,
                Rejected = DescribeRejection45(g, live.Rejected) };
        }).ToArray();
        Save(Path.Combine(output, "feedback-holdouts.json.gz"), new { freeze, Cases = holds }, true);
    }

    private static void Runtime46(Lab38 lab, Geometry215 g, string output)
    {
        string declared = Path.Combine(output, "declared-freeze.json");
        bool aligned = File.Exists(declared);
        var freeze = ReadPlain214<Freeze46>(aligned ? declared : Path.Combine(output, "feedback-freeze.json"));
        var (reference, plan, anchor) = aligned ? LiveContract46(lab, refine: true) : FixedCarrier();
        if (aligned && !freeze.FourQualifiedNominal) plan = plan with { FinishMeters = Pair45 };
        var motion = aligned ? LiveAligned46(lab, g, reference, plan, anchor, freeze.Policy)
            : Live46(lab, g, reference, plan, freeze.Policy);
        var digest = Digest46(lab, g, plan, motion);
        var replay = ReplayCanonical46(lab, g, reference with { Tape = motion.Motion.Tape }, StopClock46(plan, motion));
        bool complete = digest.End.Distance >= (plan.FinishMeters ?? double.PositiveInfinity) - .001 &&
            SameMotion46(digest, Digest46(lab, g, plan, replay));
        Save(Path.Combine(output, "runtime.json"), new { freeze, digest, motion.ControllerCpu, motion.IntegrationCpu,
            WorkerCount = 1, Timing = Timing44(motion.Decisions), PhysicalReactionSeconds = ReactionSeconds216,
            CompletedMeasuredManeuver = complete, FullRaceTimingEligible = complete && digest.End.Distance >= Four45 - .001 });

        (ReferenceTape46, ClockPlan46, double) FixedCarrier()
        {
            var carrier = Carrier46(lab);
            return (carrier, PlanClock46(carrier, refine: true), 0);
        }
    }

    private static void Qualify46(Lab38 lab, Geometry215 g, string output)
    {
        var support = Read199<Stage45>(Source46(lab, 225, "support.json.gz"));
        var sustained = Read199<Stage45>(Source46(lab, 225, "sustain.json.gz"));
        var controls = support.Screen.Concat(sustained.Screen).Where(x => x.Finished).Select(x => x.Plan).ToArray();
        var root = Carrier46(lab);
        var clock = PlanClock46(root, refine: true);
        var carrier = ReplayCanonical46(lab, g, root, clock with { FinishMeters = support.Fork });
        bool reachable = carrier.Motion.Flight.Final.Distance >= support.Fork;
        var candidates = controls.AsParallel().AsOrdered().WithDegreeOfParallelism(4).Select(p =>
        {
            if (!reachable) return new
            {
                p.Id, Handoff = false, Exact = false, End = carrier.Motion.Flight.Final,
                Stop = carrier.Motion.Flight.Stop, Completed = false
            };
            var route = new FeedbackPlan46($"recheck {p.Id}", p, 0, support.Fork, Four45);
            var run = Live46(lab, g, root, clock, route);
            var outcome = run.Motion.Flight;
            bool full = outcome.Final.Distance >= Four45 - .001;
            var replay = full ? ReplayCanonical46(lab, g, root with { Tape = run.Motion.Tape }, clock) : null;
            bool exact = replay is not null && Digest46(lab, g, clock, run) == Digest46(lab, g, clock, replay);
            if (exact)
                WriteEvidence39(lab, output, $"four-exact-{p.Id}", g, root.Seed, run.Motion.Tape, outcome);
            return new { p.Id, Handoff = true, Exact = exact, End = outcome.Final, outcome.Stop, Completed = full && exact };
        }).ToArray();
        var old = References46(lab).Where(x => x.Name is "H43-475" or "H43-476" or "H43-477" or "A224" or "D802");
        var archived = old.AsParallel().AsOrdered().WithDegreeOfParallelism(4).Select(x =>
        {
            var setup = PlanClock46(x, refine: true);
            var r = ReplayCanonical46(lab, g, x, setup);
            return new { x.Name, Result = Digest46(lab, g, setup, r), r.SweepRetries,
                Rejected = DescribeRejection45(g, r.Rejected) };
        }).ToArray();
        Save(Path.Combine(output, "qualification.json"), new { Base = Base46, CommonHandoff = support.Fork,
            carrier.Motion.Flight.Final, carrier.Motion.Flight.Stop, Candidates = candidates, ArchivedTapes = archived,
            FourHorizon = Four45, Passes = candidates.Count(x => x.Completed), NativeWorlds = 0 });
    }
}
