namespace SpeedwayHandling;

internal static partial class Study191
{
    private sealed record TrackerArchive44(Checkpoint38 Seed, Activation215[] Tape, Checkpoint38 Final, TrackDecision43[] Decisions);
    private sealed record Receipt224(string Name, bool ExactTerminal, double PositionDifference, double VelocityMomentDifference,
        double Station, string Stop, double Swept, double Ground, Phase43[] Phases, ArcReceipt44[] Arcs);

    private static void Reproduce44(Lab38 lab, string output)
    {
        var g = lab.Geometry(true);
        string[] cases = ["speedway222/cycle/cycle-flying", "speedway223/periodic-selected",
            "speedway222/frontier/speed-frontier", "speedway220/straight-full-diagnostic",
            "speedway223/speed-highest", "speedway222/qualification/nominal"];
        var periodic = cases[0];
        var tapes = cases.AsParallel().AsOrdered().WithDegreeOfParallelism(Jobs44).Select(name =>
        {
            var path = ArchivePath43(lab, name);
            var d = Archived44(lab, name, name);
            var saved = Read199<Evidence39>(path).Rows[^1].State;
            double[] stations = name == cases[0] || name == cases[1]
                ? [-52, -42, -32, -22, -12, -6, 0, 6, 12, 22, 28, 33,
                    SecondExit44(lab) - 15, SecondExit44(lab), SecondExit44(lab) + 7,
                    .. new[] { -52d, -42, -32, -22, -12, -6, 0, 6, 12, 22, 28, 33 }.Select(x => Frame.LapMeters + x)]
                : new[] { -54d, -42, -32, -24, -16, -8, -4, 0, 4, 8, 12, 18, 24, 32 }
                    .Select(x => lab.Bend.NextEntry - lab.Bend.Entry + x).ToArray();
            var phases = stations.Where(x => x <= d.Flight.Final.Distance).Select(x => AtPhase43(lab, g, d, x))
                .Append(PhaseAt43(lab, g, d, d.Flight.Steps.Length - 1)).ToArray();
            return new Receipt224(name, saved == d.Flight.Final.State, (saved.Center - d.Flight.Final.State.Center).Norm,
                (saved.Momentum - d.Flight.Final.State.Momentum).Norm, d.Flight.Final.Distance,
                d.Flight.Stop, d.Flight.Boundary, d.Flight.Ground, phases, Lap44(lab, g, d, 0).Arcs);
        }).ToArray();
        Save(Path.Combine(output, "reference-phases.json"), new { Base = Base44, Motions = tapes, NativeWorlds = 0 });

        var parent = Archived44(lab, periodic, "policy 301");
        var exactCycle = ReadPlain214<CyclePolicy42>(Path.Combine(lab.Root, "data/experiments/speedway222/cycle/cycle-freeze.json"));
        var sliced = Slice42(parent, SecondExit44(lab) + exactCycle.SwitchAfterExitMeters);
        var original = exactCycle.Policy;
        var ablations = new[]
        {
            ("matched 301", original),
            ("wider lookahead line only", original with { Line = original.Line with { Lane = 8.25 } }),
            ("earlier setup only", original with { Line = original.Line with { PreparationMeters = original.Line.PreparationMeters + 13 } }),
            ("less speed demand only", original with { Line = original.Line with { CornerRequestKph = original.Line.CornerRequestKph - 6 } }),
            ("line and heading preconditioning", original with
               { EntryShape = 4, Line = original.Line with { Lane = 8.25, PreparationMeters = original.Line.PreparationMeters + 9 } })
        };
        var matched = ablations.AsParallel().AsOrdered().WithDegreeOfParallelism(5).Select(x =>
        {
            var r = ExactLink40(lab, g, Ride42(lab, g, parent.Seed, sliced.At, sliced.Prefix, sliced.Distances, sliced.Commands,
                x.Item2, Frame.LapMeters + 55));
            var f = r.Flight;
            var run = new Fork40(x.Item1, r.Root, f, r.Commands);
            var points = new[] { -38d, -24, -12, -6, 0, 6, 12, 24, 36 }.Select(d => d + Frame.LapMeters)
                .Where(s => s <= f.Final.Distance).Select(s => AtPhase43(lab, g, run, s)).ToArray();
            return new
            {
                Name = x.Item1, Policy = x.Item2, r.Fork, Pending = r.EarlierCommands[r.Fork.State.Next..],
                Tail = PhaseAt43(lab, g, run, f.Steps.Length - 1), f.Stop, f.Boundary, f.Ground,
                States = points, ActionCount = r.Decisions.Length
            };
        }).ToArray();
        Save(Path.Combine(output, "matched-seam.json"), matched);

        TrackerSet44(lab, output, queued: false);
    }

    private static void SpeedReferences44(Lab38 lab, string output)
    {
        var g = lab.Geometry(true);
        double entry = lab.Bend.NextEntry - lab.Bend.Entry;
        string[] names = ["speedway222/frontier/speed-frontier", "speedway221/links/direct-12", "speedway223/speed-highest"];
        var receipts = names.AsParallel().AsOrdered().WithDegreeOfParallelism(3).Select(name =>
        {
            var m = Archived44(lab, name, name);
            var f = m.Flight;
            var saved = Read199<Evidence39>(ArchivePath43(lab, name)).Rows[^1].State;
            var stations = new[] { -38d, -28, -18, -12, -8, -4, 0, 4, 6, 8, 12, 17, 19 }.Select(x => entry + x)
                .Where(x => x <= f.Final.Distance);
            var samples = stations.Select(s => Probe(Index43(f.Distances, s))).Append(Probe(f.Steps.Length - 1)).ToArray();
            return new { Name = name, ExactTerminal = f.Final.State == saved,
                PositionDifference = (f.Final.State.Center - saved.Center).Norm, f.Stop, f.Final, f.Boundary, f.Ground, Samples = samples };

            object Probe(int i)
            {
                var r = f.Steps[i];
                var phase = PhaseAt43(lab, g, m, i);
                var v = r.Midpoint.Momentum.Plane / g.Base.Mass;
                var dv = (r.After.Momentum - r.Before.Momentum).Plane / (g.Base.Mass * Duration215(r));
                return new
                {
                    RelativeEntry = f.Distances[i] - entry, State = phase, Speed = Speed38(r.After, g),
                    MeasuredCurvature = v.Cross(dv) / Math.Max(.001, Math.Pow(v.Norm, 3)),
                    LongitudinalAcceleration = v.Dot(dv) / Math.Max(.001, v.Norm),
                    Authority = ProjectForce42(g, r.After, r.Forces), Edge = H36Footprint.Clearance(lab.Track, g, r.After, false),
                    Ground = H36Footprint.GroundMargin(g, r.After), Slip = r.Forces.RearSlip,
                    FrontUse = r.Forces.Front.Utilization, RearUse = r.Forces.Rear.Utilization
                };
            }
        }).ToArray();
        Save(Path.Combine(output, "matched-speed-references.json"), new { Entry = entry, References = receipts, NativeWorlds = 0 });
    }

    private static void TrackerSet44(Lab38 lab, string output, bool queued)
    {
        var g = lab.Geometry(true);
        var cases = Enumerable.Range(0, 5).AsParallel().AsOrdered().WithDegreeOfParallelism(5)
            .Select(i => TrackerProbe44(lab, g, i, queued)).ToArray();
        Save(Path.Combine(output, queued ? "queued-tracker.json" : "matched-tracker.json"), cases);
    }

    private static object TrackerProbe44(Lab38 lab, Geometry215 g, int id, bool queued = false)
    {
        string label = id == 0 ? "nominal" : $"holdout-{id}";
        var path = ArchivePath43(lab, "speedway223/feedback-" + label + "-decisions");
        var archive = Read199<TrackerArchive44>(path);
        var model = lab.Engine(g);
        var state = ReplayTape38(lab, g, archive.Seed, archive.Tape, archive.Final.State.Time, exact: true);
        var motion = new Fork40(label, archive.Seed, state, archive.Tape);
        var oldDecision = archive.Decisions[Math.Max(0, archive.Decisions.Length - (id < 3 ? 5 : 8))];
        double offset = queued ? .020 : 0;
        int i = CommandState43(motion, oldDecision.Time + offset);
        var checkpoint = new Checkpoint38(state.Steps[i].After, state.Distances[i]);
        var tape = archive.Tape.TakeWhile(x => x.Requested < checkpoint.State.Time - 1e-9).ToArray();
        if (queued && tape.Length <= checkpoint.State.Next)
            throw new InvalidOperationException("The queued comparison must have a pending request.");
        var reference = checkpoint.State.Command;
        var route = Plan44(lab, id * 7 + 8, false);
        var pick = Choose44(lab, g, model, H36Footprint, checkpoint, state.Steps[i].Forces, tape, route);
        var chosen = H36Control(checkpoint.State, g, model, lab.Course.Mark(checkpoint.State, g),
            lab.Bend.Entry + checkpoint.Distance, Bend44(lab, checkpoint.Distance, 40),
            new Tune36(7.2, 25, 20, 13, .035, .020, -.020, .45, .25, true), 72, .16f, tape, lab.Course);
        var actions = new[] { ("held active", (Command215?)reference), ("H36 balance reference", chosen.Command),
            ("H43 phase candidate", pick.Command), ("delayed torque release", reference with { Bike = reference.Bike with { Drive = 0f } }) };
        var trials = actions.SelectMany(action => new[] { .120, .240, .360 }.Select(seconds =>
        {
            var q = action.Item2 is { } command ? Submit38(checkpoint, tape, command) : tape;
            var r = Window38(lab, g, model, checkpoint, q, seconds, exact: true);
            return new { Action = action.Item1, Command = action.Item2, Seconds = seconds, ResponseSeconds = seconds - ReactionSeconds216,
                r.Final, r.Stop, r.Boundary, r.Ground, Steps = r.Steps.Length };
        })).ToArray();
        return new { Name = label, Matched = checkpoint, RequestedAtOffsetSeconds = offset,
            Pending = tape[checkpoint.State.Next..], ParentDecision = oldDecision,
            H43 = pick.Note, Result = trials, OriginalFailure = archive.Decisions[^1] };
    }
}
