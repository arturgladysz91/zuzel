namespace SpeedwayHandling;

internal static partial class Study191
{
    private sealed record HeldReplay45(Checkpoint38 Root, Activation215[] Tape, Checkpoint38 Final, string Stop);
    private sealed record AlreadyHeld45(string Name, Checkpoint38 Root, Activation215[] Tape, State215 Terminal, string Stop);

    private static void References45(Lab38 lab, Geometry215 g, string output)
    {
        List<AlreadyHeld45> inputs = [];
        foreach (string name in new[] { "recourse-selected", "line-selected", "initial-four", "support-certification-selected" })
        {
            var e = Read199<Evidence39>(Input45(lab, name + ".json.gz"));
            inputs.Add(new(name, new(e.Seed, -PreparationStart37), e.Commands, e.Rows[^1].State, e.Stop));
        }
        for (int i = 1; i <= 4; i++)
        {
            string name = $"feedback-holdout-{i}";
            var e = Read199<HeldReplay45>(Input45(lab, name + "-decisions.json.gz"));
            inputs.Add(new(name, e.Root, e.Tape, e.Final.State, e.Stop));
        }
        var trials = inputs.AsParallel().AsOrdered().WithDegreeOfParallelism(4).Select(e =>
        {
            var f = ReplayTape38(lab, g, e.Root, e.Tape, e.Terminal.Time, exact: true);
            var full = new Fork40(e.Name, e.Root, f with { Stop = e.Stop }, e.Tape);
            var summary = Summarize45(lab, g, new(Baseline45(lab), full, [], 0, 0, 0, null), Pair45, SecondExit44(lab));
            return new
            {
                e.Name, e.Root, e.Stop, ReplayStop = f.Stop, FullEquality = f.Final.State == e.Terminal,
                PositionError = (f.Final.State.Center - e.Terminal.Center).Norm,
                MomentumError = (f.Final.State.Momentum - e.Terminal.Momentum).Norm,
                AngularError = (f.Final.State.Angular - e.Terminal.Angular).Norm,
                RiderError = (f.Final.State.RiderMomentum - e.Terminal.RiderMomentum).Norm,
                f.Final, summary, Pending = Pending43(full, f.Final, true),
                Energy = Energy45(f), Commands = e.Tape.Length, Source = "versioned Study #224 tape"
            };
        }).ToArray();
        Save(Path.Combine(output, "references.json"), new { Base = Base45, Reproduced = trials, NativeWorlds = 0 });

        var initial = ReadMotion42(lab, Input45(lab, "initial-four.json.gz"), "initial four");
        var refined = ReadMotion42(lab, Input45(lab, "recourse-selected.json.gz"), "fastest pair");
        var longest = ReadMotion42(lab, Input45(lab, "support-certification-selected.json.gz"), "longest hard verified");
        var frozen = ReadMotion42(lab, Input45(lab, "line-selected.json.gz"), "frozen H43");
        Save(Path.Combine(output, "rider-history.json.gz"), new { Neutral = g.Neutral,
            Traces = new[] { initial, refined, longest, frozen }.Select(m =>
                new { m.Name, Seams = SeamStates45(lab, g, m), History = RiderHistory45(lab, g, m, 12),
                    Last = RiderHistory45(lab, g, m, 1, Math.Max(0, m.Flight.Steps.Length - 1600)), Energy = Energy45(m.Flight) })
        }, true);
        var selected = ReadPlain214<Piece44[]>(Input45(lab, "recourse-combined-policy.json"));
        var basis = Baseline45(lab);
        var purposes = new[] { basis }.Concat(Plans45(lab, basis, "lines").Skip(1).Take(3));
        Save(Path.Combine(output, "guide-geometry.json.gz"), new
        {
            Guide = selected, ReferenceLapMeters = Frame.LapMeters,
            OffsetCheck = new[] { 4d, 8, 10 }.Select(offset => new
            {
                Offset = offset, AtEntry = Frame.At(lab.Bend.Entry, offset).Position,
                AtApex = Frame.At(lab.Bend.Entry + lab.Bend.Length / 2, offset).Position,
                AtExit = Frame.At(lab.Bend.Exit, offset).Position
            }),
            Families = purposes.Select(p => new { p, Points = Enumerable.Range(0, 639).Select(i =>
            {
                double station = lab.Bend.Entry + i * .5;
                var point = LineAt44(lab, p.Route, station, x => Lane45(lab, p, x));
                var position = Frame.At(station, point.Offset).Position;
                return new { S = i * .5, point.Offset, point.Curvature, position.X, position.Y };
            }) })
        }, true);
    }

    private static object[] SeamStates45(Lab38 lab, Geometry215 g, Fork40 m) =>
        Enumerable.Range(0, 5).Select(i => i * Frame.LapMeters).Where(s => s <= m.Flight.Final.Distance).Select(s =>
        {
            int i = Index43(m.Flight.Distances, s);
            var state = m.Flight.Steps[i].After;
            return (object)new { Phase = PhaseAt43(lab, g, m, i), State = state,
                Hard = H36Footprint.GroundMargin(g, state), Boundary = H36Footprint.Clearance(lab.Track, g, state) };
        }).ToArray();

    private static object Energy45(Flight38 flight)
    {
        double balance = 0, max = 0, muscle = 0, power = 0, passive = 0, leg = 0;
        foreach (var r in flight.Steps)
        {
            balance += r.EnergyResidual;
            max = Math.Max(max, Math.Abs(balance));
            double dt = Duration215(r);
            muscle = Math.Max(muscle, r.Forces.Rider.Norm + Math.Abs(r.Forces.LeftFoot?.MuscleForce ?? 0));
            power = Math.Max(power, r.Forces.LeftFoot?.AbsoluteMusclePower ?? Math.Abs(r.ActuatorPower));
            passive += (r.Forces.PassiveSupport?.Loss ?? 0) * dt;
            leg += r.ActuatorPower * dt;
        }
        return new { WorkErrorJ = max, FinalErrorJ = balance, MaximumMuscleN = muscle, MaximumMuscleWatts = power,
            PassiveDissipationJ = passive, ActiveWorkJ = leg };
    }

    private static IEnumerable<object> RiderHistory45(Lab38 lab, Geometry215 g, Fork40 m, int stride, int begin = 0)
    {
        var f = m.Flight;
        for (int i = begin; i < f.Steps.Length; i++)
        {
            if ((i - begin) % stride != 0 && i + 1 != f.Steps.Length) continue;
            var r = f.Steps[i];
            var s = r.After;
            var mark = lab.Course.Mark(s, g);
            var road = Math.Atan2(mark.Road.Tangent.Y, mark.Road.Tangent.X);
            var velocity = r.Midpoint.Momentum.Plane / g.Base.Mass;
            var a = (r.After.Momentum - r.Before.Momentum).Plane / (Duration215(r) * g.Base.Mass);
            var pads = r.Forces.PassiveSupport;
            var rider = g.Motion(s);
            yield return new
            {
                Station = f.Distances[i], s.Time, s.Center, Speed = Speed38(s, g), Offset = mark.Road.OffsetMeters,
                HeadingError = Difference38(Heading38(s), road), Sideslip = SlipAngle38(s), s.Pose.Roll, s.Pose.Pitch,
                YawRate = mark.AngularVelocity, s.Steer, ActiveDrive = r.Before.Command.Bike.Drive,
                Curvature = velocity.Cross(a) / Math.Max(.001, Math.Pow(velocity.Norm, 3)),
                AlongAcceleration = velocity.Dot(a) / Math.Max(.001, velocity.Norm),
                RelativePosition = s.Rider - g.Neutral, rider.RiderRate, s.RiderMomentum,
                RiderTarget = r.Before.Command.Target, RearwardReserve = .36 + s.Rider.X - g.Neutral.X,
                Seat = pads?.Seat.Force, LeftPeg = pads?.LeftPeg.Force, RightPeg = pads?.RightPeg.Force,
                LeftGrip = pads?.LeftGrip.Force, RightGrip = pads?.RightGrip.Force, PadLossWatts = pads?.Loss,
                MuscleForce = r.Forces.Rider, ShoeMuscle = r.Forces.LeftFoot?.MuscleForce,
                AbsolutePower = r.Forces.LeftFoot?.AbsoluteMusclePower ?? Math.Abs(r.ActuatorPower),
                r.ActuatorPower, r.Forces.RearSlip, Front = r.Forces.Front.Normal, Rear = r.Forces.Rear.Normal,
                RearLongitudinal = r.Forces.Rear.Longitudinal, r.Forces.SlideLoss, r.EnergyResidual,
                Hard = H36Footprint.GroundMargin(g, s), r.Subdivisions,
                Pending = m.Tape.Skip(s.Next).TakeWhile(q => q.Requested <= s.Time + 1e-10).ToArray()
            };
        }
    }

    private static object? DescribeRejection45(Geometry215 g, RejectedStep45? failed)
    {
        if (failed is not { } r) return null;
        string[] labels = ["engine", "rear frame", "upper frame", "right peg", "left peg", "front fork", "handlebar"];
        object Inspect(State215 s) => new
        {
            State = s, s.Pose.Roll, s.Pose.Pitch, g.Motion(s).BodyRate, g.Motion(s).RiderRate,
            HardParts = H36Footprint.Parts(g, s).Where(x => x.GroundHard)
                .Select((x, i) => new { Name = labels[i], x.Bottom }).OrderBy(x => x.Bottom)
        };
        return new
        {
            r.Reason, r.Station, r.RequestedStep, r.SweptBoundary, r.SweptGround,
            Start = Inspect(r.Before), Mid = r.Step is { } step ? Inspect(step.Midpoint) : null,
            Rejected = r.Candidate is { } state ? Inspect(state) : null,
            GroundTransportBound = r.Step is { } row ? H36Footprint.TransportBound(g, row, groundOnly: true) : (double?)null
        };
    }

    private static void Discrepancies45(Lab38 lab, Geometry215 g, string output)
    {
        var root = ReadMotion42(lab, Input45(lab, "initial-four.json.gz"), "initial four");
        var study = ReadPlain214<SupportSearch44>(Input45(lab, "support-recourse.json"));
        var outcomes = study.Proposals.AsParallel().WithDegreeOfParallelism(3).Select(x =>
        {
            var p = x.Policy;
            var sliced = Slice42(root, p.StartStation);
            var run = Ride44(lab, g, root, p.StartStation, [new(p.StartStation, p)], Four45);
            var screen = run.Motion;
            var until = screen.Flight.Final.State.Time;
            RejectedStep45? reject = null, reducedStop = null;
            var exact = ReplayTape38(lab, g, screen.Seed, screen.Tape, until, exact: true, rejected: r => reject = r);
            var reduced = ReplayTape38(lab, g, screen.Seed, screen.Tape, until, rejected: r => reducedStop = r);
            var at = sliced.At;
            RejectedStep45? fromForkStop = null;
            var local = ReplayTape38(lab, g, at, screen.Tape, until, exact: true, rejected: r => fromForkStop = r);
            var sparse = screen.Flight.Steps;
            int common = 0;
            while (common < Math.Min(sparse.Length, exact.Steps.Length) &&
                   sparse[common].After == exact.Steps[common].After) common++;
            int near = Array.FindIndex(exact.Steps, t => t.After.Time >= at.State.Time - 1e-12);
            var before = near >= 0 ? exact.Steps[near].After : null;
            Save(Path.Combine(output, $"discrepancy-{p.Id}-tape.json.gz"), new
            {
                Root = screen.Seed, screen.Tape, Fork = at, SubmittedBeforeFork = sliced.Commands,
                PendingAtFork = Pending43(screen, at, false), exact.Final, Rejected = DescribeRejection45(g, reject),
                ReducedRootFinal = reduced.Final, ScreeningFinal = screen.Flight.Final,
                LocalExactFinal = local.Final, ExactSeams = SeamStates45(lab, g, screen with { Flight = exact }),
                LocalRejection = DescribeRejection45(g, fromForkStop),
                ReducedRootRejection = DescribeRejection45(g, reducedStop)
            }, true);
            return new
            {
                p.Id, ArchivedScreen = x.Facts.Station, Screen = screen.Flight.Final, ScreeningGround = screen.Flight.Ground,
                Exact = exact.Final, ExactGround = exact.Ground, ExactStop = exact.Stop, ReducedRoot = reduced.Final,
                ReducedStop = reduced.Stop, LocalExact = local.Final, LocalExactStop = local.Stop,
                Fork = at, ExactAtFork = before, ForkCenterError = before is null ? (double?)null : (at.State.Center - before.Center).Norm,
                ForkMomentumError = before is null ? (double?)null : (at.State.Momentum - before.Momentum).Norm,
                CommonIdenticalSteps = common,
                FirstDifferent = common < Math.Min(sparse.Length, exact.Steps.Length)
                    ? new { Screen = sparse[common].After, Exact = exact.Steps[common].After } : null,
                Rejected = DescribeRejection45(g, reject)
            };
        }).OrderBy(x => x.Id).ToArray();
        Save(Path.Combine(output, "discrepancies.json.gz"), new { Cases = outcomes, GroundFormula = "same in reduced and exact",
            FullHorizon = Four45 }, true);
    }

    private static void Limits45(Lab38 lab, Geometry215 g, string output)
    {
        var root = ReadMotion42(lab, Input45(lab, "recourse-selected.json.gz"), "fastest two laps");
        var basePlan = Baseline45(lab);
        var schedule = ReadPlain214<Piece44[]>(Input45(lab, "recourse-combined-policy.json"));
        var model = lab.Engine(g);
        double exit = SecondExit44(lab);
        double lap = Frame.LapMeters;
        double[] stations = [exit - 18, exit - 8, exit - 2, exit + 6, exit + 21, lap - 34, lap - 20, lap - 8, lap + 6,
            lap + 35, lap + 83, 480, 540, 558, 571, 595, 612];
        var items = stations.Select(s =>
        {
            var (at, prefix, _, tape) = Slice42(root, s);
            var r = prefix[^1];
            var active = schedule.LastOrDefault(p => p.Start <= s)?.Route ?? basePlan.Route;
            var variants = Enum.GetValues<Limit45>().Select(variant =>
            {
                List<PaceAudit45> details = [];
                var p = basePlan with { Ablation = variant, Route = active };
                var c = Choose44(lab, g, model, H36Footprint, at, r.Forces, tape, active, p, details.Add);
                return new { Rule = variant.ToString(), Trace = details, c.Note, Request = c.Command,
                    Next = c.Command is { } cmd ? Probe45(cmd) : null };
            }).ToArray();

            var guide = variants[0].Request ?? at.State.Command;
            var independent = new[] { 0f, .34f, .67f, 1f }.SelectMany(drive => new[] { -.022f, 0, .022f }.Select(steer =>
            {
                var cmd = guide with { Bike = new((float)Steering39(guide.Bike.SteeringRadians + steer), drive),
                    Target = guide.Target + new Vec215(drive * .032, 0, -.012 * drive) };
                return new { cmd, Next = Probe45(cmd) };
            })).ToArray();

            object Probe45(Command215 c)
            {
                var trial = Window38(lab, g, model, at, Submit38(at, tape, c), .24, exact: true);
                var target = trial.Final;
                return new { trial.Stop, trial.Boundary, trial.Ground, trial.Final,
                    Advance = target.Distance - at.Distance, Speed = Speed38(target.State, g), Detail = Measure38(trial.Steps, g, trial.Stop) };
            }

            return new { Checkpoint = at, Phase = AtPhase43(lab, g, root, s), Pending = Pending43(root, at, false),
                Variants = variants, Independent = independent };
        }).ToArray();
        Save(Path.Combine(output, "matched-limits.json.gz"), new { ForecastSeconds = .24, ReactionSeconds216, Cases = items }, true);
    }
}
