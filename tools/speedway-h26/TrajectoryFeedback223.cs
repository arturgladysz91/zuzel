using System.Diagnostics;
using System.Runtime.InteropServices;

namespace SpeedwayHandling;

internal static partial class Study191
{
    private const double TrackerHorizonSeconds43 = .120;
    private sealed record Gains43(int Id, double Steer, double Drive, double Rider, double Timing, double Explore);
    private sealed record TrackSeed43(string Name, double SpeedKph, double LaneMeters, double AdditionalDelaySeconds);
    private sealed record ReferenceAct43(double Phase, double ReviewSeconds, Command215 Command, Phase43 State);
    private sealed record TrackDecision43(double Time, double Phase, double ReferencePhase, double Milliseconds,
        int ForecastSteps, bool ReferenceCacheHit, double Correction, double VelocityError, double RollError,
        double LaneError, double QueueDriveError, string? Refusal);
    private sealed record Tracking43(Fork40 Motion, TrackDecision43[] Decisions, double ControllerCpu,
        double IntegrationCpu, double WallSeconds);
    private sealed record TrackingInput43(Fork40 Linked, Fork40 Cycle, ReferenceAct43[] Early, ReferenceAct43[] Late);

    [DllImport("kernel32.dll")]
    private static extern IntPtr GetCurrentThread();

    [DllImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetThreadTimes(IntPtr thread, out long created, out long exited, out long kernel, out long user);

    private static long ThreadTicks43() => OperatingSystem.IsWindows() &&
        GetThreadTimes(GetCurrentThread(), out _, out _, out long kernel, out long user) ? kernel + user : 0;

    private static void QualifyTracking43(Lab38 lab, string output)
    {
        var g = lab.Geometry(true);
        var race = Path.Combine(output, "shoot-selected.json.gz");
        var periodic = Path.Combine(output, "periodic-selected.json.gz");
        var linked = Read223(lab, File.Exists(race) ? race : ArchivePath43(lab, "speedway222/qualification/nominal"),
            "selected linked guide");
        var cycle = Read223(lab, File.Exists(periodic) ? periodic : ArchivePath43(lab, "speedway222/cycle/cycle-flying"), "cyclic guide");
        double end = Map199.At(2, 1).Exit - lab.Bend.Entry + PostMarkerMeters43;
        var input = new TrackingInput43(linked, cycle, TrackingActs43(lab, g, linked), TrackingActs43(lab, g, cycle));
        var nominalSeed = new TrackSeed43("nominal", 0, 0, 0);
        Gains43[] options = [new(5, .65, .55, .30, .22, .010), new(6, .50, .48, .25, .14, .005)];
        var train = options.Select(p => (Policy: p, Trial: Track43(lab, g, input, nominalSeed, p, end))).ToArray();
        var results = train.Select(x => new { x.Policy, Outcome = QualReceipt43(lab, g, x.Trial, end) }).ToArray();
        var winner = train.OrderBy(x => Score43(lab, g, x.Trial.Motion), Comparer<Score40>.Create(CompareLink40)).First();
        var policy = winner.Policy;
        TrackSeed43[] holds = [new("fresh inward slower", -.19, -.041, .0028),
            new("fresh outward faster", .21, .044, .0046), new("fresh outward slower", -.24, .063, .0063),
            new("fresh inward faster", .28, -.069, .0074)];
        Save(Path.Combine(output, "feedback-freeze.json"), new
        {
            Policy = policy, Holdouts = holds, Training = results, TrackIndexed = true, ReactionSeconds216,
            ForecastSeconds = TrackerHorizonSeconds43, NativeWorlds = 0
        });
        double lap = Frame.LapMeters;
        var trials = new List<object>();
        var shapes = new[] { ("nominal", H36Footprint), ("compact", Compact38), ("large", Large38) };
        foreach (var (name, shape) in shapes)
        {
            var variant = lab.Geometry(true, shape);
            var geometry = Track43(lab, variant, input, nominalSeed, policy, end, shape);
            trials.Add(new { Name = name, Type = "geometry", Outcome = QualReceipt43(lab, variant, geometry, end) });
        }
        for (int i = 0; i <= holds.Length; i++)
        {
            var condition = i == 0 ? nominalSeed : holds[i - 1];
            var trial = Track43(lab, g, input, condition, policy, 2 * lap + PostMarkerMeters43);
            bool two = trial.Motion.Flight.Final.Distance >= 2 * lap + PostMarkerMeters43;
            if (two) trial = Track43(lab, g, input, condition, policy, 4 * lap + PostMarkerMeters43);
            var outcome = QualReceipt43(lab, g, trial, end);
            trials.Add(new { Name = condition.Name, Type = "frozen", Outcome = outcome });
            string label = i == 0 ? "feedback-nominal" : $"feedback-holdout-{i}";
            var m = trial.Motion;
            Save(Path.Combine(output, label + "-decisions.json.gz"), new
            {
                condition, Policy = policy, m.Seed, m.Tape, m.Flight.Final, m.Flight.Stop, trial.Decisions,
                Samples = m.Flight.Steps.Select((row, n) => (row, n)).Where(p => p.n % 20 == 0)
                    .Select(x => new
                    {
                        Station = m.Flight.Distances[x.n], State = x.row.After, Front = x.row.Forces.Front.Normal,
                        Rear = x.row.Forces.Rear.Normal, Slip = x.row.Forces.RearSlip, Beta = SlipAngle38(x.row.After),
                        Force = x.row.Forces.Rider.Norm + Math.Abs(x.row.Forces.LeftFoot?.MuscleForce ?? 0)
                    })
            }, true);
            if (i == 0) WriteEvidence39(lab, output, label, g, m.Seed, m.Tape, m.Flight);
            Console.WriteLine($"Study 223 feedback {condition.Name}: {m.Flight.Final.Distance:F2} m, {m.Flight.Stop}.");
        }
        Save(Path.Combine(output, "feedback-qualification.json"), new { Policy = policy, Trials = trials, NativeWorlds = 0 });
    }

    private static ReferenceAct43[] TrackingActs43(Lab38 lab, Geometry215 g, Fork40 motion)
    {
        var commands = motion.Tape.TakeWhile(x => x.Requested < motion.Flight.Final.State.Time - 1e-7).ToArray();
        return commands.Select((cmd, i) =>
        {
            var at = PhaseAt43(lab, g, motion, CommandState43(motion, cmd.Requested));
            double review = i + 1 < commands.Length ? commands[i + 1].Requested - cmd.Requested : Cadence38;
            double phase = cmd.Requested <= motion.Seed.State.Time + 1e-9 ? motion.Seed.Distance : at.Station;
            return new ReferenceAct43(phase, review, cmd.Command, at);
        }).ToArray();
    }

    private static (ReferenceAct43[] Table, Fork40 Source, double Phase) TrackingPhase43(TrackingInput43 guides, double station)
    {
        double lap = Frame.LapMeters;
        double phase = station < 0 ? station : station % lap;
        // Keep the linked witness until its physical tape hands off to exit recovery.
        // Reuse the tail through the first 24 m if it was independently propagated beyond the seam.
        if (station >= lap && station <= guides.Cycle.Flight.Final.Distance && station < guides.Late[^1].Phase - .15)
            return (guides.Late, guides.Cycle, station);
        if (phase <= guides.Early[^1].Phase - .08) return (guides.Early, guides.Linked, phase);
        return (guides.Late, guides.Cycle, phase);
    }

    private static int Nearest43(ReferenceAct43[] items, double phase, int? cache, out bool hit)
    {
        if (cache is { } j && (j == 0 || phase > (items[j - 1].Phase + items[j].Phase) * .5) &&
            (j == items.Length - 1 || phase <= (items[j].Phase + items[j + 1].Phase) * .5))
        {
            hit = true;
            return j;
        }
        hit = false;
        int lo = 0, hi = items.Length - 1;
        while (lo < hi)
        {
            int mid = (lo + hi) / 2;
            if (phase > (items[mid].Phase + items[mid + 1].Phase) * .5) lo = mid + 1;
            else hi = mid;
        }
        return lo;
    }

    private static Tracking43 Track43(Lab38 lab, Geometry215 g, TrackingInput43 guides, TrackSeed43 held, Gains43 p,
        double finish, SpatialBike36? shape = null)
    {
        var clock = Stopwatch.StartNew();
        var model = lab.Engine(g);
        var root = guides.Linked.Seed;
        if (held.SpeedKph != 0 || held.LaneMeters != 0)
        {
            var s = root.State;
            var road = lab.Course.Mark(s, g).Road;
            var equilibrium = SteadyCircle215(model, g, lab.Fixture.Rest(g, NominalSupport216),
                (Speed38(s, g) + held.SpeedKph) / 3.6, 0, s.Time, lab.Fixture.Ratio, balanceRider: true);
            if (equilibrium.Residual > .005)
                throw new InvalidOperationException("Fresh H42 approach does not satisfy the original equilibrium gate.");
            root = new(Place36(equilibrium.State, lab.Bend.Entry + root.Distance, road.OffsetMeters + held.LaneMeters), root.Distance);
        }
        var state = root;
        var rows = new List<Step215>();
        var distance = new List<double>();
        var decisions = new List<TrackDecision43>();
        Activation215[] tape = [];
        var envelope = shape ?? H36Footprint;
        double ground = envelope.GroundMargin(g, state.State);
        double boundary = envelope.Clearance(lab.Track, g, state.State, true);
        double timeLimit = root.State.Time + 95;
        long controller = 0, propagation = 0;
        ReferenceAct43[]? previousTable = null;
        int? previousIndex = null;
        string stop = "verified interval";
        while (state.Distance < finish && state.State.Time < timeLimit && stop == "verified interval")
        {
            long started = Stopwatch.GetTimestamp(), cpu = ThreadTicks43();
            var (table, reference, phase) = TrackingPhase43(guides, state.Distance);
            int j = Nearest43(table, phase, ReferenceEquals(previousTable, table) ? previousIndex : null, out bool hit);
            previousIndex = j;
            previousTable = table;
            var action = table[j];
            var ideal = AtPhase43(lab, g, reference, phase);
            var command = action.Command;
            var real = lab.Course.Mark(state.State, g);
            var motion = g.Motion(state.State);
            double direction = Difference38(Heading38(state.State), Math.Atan2(real.Road.Tangent.Y, real.Road.Tangent.X));
            double velocityError = ideal.RoadSpeed - real.Speed * Math.Cos(direction);
            double laneError = ideal.Offset - real.Road.OffsetMeters;
            double rollError = ideal.Roll - state.State.Pose.Roll;
            double rateError = ideal.BodyRate.X - motion.BodyRate.X;
            double headingError = Difference38(ideal.Direction, direction);
            double activeSteer = state.State.Steer;
            var pending = tape.Skip(state.State.Next).FirstOrDefault().Command;
            // Compare the reference queue immediately before its request. A phase sample
            // just after that request already contains a command we have yet to submit.
            var expectedPending = action.State.Pending.FirstOrDefault(x => x.Requested < -1e-8)?.Command
                ?? action.State.Active;
            double queueDriveError = expectedPending.Bike.Drive -
                (state.State.Next < tape.Length ? pending.Bike.Drive : state.State.Command.Bike.Drive);
            double dt = action.ReviewSeconds;
            double residual = Math.Abs(velocityError) + Math.Abs(laneError) + Math.Abs(rollError) +
                Math.Abs(rateError) * .2 + Math.Abs(headingError) + (ideal.Rider - state.State.Rider).Norm;
            if (residual > .055 && j + 1 < table.Length)
            {
                double phaseError = action.Phase - phase;
                dt += Math.Clamp(p.Timing * phaseError / Math.Max(7, real.Speed), -.009, .009);
            }
            dt = Math.Clamp(dt, .012, .120);
            bool needsRecovery = residual > .055 || (residual > .025 && Math.Abs(queueDriveError) > .12);
            int forecastSteps = 0;
            Command215? selected = command;
            if (needsRecovery)
            {
                double steer = p.Steer * (.075 * rollError + .020 * rateError + .014 * laneError - .04 * headingError
                    + .16 * (ideal.Steer - activeSteer));
                double throttle = p.Drive * Math.Clamp(.13 * velocityError + .10 * queueDriveError, -.16, .16);
                var body = (ideal.Rider - state.State.Rider) * p.Rider + (ideal.RiderRate - motion.RiderRate) * .04;
                double foot = command.LeftFoot + .20 * (ideal.Leg.Extension - state.State.Leg.Extension);
                var correction = Bounded43(command.Bike.Drive + throttle, command.Bike.SteeringRadians + steer,
                    command.Target + body, foot);
                if (residual < .065 && envelope.GroundMargin(g, state.State) > .025)
                    selected = correction;
                else
                {
                    var alternatives = new[] { correction, command,
                        correction with { Bike = correction.Bike with { SteeringRadians = Steering39(correction.Bike.SteeringRadians + p.Explore) } },
                        correction with { Bike = correction.Bike with { SteeringRadians = Steering39(correction.Bike.SteeringRadians - p.Explore) } },
                        correction with { Bike = correction.Bike with { Drive = Math.Clamp(correction.Bike.Drive - .14f, 0, 1) } },
                        correction with { Bike = correction.Bike with { Drive = Math.Clamp(correction.Bike.Drive + .10f, 0, 1) } },
                        command with { Bike = command.Bike with { SteeringRadians = Steering39(command.Bike.SteeringRadians + .45 * p.Explore) } },
                        command with { Bike = command.Bike with { SteeringRadians = Steering39(command.Bike.SteeringRadians - .45 * p.Explore) } } };
                    double best = double.PositiveInfinity;
                    selected = null;
                    foreach (var attempt in alternatives.Distinct())
                    {
                        var predicted = Forecast(attempt);
                        forecastSteps += predicted.Steps.Length;
                        if (predicted.Stop != "verified interval") continue;
                        var future = predicted.Final;
                        double refFuture = phase + future.Distance - state.Distance;
                        var target = AtPhase43(lab, g, reference, refFuture);
                        var observe = lab.Course.Mark(future.State, g);
                        double angle = Difference38(Heading38(future.State),
                            Math.Atan2(observe.Road.Tangent.Y, observe.Road.Tangent.X));
                        double rate = g.Motion(future.State).BodyRate.X;
                        double cost = 3 * Sq43(observe.Road.OffsetMeters - target.Offset) + 2 * Sq43(observe.Speed * Math.Cos(angle) - target.RoadSpeed)
                            + 60 * Sq43(future.State.Pose.Roll - target.Roll) + 2.8 * Sq43(rate - target.BodyRate.X)
                            + 23 * Sq43(Difference38(angle, target.Direction)) + 8 * Sq43((future.State.Rider - target.Rider).Norm)
                            + 350 * Sq43(Math.Max(0, .015 - predicted.Ground)) + .16 * CommandGap43(attempt, command);
                        if (cost >= best) continue;
                        best = cost;
                        selected = attempt;
                    }
                }
            }
            double delay = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
            Flight38 Forecast(Command215 candidate)
            {
                var queue = Enqueue(candidate).ToList();
                double offset = dt;
                for (int k = j + 1; k < table.Length && offset < TrackerHorizonSeconds43 - ReactionSeconds216; k++)
                {
                    double futureTime = state.State.Time + offset;
                    queue.Add(new(futureTime, futureTime + ReactionSeconds216 + held.AdditionalDelaySeconds, table[k].Command));
                    offset += table[k].ReviewSeconds;
                }
                return Window38(lab, g, model, state, queue.ToArray(), TrackerHorizonSeconds43, envelope, forecast: true);
            }

            controller += ThreadTicks43() - cpu;
            decisions.Add(new(state.State.Time, state.Distance, action.Phase, delay, forecastSteps, hit,
                selected is { } applied ? CommandGap43(applied, command) : 0, velocityError, rollError, laneError,
                queueDriveError, selected is null ? "state-dependent short-horizon candidates exhausted" : null));
            if (selected is null)
            {
                stop = decisions[^1].Refusal!;
                break;
            }
            cpu = ThreadTicks43();
            tape = Enqueue(selected.Value);
            var window = Window38(lab, g, model, state, tape, dt, envelope, exact: true, finishAt: finish);
            propagation += ThreadTicks43() - cpu;
            boundary = Math.Min(boundary, window.Boundary);
            ground = Math.Min(ground, window.Ground);
            rows.AddRange(window.Steps);
            distance.AddRange(window.Distances);
            state = window.Final;
            stop = window.Stop;
        }
        if (stop == "verified interval")
            stop = state.Distance >= finish ? "completed linked horizon" : "tracker time budget";
        var combined = new Flight38(state, rows.ToArray(), distance.ToArray(), stop, boundary, ground);
        return new(new(held.Name, root, combined, tape), decisions.ToArray(), controller / 1e7, propagation / 1e7,
            clock.Elapsed.TotalSeconds);

        Activation215[] Enqueue(Command215 command)
        {
            double t = state.State.Time;
            return [.. tape, new(t, t + ReactionSeconds216 + held.AdditionalDelaySeconds, command)];
        }
    }

    private static double Sq43(double x) => x * x;

    private static object QualReceipt43(Lab38 lab, Geometry215 g, Tracking43 trial, double linkedEnd)
    {
        var full = trial.Motion;
        int idx = full.Flight.Distances.Length == 0 ? -1 : Index43(full.Flight.Distances, linkedEnd);
        var section = idx < 0 || idx == full.Flight.Steps.Length - 1 ? full : full with
        {
            Flight = full.Flight with
            {
                Steps = full.Flight.Steps[..(idx + 1)], Distances = full.Flight.Distances[..(idx + 1)],
                Final = new(full.Flight.Steps[idx].After, full.Flight.Distances[idx]), Stop = "completed linked horizon"
            }
        };
        var d = trial.Decisions;
        var t = d.Select(x => x.Milliseconds).Order().ToArray();
        double Percentile(double p) => t.Length == 0 ? 0 : t[(int)Math.Ceiling(p * t.Length) - 1];
        double lap = Frame.LapMeters;
        double last = full.Flight.Final.Distance;
        return new
        {
            Score = Score43(lab, g, section), Last = last, full.Flight.Stop, full.Flight.Boundary, full.Flight.Ground,
            LapOne = last >= lap + PostMarkerMeters43, LapTwo = last >= 2 * lap + PostMarkerMeters43,
            LapFour = last >= 4 * lap + PostMarkerMeters43,
            Gates = last >= 0 ? LapGates43(lab, g, full) : [],
            Decisions = t.Length, P50 = Percentile(.50), P95 = Percentile(.95), P99 = Percentile(.99), PMax = Percentile(1),
            Missed50 = t.Count(x => x > 50), ForecastSteps = d.Sum(x => x.ForecastSteps), ActualSteps = full.Flight.Steps.Length,
            SpatialReferenceCacheHits = d.Count(x => x.ReferenceCacheHit), SpatialReferenceCacheMisses = d.Count(x => !x.ReferenceCacheHit),
            CachedCommands = 0, Adapted = d.Count(x => x.Correction > 1e-6), PathMeters = PathLength43(full),
            trial.ControllerCpu, trial.IntegrationCpu, trial.WallSeconds
        };
    }
}
