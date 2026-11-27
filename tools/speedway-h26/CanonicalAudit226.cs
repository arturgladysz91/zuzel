using System.Text.Json;

namespace SpeedwayHandling;

internal static partial class Study191
{
    private sealed record Observed46(Fork40 Motion, LegacyStep46[] Steps);

    private static void LegacyAudit46(Lab38 lab, Geometry215 g, string output, string[] args)
    {
        var references = References46(lab).Where(x => args.Length == 0 || args.Contains(x.Name)).ToArray();
        bool held = references.Any(x => x.Name.Contains("held", StringComparison.Ordinal));
        Input42? book = held ? MatchInputs42(lab) : null;
        Policy42? carrier = held ? ReadPlain214<Qualification42>(
            Source46(lab, 222, "qualification/qualification.json")).Policy : null;
        var seeds = ReadPlain214<Freeze45>(Source46(lab, 225, "feedback-freeze.json")).Starts;
        var prior43 = ObserveArchive("initial-four.json.gz", 224);
        var prior44 = ObserveArchive("qualification/nominal.json.gz", 222);
        var outcomes = references.AsParallel().AsOrdered().WithDegreeOfParallelism(Math.Min(5, Environment.ProcessorCount))
            .Select(reference =>
            {
                Observed46 basis = reference.Name.StartsWith("H43", StringComparison.Ordinal) ? prior43 : prior44;
                if (reference.Name.Contains("held", StringComparison.Ordinal))
                {
                    int n = reference.Name[^1] - '1';
                    var trail = new List<LegacyStep46>();
                    var first = FirstArc42(lab, g, book!, seeds[n], trail.Add);
                    var arcTrail = trail.Take(first.Flight.Steps.Length).ToList();
                    var run = Adaptive42(lab, g, first, book!.Carrier, carrier!, observed: trail.Add);
                    // The carrier begins from an earlier accepted point of the source arc.
                    var carry = trail.Skip(arcTrail.Count).ToArray();
                    double starts = carry.Length == 0 ? run.Flight.Final.State.Time : carry[0].State.Time;
                    var notes = arcTrail.Where(t => t.Step.After.Time <= starts + 1e-12)
                        .Concat(carry).ToArray();
                    basis = new(new(reference.Name, run.Root, run.Flight, run.Commands), notes);
                }

                var online = reference.Handoff is null ? null : TapedOnline46(lab, g, reference, basis);
                var full = new List<LegacyStep46>();
                RejectedStep45? rejection = null;
                var replay = ReplayTape38(lab, g, reference.Seed, reference.Tape, reference.Until, exact: true,
                    endDistance: reference.Finish ?? double.PositiveInfinity, rejected: x => rejection = x, observed: full.Add);
                object? differences = online is { } available ? Compare46(lab, g, reference, available.Steps, full.ToArray()) : null;
                var outcome = new
                {
                    reference.Name, reference.Archive, reference.Seed, reference.Handoff,
                    Replay = new { replay.Final, replay.Stop, replay.Boundary, replay.Ground, Samples = replay.Steps.Length },
                    Online = online is null ? null : new { online.Motion.Flight.Final, online.Motion.Flight.Stop,
                        online.Motion.Flight.Boundary, online.Motion.Flight.Ground, Samples = online.Motion.Flight.Steps.Length },
                    Difference = differences, Rejected = DescribeRejection45(g, rejection),
                    QueueDigest = Hash46(reference.Tape), reference.Finish,
                    Runtime = System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription
                };
                Console.WriteLine($"{reference.Name}: historical replay {replay.Final.Distance:F3} m, review-window {online?.Motion.Flight.Final.Distance:F3} m");
                Save(Path.Combine(output, reference.Name + "-legacy.json.gz"), outcome, true);
                return new { reference.Name, ReplayStation = replay.Final.Distance, ReplayStop = replay.Stop,
                    OnlineStation = online?.Motion.Flight.Final.Distance, OnlineStop = online?.Motion.Flight.Stop };
            }).ToArray();
        Save(Path.Combine(output, "legacy-summary.json"), new { Base = Base46, Outcomes = outcomes,
            Configuration = Config46(lab, g, PlanClock46(references[0])) });

        Observed46 ObserveArchive(string name, int study)
        {
            var e = Read199<Evidence39>(Source46(lab, study, name));
            var seed = new Checkpoint38(e.Seed, -PreparationStart37);
            var steps = new List<LegacyStep46>();
            var flight = ReplayTape38(lab, g, seed, e.Commands, e.Rows[^1].State.Time, exact: true, observed: steps.Add);
            return new(new(name, seed, flight, e.Commands), steps.ToArray());
        }
    }

    // The archived future actions are applied only when their original review was reached.
    // Each old online window is a requested 60 ms, as in Ride44/Ride45.
    private static Observed46 TapedOnline46(Lab38 lab, Geometry215 g, ReferenceTape46 tape, Observed46 basis)
    {
        var model = lab.Engine(g);
        double fork = tape.Handoff!.Value;
        if (basis.Motion.Flight.Final.Distance < fork - .001)
            return basis;
        var (at, steps, stations, issued) = Slice42(basis.Motion, fork);
        var notes = basis.Steps.Where(s => s.Step.After.Time <= at.State.Time + 1e-12).ToList();
        var trail = new List<Step215>(steps);
        var distances = new List<double>(stations);
        double first = at.State.Time, boundary = basis.Motion.Flight.Boundary, hard = basis.Motion.Flight.Ground;
        string stop = Continuing46;
        double requestClockError = 0;
        var actions = tape.Tape.Where(x => x.Requested >= first - 1e-10).ToArray();
        for (int i = 0; i < actions.Length && stop == Continuing46 && at.Distance < (tape.Finish ?? double.PositiveInfinity); i++)
        {
            var action = actions[i];
            requestClockError = Math.Max(requestClockError, Math.Abs(action.Requested - at.State.Time));
            if (requestClockError > 1e-8)
            {
                stop = "recorded review time no longer coincides with the online state";
                break;
            }
            issued = [.. issued, action];
            var f = Window38(lab, g, model, at, issued, Cadence38, exact: true,
                finishAt: tape.Finish ?? double.PositiveInfinity, observed: notes.Add);
            trail.AddRange(f.Steps);
            distances.AddRange(f.Distances);
            at = f.Final;
            boundary = Math.Min(boundary, f.Boundary);
            hard = Math.Min(hard, f.Ground);
            stop = f.Stop;
        }
        return new(new(tape.Name, tape.Seed, new(at, trail.ToArray(), distances.ToArray(), stop, boundary, hard), issued),
            notes.ToArray());
    }

    private static object Compare46(Lab38 lab, Geometry215 g, ReferenceTape46 reference, LegacyStep46[] a, LegacyStep46[] b)
    {
        int rows = Math.Min(a.Length, b.Length);
        int input = -1, dt = -1, physical = -1;
        var growth = new List<object>();
        double nextSample = double.NegativeInfinity;
        int ib = 0;
        for (int i = 0; i < rows; i++)
        {
            if (input < 0 && !SameBits46(a[i].Requested, b[i].Requested)) input = i;
            if (dt < 0 && !SameBits46(a[i].Step.Plan.Duration, b[i].Step.Plan.Duration)) dt = i;
            if (physical < 0 && a[i].Step.After != b[i].Step.After)
            {
                physical = i;
                nextSample = a[i].Step.After.Time;
            }
            if (a[i].Step.After.Time < nextSample || physical < 0) continue;
            while (ib + 1 < b.Length && Math.Abs(b[ib + 1].Step.After.Time - a[i].Step.After.Time) <
                Math.Abs(b[ib].Step.After.Time - a[i].Step.After.Time)) ib++;
            var aa = a[i].Step.After;
            var bb = b[ib].Step.After;
            growth.Add(new { Time = aa.Time, ReplayTime = bb.Time, a[i].Station,
                PositionM = (aa.Center - bb.Center).Norm, Momentum = (aa.Momentum - bb.Momentum).Norm,
                Attitude = (aa.Pose.V - bb.Pose.V).Norm, Angular = (aa.Angular - bb.Angular).Norm,
                Rider = (aa.RiderMomentum - bb.RiderMomentum).Norm,
                ActiveCommandsEqual = aa.Command == bb.Command, BothCursors = new[] { aa.Next, bb.Next },
                OnlineSubdivisions = a[i].Step.Subdivisions, ReplaySubdivisions = b[ib].Step.Subdivisions,
                OnlineFront = a[i].Step.Forces.Front.Normal, ReplayFront = b[ib].Step.Forces.Front.Normal,
                OnlineRear = a[i].Step.Forces.Rear.Normal, ReplayRear = b[ib].Step.Forces.Rear.Normal });
            nextSample += .12;
        }

        object? Pair(int index) => index < 0 ? null : new { Index = index, Online = Detail(a[index]), Replay = Detail(b[index]) };

        object Detail(LegacyStep46 observation)
        {
            var o = observation;
            var calculator = lab.Engine(g);
            var audit = new SolveTrace46();
            Step215? record = null;
            string? error = null;
            try { record = calculator.Step(o.State, reference.Tape, o.Requested, audit: audit); }
            catch (InvalidOperationException failure) { error = failure.Message; }
            return new { o.State, o.Station, o.WindowEnd, o.Requested,
                RequestedBits = BitConverter.DoubleToInt64Bits(o.Requested),
                Executed = o.Step.Plan.Duration, ExecutedBits = BitConverter.DoubleToInt64Bits(o.Step.Plan.Duration),
                After = o.Step.After, o.Step.Subdivisions, o.Step.Contacts, Audit = audit,
                LocalReproduction = record?.After == o.Step.After, LocalError = error,
                Active = o.Step.Before.Command, Queue = reference.Tape.Skip(o.State.Next)
                    .TakeWhile(x => x.Requested <= o.State.Time + 1e-10).ToArray() };
        }

        return new { Compared = rows, FirstDifferentRequested = Pair(input), FirstDifferentExecuted = Pair(dt),
            FirstDifferentState = Pair(physical), Growth = growth };
    }

    private sealed record ProcessInput46(ReferenceTape46 Reference, ClockPlan46 Clock, RunDigest46 Expected,
        Stamp46[] Steps);

    private static void Determinism46(Lab38 lab, Geometry215 g, string output, string[] args)
    {
        var names = args.Length == 0 ? new[] { "A225", "B615", "H43-476" } : args;
        var inputs = References46(lab).Where(x => names.Contains(x.Name)).ToArray();
        if (inputs.Length == 0) throw new ArgumentException("No matching reference tape.");
        var cases = inputs.AsParallel().AsOrdered().WithDegreeOfParallelism(Math.Min(4, Environment.ProcessorCount))
            .Select(input =>
            {
                var clock = PlanClock46(input, refine: true);
                var online = ReplayCanonical46(lab, g, input, clock);
                var baseline = Digest46(lab, g, clock, online);
                string name = input.Name;
                Save(Path.Combine(output, name + "-process-input.json.gz"), new ProcessInput46(input, clock, baseline, online.Schedule), true);
                var played = ReplayCanonical46(lab, g, input, clock, online.Schedule);
                var scheduled = Digest46(lab, g, clock, played);
                int cut = Math.Max(1, online.Schedule.Length / 2);
                if (cut >= online.Schedule.Length) throw new InvalidOperationException("Checkpoint demonstration needs an accepted interval on each side.");
                var f = online.Motion.Flight;
                var checkpoint = new Check46(new(f.Steps[cut - 1].After, f.Distances[cut - 1]), online.Motion.Tape, clock);
                var options = new JsonSerializerOptions(Json) { WriteIndented = true };
                var restored = JsonSerializer.Deserialize<Check46>(JsonSerializer.SerializeToUtf8Bytes(checkpoint, options), options)
                    ?? throw new InvalidOperationException("H45 checkpoint serialization failed");
                var resumed = ReplayCanonical46(lab, g, input with { Tape = restored.Prefix }, restored.Plan,
                    resume: restored.State);
                bool exactSuffix = online.Schedule[cut..].SequenceEqual(resumed.Schedule) &&
                    f.Steps[cut..].Select(s => s.After).SequenceEqual(resumed.Motion.Flight.Steps.Select(s => s.After)) &&
                    f.Distances[cut..].SequenceEqual(resumed.Motion.Flight.Distances);
                var commandOnly = ReplayCanonical46(lab, g, input, clock);
                var independent = Digest46(lab, g, clock, commandOnly);
                if (baseline != independent || baseline != scheduled || !exactSuffix)
                    throw new InvalidOperationException($"H45 same-input determinism failed for {name}");
                Save(Path.Combine(output, name + "-canonical.json.gz"), new
                {
                    Input = input, Clock = clock, baseline, independent, scheduled, exactSuffix, Checkpoint = restored,
                    ActiveAndPending = Pending43(online.Motion, restored.State, true), online.SweepRetries,
                    Energy = Balance46(f, g), Rejected = DescribeRejection45(g, online.Rejected),
                    States = f.Steps.Select((s, i) => new { s.After, Station = f.Distances[i], s.Contacts,
                        Front = s.Forces.Front.Normal, Rear = s.Forces.Rear.Normal, s.EnergyResidual })
                }, true);
                Console.WriteLine($"{name}: same {baseline.Steps} steps in all checks, {baseline.End.Distance:F3} m ({baseline.Stop})");
                return new { Name = name, baseline, exactSuffix, online.IntegrationCpu,
                    IndependentCommandReplay = baseline == independent, RecordedStepReplay = baseline == scheduled };
            }).ToArray();
        Save(Path.Combine(output, "determinism-summary.json"), new { Base = Base46, Contract = Contract46, Cases = cases });
    }

    private static void ProcessReplay46(Lab38 lab, Geometry215 g, string output, string[] args)
    {
        if (args.Length != 1) throw new ArgumentException("Provide a process-input.json.gz path.");
        var e = Read199<ProcessInput46>(args[0]);
        var local = ReplayCanonical46(lab, g, e.Reference, e.Clock);
        var result = Digest46(lab, g, e.Clock, local);
        Save(Path.Combine(output, e.Reference.Name + "-independent-process.json"), new
        {
            Expected = e.Expected, Result = result, Equal = result == e.Expected,
            WorkerCount = 1, Runtime = System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription,
            System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture
        });
        if (result != e.Expected) throw new InvalidOperationException("Independent process differs.");
    }

    private static object Balance46(Flight38 flight, Geometry215 g)
    {
        double p = 0, h = 0;
        foreach (var s in flight.Steps)
        {
            var f = s.Forces;
            var external = f.GroundForce + f.Drag - Vec215.Up * (g.Base.Mass * Gravity216);
            p = Math.Max(p, (s.After.Momentum - s.Before.Momentum - external * s.Plan.Duration).Norm);
            h = Math.Max(h, (s.After.Angular - s.Before.Angular - s.RollPitchYawMoment * s.Plan.Duration).Norm);
        }
        return new { MaximumStepImpulseError = p, MaximumStepAngularImpulseError = h, Energy = Energy45(flight) };
    }
}
