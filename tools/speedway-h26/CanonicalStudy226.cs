using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;

namespace SpeedwayHandling;

internal static partial class Study191
{
    private const string Base46 = "2b46b96889cfa715fc221376b33fb54df6cf90a6";
    private const string Contract46 = "H45 MODEL absolute epochs v1";
    private const int GroundRetries46 = 8;
    private const double SmallestClearanceRetrySeconds46 = 1e-6;
    private const string Continuing46 = "verified interval";

    private sealed record ReviewPlan46(double[] Prefix, double? Anchor)
    {
        internal double Next(double time)
        {
            double after = time + ReviewTolerance46;
            int i = Array.BinarySearch(Prefix, after);
            int index = i >= 0 ? i + 1 : ~i;
            if (index < Prefix.Length) return Prefix[index];
            if (Anchor is not { } origin) return double.PositiveInfinity;
            if (after < origin) return origin;
            var cycle = (long)Math.Floor((after - origin) / Cadence38) + 1;
            double next = origin + cycle * Cadence38;
            return next > after ? next : origin + (cycle + 1) * Cadence38;
        }
    }
    private const double ReviewTolerance46 = 1e-9;
    private sealed record ClockPlan46(long Origin, long StepTicks, long EndTick, double? FinishMeters,
        bool RefineSweep, ReviewPlan46? Review = null, double? RawEnd = null);
    private sealed record Stamp46(long Epoch, double Requested, double AcceptedRequest, double Executed,
        double Time, int EventSplits, int SweepSplits, int Next, double? ReviewEnd = null);
    private sealed record CanonicalRun46(Fork40 Motion, Stamp46[] Schedule, RejectedStep45? Rejected,
        int SweepRetries, double IntegrationCpu, double ControllerCpu, StateDecision44[] Decisions);
    private sealed record RunDigest46(string Schedule, string States, string Dynamics, string Configuration,
        int Steps, Checkpoint38 End, string Stop, double Boundary, double Ground);
    private sealed record Check46(Checkpoint38 State, Activation215[] Prefix, ClockPlan46 Plan);
    private sealed record ReferenceTape46(string Name, Checkpoint38 Seed, Activation215[] Tape, double Until,
        double? Finish, string Archive, double? Handoff = null, Checkpoint38? Fork = null);
    private sealed record LegacyDiscrepancy46(Checkpoint38 Root, Activation215[] Tape, Checkpoint38 Fork,
        Checkpoint38 ScreeningFinal, Checkpoint38 Final);

    private static string Source46(Lab38 lab, int study, string file) =>
        Path.Combine(lab.Root, "data", "experiments", $"speedway{study}", file);

    private static void CanonicalStudy226(string checkout, string output, string mode, string[] args)
    {
        var lab = MakeLinkLab40(checkout);
        var g = lab.Geometry(true);
        switch (mode)
        {
            case "legacy": LegacyAudit46(lab, g, output, args); break;
            case "determinism": Determinism46(lab, g, output, args); break;
            case "process": ProcessReplay46(lab, g, output, args); break;
            case "align": Align46(lab, g, output, args); break;
            case "feedback-aligned": AlignedFeedback46(lab, g, output, args); break;
            case "ground": GroundAudit46(lab, g, output); break;
            case "airborne": Airborne46(lab, g, output); break;
            case "matched": MatchedDrive46(lab, g, output); break;
            case "perturb": Perturb46(lab, g, output, args); break;
            case "feedback": Feedback46(lab, g, output, args); break;
            case "qualify": Qualify46(lab, g, output); break;
            case "runtime": Runtime46(lab, g, output); break;
            default: throw new ArgumentException("Use legacy, determinism, process, align, ground, airborne, matched, perturb, feedback, feedback-aligned, qualify or runtime.");
        }
    }

    private static ReferenceTape46[] References46(Lab38 lab)
    {
        var input = new List<ReferenceTape46>();
        foreach (int id in new[] { 475, 476, 477 })
        {
            string name = $"discrepancy-{id}-tape.json.gz";
            var entry = Read199<LegacyDiscrepancy46>(Source46(lab, 225, name));
            input.Add(new($"H43-{id}", entry.Root, entry.Tape, entry.ScreeningFinal.State.Time, Four45, name,
                entry.Fork.Distance, entry.Fork));
        }
        var longest = Read199<Evidence39>(Source46(lab, 224, "support-certification-selected.json.gz"));
        input.Add(new("H43-longest", new(longest.Seed, -PreparationStart37), longest.Commands, longest.Rows[^1].State.Time,
            null, "support-certification-selected.json.gz"));
        var pairs = new[] { ("A224", "support-exact-224", Four45), ("A225", "best-lap-225", Pair45),
            ("B615", "transition-615", Pair45), ("D802", "sustain-802", Four45),
            ("B615-held-1", "holdout-1", Pair45), ("B615-held-2", "holdout-2", Pair45),
            ("B615-held-3", "holdout-3", Pair45), ("B615-held-4", "holdout-4", Pair45) };
        var freeze = ReadPlain214<Freeze45>(Source46(lab, 225, "feedback-freeze.json"));
        foreach (var (label, file, destination) in pairs)
        {
            string name = file + "-tape.json.gz";
            var e = Read199<Tape45>(Source46(lab, 225, name));
            input.Add(new(label, new(e.Seed, e.SeedStation), e.Commands,
                Math.Max(e.Terminal.Time, e.Commands[^1].Requested + Cadence38), destination, name, freeze.Fork));
        }
        return input.ToArray();
    }

    private static ClockPlan46 PlanClock46(ReferenceTape46 input, long step = Clock46.StockTicks,
        bool refine = false) => new(Clock46.Tick(input.Seed.State.Time), step,
            Clock46.Tick(input.Until), input.Finish, refine);

    private sealed class Propagator46
    {
        private readonly Lab38 lab;
        private readonly Geometry215 geometry;
        private readonly UnifiedModel215 plant;
        private readonly SpatialBike36 shape = H36Footprint;
        private readonly ClockPlan46 plan;
        private readonly Clock46 clock;
        private double edge, floor;
        private int prescribedCursor;
        private readonly Stamp46[]? prescribed;
        internal readonly List<Stamp46> Timetable = [];
        internal readonly List<Step215> Rows = [];
        internal readonly List<double> Stations = [];
        internal readonly List<StateDecision44> Decisions = [];
        internal Checkpoint38 State { get; private set; }
        internal RejectedStep45? Rejected { get; private set; }
        internal double MinimumBoundary { get; private set; }
        internal double MinimumGround { get; private set; }
        internal string Stop { get; set; } = Continuing46;
        internal int SweepRetries { get; private set; }
        internal long PlantCpu;
        internal long DecisionCpu;
        internal UnifiedModel215 Model => plant;
        internal bool Done => Stop != Continuing46 || (plan.RawEnd is { } last
            ? State.State.Time >= last - ReviewTolerance46 : Clock46.Tick(State.State.Time) >= plan.EndTick) ||
            State.Distance >= (plan.FinishMeters ?? double.PositiveInfinity);

        internal Propagator46(Lab38 lab, Geometry215 geometry, ClockPlan46 plan, Checkpoint38 seed,
            Stamp46[]? prescribed = null)
        {
            if (plan.StepTicks is <= 0 or > 5_000_000_000 || Clock46.Tick(seed.State.Time) < plan.Origin ||
                plan.EndTick < Clock46.Tick(seed.State.Time))
                throw new ArgumentOutOfRangeException(nameof(plan));
            this.lab = lab;
            this.geometry = geometry;
            this.plan = plan;
            this.prescribed = prescribed;
            clock = new(plan.Origin, plan.StepTicks);
            plant = lab.Engine(geometry);
            State = seed;
            MinimumBoundary = edge = shape.Clearance(lab.Track, geometry, seed.State, true);
            MinimumGround = floor = shape.GroundMargin(geometry, seed.State);
            if (edge < SpatialBike36.RequiredSweptMeters || floor <= 0)
                Stop = edge < SpatialBike36.RequiredSweptMeters ? "initial swept boundary clearance" : "initial hard-part clearance";
        }

        internal void Advance(Activation215[] commands)
        {
            if (Done) return;
            long began = ThreadTicks43();
            var s = State.State;
            double? reviewEnd = plan.Review is { } review ? Math.Min(review.Next(s.Time), plan.RawEnd!.Value) : null;
            long epoch = reviewEnd is { } boundaryTime ? Clock46.Tick(boundaryTime) :
                Math.Min(clock.Next(Clock46.Tick(s.Time)), plan.EndTick);
            double requested = reviewEnd is { } window ? Math.Min(Clock46.Seconds(plan.StepTicks), window - s.Time) :
                Clock46.Seconds(epoch) - s.Time;
            if (requested <= 0 || requested > .005001)
                throw new InvalidOperationException("Invalid H45 epoch interval");
            if (prescribed is { } schedule && prescribedCursor < schedule.Length)
            {
                var expected = schedule[prescribedCursor];
                if (expected.Epoch != epoch || !SameBits46(expected.Requested, requested))
                    throw new InvalidOperationException($"Recorded and independent clocks disagree at step {prescribedCursor}");
                requested = expected.Requested;
            }
            double candidateDuration = requested;
            var mark = lab.Course.Mark(s, geometry);
            for (int retry = 0; ; retry++)
            {
                Step215 row;
                try { row = plant.Step(s, commands, candidateDuration); }
                catch (InvalidOperationException ex)
                {
                    Reject(ex.Message, candidateDuration, s, (ex as ChassisEnvelope36)?.Candidate, null, null, null);
                    break;
                }
                var tail = lab.Course.Mark(row.After, geometry);
                double nextEdge = shape.Clearance(lab.Track, geometry, row.After, true);
                double nextGround = shape.GroundMargin(geometry, row.After);
                double sweep = Math.Min(edge, nextEdge) - shape.TransportBound(geometry, row) * .5;
                double hard = Math.Min(floor, nextGround) - shape.TransportBound(geometry, row, groundOnly: true) * .5;
                string? violation = hard <= 0 ? "swept hard-part ground" : CornerCourse216.Violation(tail, sweep);
                if (violation is not null)
                {
                    bool boundOnly = hard <= 0 && nextGround > 0 && floor > 0 ||
                        violation == "swept boundary clearance" && nextEdge >= .50 && edge >= .50;
                    if (plan.RefineSweep && boundOnly && retry < GroundRetries46 &&
                        row.Plan.Duration > 2 * SmallestClearanceRetrySeconds46)
                    {
                        SweepRetries++;
                        candidateDuration = row.Plan.Duration / 2;
                        continue;
                    }
                    Reject(violation, candidateDuration, s, row.After, row, sweep, hard);
                    break;
                }

                double distance = State.Distance + Frame.UnwrapDelta(mark.Road.ReferenceMeters, tail.Road.ReferenceMeters);
                var stamp = new Stamp46(epoch, requested, candidateDuration, row.Plan.Duration, row.After.Time,
                    row.Subdivisions, retry, row.After.Next, reviewEnd);
                if (prescribed is { } known && (prescribedCursor >= known.Length || stamp != known[prescribedCursor]))
                    throw new InvalidOperationException($"Recorded and integrated steps disagree at {prescribedCursor}");
                prescribedCursor++;
                if (Clock46.Tick(row.After.Time) > epoch || row.After.Time <= s.Time)
                    throw new InvalidOperationException("H45 epoch was crossed or the clock did not advance");
                State = new(row.After, distance);
                Rows.Add(row);
                Stations.Add(distance);
                Timetable.Add(stamp);
                edge = nextEdge;
                floor = nextGround;
                MinimumBoundary = Math.Min(MinimumBoundary, sweep);
                MinimumGround = Math.Min(MinimumGround, hard);
                break;
            }
            PlantCpu += ThreadTicks43() - began;
        }

        internal void ContinueVerifiedPrefix(CanonicalRun46 prefix, double handoff)
        {
            if (Rows.Count != 0 || State != prefix.Motion.Seed)
                throw new InvalidOperationException("The shared prefix must use the same root.");
            var f = prefix.Motion.Flight;
            State = f.Final;
            Rows.AddRange(f.Steps);
            Stations.AddRange(f.Distances);
            Timetable.AddRange(prefix.Schedule);
            edge = shape.Clearance(lab.Track, geometry, State.State, true);
            floor = shape.GroundMargin(geometry, State.State);
            MinimumBoundary = f.Boundary;
            MinimumGround = f.Ground;
            SweepRetries = prefix.SweepRetries;
            if (State.State.Time < handoff - ReviewTolerance46)
            {
                Stop = f.Stop;
                Rejected = prefix.Rejected;
            }
        }

        private void Reject(string reason, double duration, State215 before, State215? end, Step215? step,
            double? boundary, double? ground)
        {
            Stop = reason;
            Rejected = new(reason, State.Distance, duration, before, end, step, boundary, ground);
        }

        internal CanonicalRun46 Result(string name, Checkpoint38 seed, Activation215[] commands)
        {
            if (prescribed is { } schedule && prescribedCursor != schedule.Length)
                throw new InvalidOperationException("Recorded schedule did not exhaust at the same certified terminal.");
            string reason = Stop != Continuing46 ? Stop :
                State.Distance >= (plan.FinishMeters ?? double.PositiveInfinity) ? "completed requested horizon" : "clock horizon";
            return new(new(name, seed, new(State, Rows.ToArray(), Stations.ToArray(), reason, MinimumBoundary, MinimumGround),
                commands), Timetable.ToArray(), Rejected, SweepRetries, PlantCpu / 1e7, DecisionCpu / 1e7, Decisions.ToArray());
        }
    }

    private static CanonicalRun46 ReplayCanonical46(Lab38 lab, Geometry215 g, ReferenceTape46 recording,
        ClockPlan46 plan, Stamp46[]? schedule = null, Checkpoint38? resume = null)
    {
        var commands = plan.Review is not null ? recording.Tape : recording.Tape.Select(Clock46.Import).ToArray();
        for (int i = 1; i < commands.Length; i++)
            if (commands[i].Requested < commands[i - 1].Requested || commands[i].Time < commands[i - 1].Time)
                throw new InvalidOperationException("H45 request and activation FIFO must each be ordered");
        var plant = new Propagator46(lab, g, plan, resume ?? recording.Seed, schedule);
        while (!plant.Done) plant.Advance(commands);
        return plant.Result(recording.Name, recording.Seed, commands);
    }

    private static bool SameBits46(double a, double b) => BitConverter.DoubleToInt64Bits(a) == BitConverter.DoubleToInt64Bits(b);

    private static string Hash46<T>(IEnumerable<T> items)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        foreach (var item in items)
        {
            hash.AppendData(JsonSerializer.SerializeToUtf8Bytes(item, Json));
            hash.AppendData([0]);
        }
        return Convert.ToHexString(hash.GetHashAndReset());
    }

    private static object Config46(Lab38 lab, Geometry215 g, ClockPlan46 clock) => new
    {
        Name = Contract46, clock, g.Base.Mass, g.Base.RiderMass, g.Axes, g.Neutral, HasLeftFoot = g.Leg is not null,
        g.FrontContact, Geometry = H36Footprint, Grip = Grip42, lab.Fixture.Ratio, lab.Fixture.Release,
        Support = NominalSupport216, Actuator = Actuator215.Limited, Physics, Settings = Settings202("H28-F"),
        PhysicalReactionSeconds = ReactionSeconds216, CadenceSeconds = Cadence38,
        SweptBoundaryMeters = SpatialBike36.RequiredSweptMeters
    };

    private static RunDigest46 Digest46(Lab38 lab, Geometry215 g, ClockPlan46 clock, CanonicalRun46 r)
    {
        var f = r.Motion.Flight;
        return new(Hash46(r.Schedule), Hash46(f.Steps.Select(s => s.After)), Hash46(f.Steps.Select(s =>
                new { s.Plan, s.Contacts, s.Forces, s.EnergyResidual, s.ActuatorPower, s.Horizontal })),
            Hash46([Config46(lab, g, clock)]), f.Steps.Length, f.Final, f.Stop, f.Boundary, f.Ground);
    }
}
