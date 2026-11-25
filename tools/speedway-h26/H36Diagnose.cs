
using System.Diagnostics;
using SharedWorldSpeedway;
using WholeCorner171;

namespace SpeedwayHandling;

internal static partial class Study191
{
    private sealed record HorizonProof36(string Label, double HorizonSeconds, State215 Initial, State215 Final,
        double ReferenceBefore, double ReferenceAfter, double MinimumSwept, double GroundMargin, string Result,
        int Steps, int ForecastSteps, Step215[] Trace);

    private static void H36Diagnose(string root, string output, Fixture216 fixture, CornerCourse216 course,
        RacingLineGeometry track)
    {
        var timer = Stopwatch.StartNew();
        using var process = Process.GetCurrentProcess();
        var cpu = process.TotalProcessorTime;
        string priorPath = Path.Combine(root, "data/experiments/speedway217");
        var seeds = ReadPlain214<Seed217[]>(Path.Combine(priorPath, "seeds.json"));
        var diagnoses = new List<object>();
        var tests = new List<HorizonProof36>();
        foreach (string name in new[] { "80-leg", "90-leg-gate", "120-wheels-gate" })
        {
            var archived = Read199<Run217>(Path.Combine(priorPath, $"trace-{name}-Predictive.json.gz"));
            var seed = seeds.Single(s => s.Name == name);
            var rows = archived.Trace;
            var terminal = rows[^1].After;
            double section = archived.Summary.Rollout.FinalReference;
            var g = new Geometry215(fixture.Base, Axes215.All, seed.Tuning.Leg ? new Leg216() : null, H36Footprint);
            var road = course.Mark(terminal, g);
            var preceding = rows.LastOrDefault(r => r.After.Time <= terminal.Time - .38)?.After ?? seed.State;
            double precedingS = section + Frame.UnwrapDelta(road.Road.ReferenceMeters,
                course.Mark(preceding, g).Road.ReferenceMeters);
            var submitted = seed.Pending.Concat(archived.Decisions.Where(d => d.Chosen.HasValue)
                .Select(d => new Activation215(d.Time, d.Time + ReactionSeconds216, d.Chosen!.Value))).ToArray();
            var old = archived.Decisions.Last(d => d.Preview != null).Preview!;
            var forces = rows[^1].Forces;
            double lookRadius = Frame.RoadAt(Math.Clamp(section + 4, seed.Bend.Entry + .1, seed.Bend.Exit - .1),
                road.Road.OffsetMeters).RadiusMeters;
            double demand = road.Speed * road.Speed / lookRadius;
            double capacity = (forces.Front.Capacity + forces.Rear.Capacity + .32 *
                (forces.LeftFoot?.Contact.NormalForce ?? 0)) / fixture.Base.Mass;
            var options = new List<HorizonProof36>();
            SaveProbe("abstention-short", terminal, section, .24, 0, 0);
            SaveProbe("abstention-long", terminal, section, 1.05, 0, 0);
            SaveProbe("abstention-wide-and-coast", terminal, section, 1.05, 1.65, -22);
            SaveProbe("earlier-same-line", preceding, precedingS, 1.05, 0, 0);
            SaveProbe("earlier-wide-and-coast", preceding, precedingS, 1.45, 1.65, -22);

            diagnoses.Add(new
            {
                Name = name, Timestamp = terminal.Time, Reference = section, LegacyReasons = old.Rejections,
                LegacyAbstention = old.Reason, OriginalClearance = course.Clearance(road),
                ProjectedClearance = H36Footprint.Clearance(track, g, terminal),
                ChassisGround = H36Footprint.GroundMargin(g, terminal),
                Estimate = new { DemandMetersPerSecondSquared = demand, ContactBudgetUpperBound = capacity,
                    SnapshotDeficit = demand > capacity },
                MeasuredAuthority = new { forces.Front.Normal, forces.FrontYawBand, forces.FrontRollBand, RollRate = g.Motion(terminal).BodyRate.X },
                MuscleForce = forces.Rider.Norm + Math.Abs(forces.LeftFoot?.MuscleForce ?? 0),
                MusclePower = forces.LeftFoot?.AbsoluteMusclePower ?? Math.Abs(rows[^1].ActuatorPower),
                Trials = options.Select(o => new { o.Label, o.HorizonSeconds, o.ReferenceBefore, o.ReferenceAfter,
                    o.MinimumSwept, o.GroundMargin, o.Result, o.Steps, o.ForecastSteps }).ToArray()
            });

            void SaveProbe(string label, State215 state, double s, double horizon, double laneDelta, double speedDelta)
            {
                var lane = Math.Clamp(seed.Tuning.Lane + laneDelta, 3.4, 8.2);
                var tune = new Tune36(lane, 11, 24, 8, 0, 0, 0, 0, 0, seed.Tuning.Leg);
                var proof = Prove(label, state, s, seed.Bend, tune,
                    Math.Max(40, 3.6 * state.Momentum.Plane.Norm / g.Base.Mass + speedDelta), g,
                    submitted.Where(x => x.Requested <= state.Time + 1e-10).ToArray(), horizon);
                tests.Add(proof);
                options.Add(proof);
                Save(Path.Combine(output, $"abstention-{name}-{label}.json.gz"), proof, true);
            }
        }

        var terminalChecks = new List<object>();
        foreach (var path in Directory.EnumerateFiles(output, "trajectory-*.json.gz")
                     .Concat(Directory.EnumerateFiles(output, "pace-trajectory-*.json.gz"))
                     .Concat(Directory.EnumerateFiles(output, "trim-trajectory-*.json.gz"))
                     .Concat(Directory.EnumerateFiles(output, "prepare-trajectory-*.json.gz")).Order())
        {
            var archived = Read199<Trace36>(path);
            if (!archived.Summary.Balanced) continue;
            var g = new Geometry215(fixture.Base, Axes215.All, archived.Summary.Tune.Leg ? new Leg216() : null, H36Footprint);
            var state = archived.Trace[^1].After;
            var proof = Prove("exit-terminal", state, archived.Summary.Finish.Reference, Map199.At(2, 0), archived.Summary.Tune,
                archived.Summary.Finish.Kph, g, archived.Commands, .70);
            tests.Add(proof);
            string artifact = Path.GetFileName(path).Replace("trajectory-", "terminal-", StringComparison.Ordinal);
            Save(Path.Combine(output, artifact), proof, true);
            terminalChecks.Add(new { Source = Path.GetFileName(path), Speed = archived.Summary.NominalEntryKph, proof.ReferenceBefore, proof.ReferenceAfter,
                proof.MinimumSwept, proof.GroundMargin, proof.Result, proof.Final.Pose.Roll });
        }
        Save(Path.Combine(output, "diagnosis-h36.json"), new
        {
            Representative = diagnoses, Terminal = terminalChecks, Steps = tests.Sum(t => t.Steps),
            ForecastSteps = tests.Sum(t => t.ForecastSteps), CpuSeconds = (process.TotalProcessorTime - cpu).TotalSeconds,
            WallSeconds = timer.Elapsed.TotalSeconds
        });

        HorizonProof36 Prove(string name, State215 initial, double startS, BendVisit bend, Tune36 tune, double speed,
            Geometry215 g, Activation215[] original, double horizon)
        {
            var model = fixture.Engine(g, NominalSupport216, Actuator215.Limited, true);
            var state = initial;
            var queue = original;
            var trace = new List<Step215>();
            var mark = course.Mark(state, g);
            double s = startS, next = state.Time, end = state.Time + horizon;
            double clearance = H36Footprint.Clearance(track, g, state), minimum = clearance;
            double ground = H36Footprint.GroundMargin(g, state);
            int forecast = 0;
            string stop = "verified horizon";
            while (state.Time < end - 1e-9)
            {
                if (state.Time >= next - 1e-10)
                {
                    var proposal = H36Control(state, g, model, mark, s, bend, tune, speed, .16f, queue, course);
                    forecast += proposal.Steps;
                    if (proposal.Command is not { } command) { stop = proposal.Stop!; break; }
                    queue = [.. queue, new(state.Time, state.Time + ReactionSeconds216, command)];
                    next += H36ControlPeriod;
                }
                try
                {
                    var row = model.Step(state, queue, Math.Min(StepSeconds216, Math.Min(end - state.Time, next - state.Time)));
                    var after = course.Mark(row.After, g);
                    double tail = H36Footprint.Clearance(track, g, row.After);
                    double swept = Math.Min(clearance, tail) - H36Footprint.TransportBound(g, row) / 2;
                    double sweptGround = Math.Min(H36Footprint.GroundMargin(g, row.Before), H36Footprint.GroundMargin(g, row.After)) -
                        H36Footprint.TransportBound(g, row, groundOnly: true) / 2;
                    minimum = Math.Min(minimum, swept);
                    ground = Math.Min(ground, sweptGround);
                    if (sweptGround <= 0) { stop = "chassis swept ground margin"; break; }
                    if (CornerCourse216.Violation(after, swept) is { } violation) { stop = violation; break; }
                    s += Frame.UnwrapDelta(mark.Road.ReferenceMeters, after.Road.ReferenceMeters);
                    state = row.After;
                    mark = after;
                    clearance = tail;
                    trace.Add(row);
                }
                catch (InvalidOperationException e) { stop = e.Message; break; }
            }
            return new(name, horizon, initial, state, startS, s, minimum, ground, stop, trace.Count, forecast, trace.ToArray());
        }
    }
}
