using System.Text.Json;

namespace SpeedwayHandling;

internal static partial class Study191
{
    private static readonly long[] Mesh46 = [Clock46.StockTicks, Clock46.StockTicks / 2, Clock46.StockTicks / 4];
    private sealed record MatchedFile46(MatchedState46[] Cases);
    private sealed record MatchedState46(Checkpoint38 Checkpoint, ProbeAct46[] Independent);
    private sealed record ProbeAct46(Command215 cmd);

    private static void MatchedDrive46(Lab38 lab, Geometry215 g, string output)
    {
        var reference = Read199<Evidence39>(Source46(lab, 224, "recourse-selected.json.gz"));
        var matched = Read199<MatchedFile46>(Source46(lab, 225, "matched-limits.json.gz"));
        var cases = matched.Cases.AsParallel().AsOrdered().WithDegreeOfParallelism(5).Select(c =>
        {
            var at = c.Checkpoint;
            long t = Clock46.Tick(at.State.Time);
            var offered = c.Independent.Select(q => q.cmd).Where(cmd => cmd.Bike.Drive >= .999f);
            var prefix = reference.Commands.Where(q => q.Requested < at.State.Time - 1e-10).ToArray();
            var clock = new ClockPlan46(t, Clock46.StockTicks, t + Clock46.Tick(.240), null, true);
            var results = offered.Select(command =>
            {
                Activation215[] tape = [.. prefix, Clock46.Command(t, command)];
                var experiment = new ReferenceTape46("matched 240 ms", at, tape, Clock46.Seconds(clock.EndTick), null,
                    "Study 225 matched-limits");
                var r = ReplayCanonical46(lab, g, experiment, clock);
                var f = r.Motion.Flight;
                return new { command, f.Final, Reached = Clock46.Tick(f.Final.State.Time) == clock.EndTick,
                    f.Stop, f.Boundary, f.Ground, r.SweepRetries, Motion = Measure38(f.Steps, g, f.Stop),
                    Rejected = DescribeRejection45(g, r.Rejected) };
            }).ToArray();
            return new { at, Pending = prefix.Skip(at.State.Next).ToArray(), Results = results };
        }).ToArray();
        Save(Path.Combine(output, "matched-drive.json.gz"), new
        {
            FormerSafe = 17, NowSafe = cases.Count(c => c.Results.Any(r => r.Reached)),
            PhysicalReactionSeconds = ReactionSeconds216, HorizonSeconds = .240, Cases = cases
        }, true);
    }

    private static void GroundAudit46(Lab38 lab, Geometry215 g, string output)
    {
        var cases = new[] { 475, 476, 477 }.Select(id =>
        {
            var path = Source46(lab, 225, $"discrepancy-{id}-tape.json.gz");
            var stored = Read199<JsonElement>(path);
            var q = stored.GetProperty("Rejected");
            var state = q.GetProperty("Start").GetProperty("State").Deserialize<State215>(Json)!;
            var terminal = q.GetProperty("Rejected").GetProperty("State").Deserialize<State215>(Json)!;
            var tape = stored.GetProperty("Tape").Deserialize<Activation215[]>(Json)!;
            var at = new Checkpoint38(state, q.GetProperty("Station").GetDouble());
            double duration = q.GetProperty("RequestedStep").GetDouble();
            var solve = new SolveTrace46();
            var large = lab.Engine(g).Step(state, tape, duration, audit: solve);
            double bound = H36Footprint.TransportBound(g, large, groundOnly: true);
            var input = new ReferenceTape46($"peg {id}", at, tape, terminal.Time, null, path);
            var meshes = Mesh46.Concat([Clock46.StockTicks / 8]).Select(step =>
            {
                var clock = PlanClock46(input, step);
                var run = ReplayCanonical46(lab, g, input, clock);
                var flight = run.Motion.Flight;
                return new { step, Reached = Clock46.Tick(flight.Final.State.Time) == clock.EndTick,
                    flight.Final, flight.Stop, flight.Ground, flight.Boundary,
                    Rows = flight.Steps.Select(s => new
                    {
                        s.After.Time, s.After.Pose.Roll, s.After.Pose.Pitch,
                        Mid = HardParts46(g, s.Midpoint), End = HardParts46(g, s.After),
                        Lower = Math.Min(H36Footprint.GroundMargin(g, s.Before), H36Footprint.GroundMargin(g, s.After))
                            - .5 * H36Footprint.TransportBound(g, s, groundOnly: true)
                    }), Rejected = DescribeRejection45(g, run.Rejected) };
            }).ToArray();
            var refinedClock = PlanClock46(input, refine: true);
            var refined = ReplayCanonical46(lab, g, input, refinedClock);
            return new { id, Stock = new { q, Reproduced = large.After == terminal, Solve = solve, bound,
                    Start = HardParts46(g, large.Before), Mid = HardParts46(g, large.Midpoint), End = HardParts46(g, large.After) },
                Meshes = meshes, Adaptive = new { refined.Motion.Flight.Final, refined.Motion.Flight.Stop, refined.Motion.Flight.Ground,
                    refined.Motion.Flight.Boundary, refined.SweepRetries, refined.Schedule, Rejected = DescribeRejection45(g, refined.Rejected) } };
        }).ToArray();
        Save(Path.Combine(output, "swept-ground.json.gz"), new { Cases = cases, Boundary = SpatialBike36.RequiredSweptMeters,
            MinimumGroundStrictlyPositive = true, GroundRetries46, SmallestClearanceRetrySeconds46 }, true);
    }

    private static object HardParts46(Geometry215 g, State215 state)
    {
        string[] names = ["engine", "rear frame", "upper frame", "right peg", "left peg", "front fork", "handlebar"];
        return H36Footprint.Parts(g, state).Where(p => p.GroundHard)
            .Select((p, n) => new { Part = names[n], p.Bottom }).OrderBy(p => p.Bottom).ToArray();
    }

    private static void Airborne46(Lab38 lab, Geometry215 g, string output)
    {
        var history = Read199<Evidence39>(Source46(lab, 224, "initial-four.json.gz"));
        double ends = history.Rows[^1].State.Time + .025;
        var cases = new[] { 725d, 737.7 }.Select(at =>
        {
            var row = history.Rows.First(s => s.ReferenceMeters >= at);
            var state = new Checkpoint38(row.State, row.ReferenceMeters);
            var proof = new ReferenceTape46($"airborne from {at}", state, history.Commands, ends, null, "initial-four");
            var runs = Mesh46.Select(step =>
            {
                var clock = PlanClock46(proof, step, refine: true);
                var run = ReplayCanonical46(lab, g, proof, clock);
                var f = run.Motion.Flight;
                var rows = f.Steps.Select((s, i) => AirRow46(g, s, f.Distances[i])).ToArray();
                var air = f.Steps.Where(s => s.Forces.Front.Normal == 0 && s.Forces.Rear.Normal == 0).ToArray();
                var contacts = f.Steps.SelectMany(s => s.Contacts.Select(c => new
                {
                    Time = s.Before.Time + c.Seconds, Kind = c.Foot ? "foot" : c.Front ? "front" : "rear", c.Touchdown
                })).ToArray();
                return new { StepMilliseconds = 1000 * Clock46.Seconds(step), f.Final, f.Stop, f.Ground, f.Boundary,
                    DoubleAirSeconds = air.Sum(s => s.Plan.Duration), FirstDoubleAir = air.FirstOrDefault()?.Before.Time,
                    LastDoubleAir = air.LastOrDefault()?.After.Time,
                    PeakRearLoad = f.Steps.Max(s => s.Forces.Rear.Normal), PeakVerticalMomentum = f.Steps.Max(s => s.After.Momentum.Z),
                    PeakPitch = f.Steps.Max(s => s.After.Pose.Pitch), Contacts = contacts,
                    Balance = Balance46(f, g), Rejected = DescribeRejection45(g, run.Rejected), Rows = rows };
            }).ToArray();
            Connection46? connection = null;
            var old = ReplayTape38(lab, g, state, history.Commands, ends, exact: true,
                rejected: r => connection = new(r, DescribeRejection45(g, r)));
            var historical = new { old.Final, old.Stop, Balance = Balance46(old, g), Rejected = connection?.Description,
                Rows = old.Steps.Select((s, i) => AirRow46(g, s, old.Distances[i])) };
            return new { Seed = state, Meshes = runs, LocalHistoricalReplay = historical,
                Pending = history.Commands.Skip(state.State.Next).TakeWhile(q => q.Requested <= state.State.Time + 1e-12) };
        }).ToArray();

        var archived = history.Rows.Where(r => r.ReferenceMeters >= 724.9).ToArray();
        Save(Path.Combine(output, "airborne-exit.json.gz"), new
        {
            Original = archived, history.Seed, history.Commands, Cases = cases, Gravity216,
            SpringPerWheelNPerM = UnifiedModel215.Stiffness, DamperPerWheelNsPerM = UnifiedModel215.Damping
        }, true);
    }

    private sealed record Connection46(RejectedStep45 Rejected, object? Description);

    private static object AirRow46(Geometry215 g, Step215 s, double station)
    {
        var f = s.Forces;
        var pads = f.PassiveSupport;
        double energy(Tyre215 a) => .5 * UnifiedModel215.Stiffness * Math.Pow(Math.Max(0, -a.Patch.Gap), 2);
        return new { Station = station, s.After, s.Plan.Duration, s.Contacts, s.Subdivisions,
            s.After.Pose.Roll, s.After.Pose.Pitch, PitchRate = g.Motion(s.After).BodyRate.Y,
            VerticalMomentum = s.After.Momentum.Z,
            VerticalAcceleration = (s.After.Momentum.Z - s.Before.Momentum.Z) / (g.Base.Mass * s.Plan.Duration),
            Front = f.Front.Normal, Rear = f.Rear.Normal, Foot = f.LeftFoot?.Contact.NormalForce,
            FrontGap = f.Front.Patch.Gap, RearGap = f.Rear.Patch.Gap,
            FrontSpringJ = energy(f.Front), RearSpringJ = energy(f.Rear), FrontDampingW = f.Front.NormalLoss,
            RearDampingW = f.Rear.NormalLoss, FrontGapRate = f.Front.Patch.NormalSpeed, RearGapRate = f.Rear.Patch.NormalSpeed,
            Seat = pads?.Seat.Force, Pegs = pads is null ? null : new[] { pads.LeftPeg.Force, pads.RightPeg.Force },
            Grips = pads is null ? null : new[] { pads.LeftGrip.Force, pads.RightGrip.Force },
            PadSpringJ = pads?.Energy, PadDissipationW = pads?.Loss,
            s.ActuatorPower, MusclePowerAbs = f.LeftFoot?.AbsoluteMusclePower ?? Math.Abs(s.ActuatorPower),
            f.Rider, RiderRate = g.Motion(s.After).RiderRate, s.After.RiderMomentum, s.RollPitchYawMoment,
            s.EnergyResidual, s.EnergyBefore, s.EnergyAfter };
    }
}
