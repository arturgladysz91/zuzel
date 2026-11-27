using System.Diagnostics;
using SharedWorldSpeedway;

namespace SpeedwayHandling;

internal static partial class Study191
{
    private const string Base45 = "fd085297ef590729e72879c1de936240121781ac";
    private enum Line45 { A, B, C, D }
    private enum Limit45 { None, Straight, Turn, Deceleration, Lookahead, Slip, Lean, ExitFloor }
    private sealed record Plan45(int Id, Line45 Line, Route44 Route, double ApexMeters, double ReleaseMeters,
        double TurnInMeters, double DriveReserve, double RiderReserve, double BankingReserve,
        double FeedbackSeconds = .065, Limit45 Ablation = Limit45.None, bool DirectionalPace = false,
        double? SpeedBand = null, double TransitionMeters = 0, double Blend = 1,
        double EntryCorrection = 0, double TurnPhaseMeters = 0, Route44? InheritedLine = null);
    private sealed record PaceAudit45(double Station, double InwardBudget, double Deceleration,
        double EnvelopeKph, double TargetKph, double PaceDrive, double WithFloors, double WithSlip,
        double WithInterventions, double RawRoll, double FinalRoll, double RearSlip, int Rescue);
    private sealed record RejectedStep45(string Reason, double Station, double RequestedStep,
        State215 Before, State215? Candidate, Step215? Step, double? SweptBoundary, double? SweptGround);
    private sealed record Running45(Plan45 Plan, Fork40 Motion, StateDecision44[] Decisions,
        double ControllerCpu, double IntegrationCpu, double WallSeconds, RejectedStep45? Rejected);
    private sealed record Metrics45(double Begin, double End, double? Seconds, double PathMeters, double PeakKph,
        double MinKph, double MeanAbsCurvature, double MaxAbsCurvature, double TyreLossJ,
        double EngineWorkJ, double WotSeconds, double MeanDrive, double MinGround, double MeanSteer,
        double HeadingError, double Sideslip, double SideSpeed, double Roll, double YawRate,
        double RearSlip, double FrontLoad, double RearLoad);
    private sealed record Summary45(Plan45 Plan, double Station, string Stop, int Steps, int Decisions,
        double Clearance, double Hard, double ControllerCpu, double IntegrationCpu, double WallSeconds,
        double StraightPeakKph, double NextExit, double Finish, bool Finished, Metrics45[] Laps,
        Metrics45[] Windows, Phase43[] Seams, double? ExitToFinishSeconds = null);
    private sealed record Stage45(Summary45[] Screen, Summary45[] Exact, Plan45? Pick, double Fork, double Finish);

    private static string Input45(Lab38 lab, string file) =>
        Path.Combine(lab.Root, "data", "experiments", "speedway224", file);

    private static void RacingStudy225(string checkout, string output, string mode)
    {
        var lab = MakeLinkLab40(checkout);
        var g = lab.Geometry(true);
        switch (mode)
        {
            case "references": References45(lab, g, output); break;
            case "discrepancies": Discrepancies45(lab, g, output); break;
            case "limits": Limits45(lab, g, output); break;
            case "lines": Search45(lab, g, output, mode); break;
            case "transition": Search45(lab, g, output, mode); break;
            case "support": Search45(lab, g, output, mode); break;
            case "support-exact": CertifySupport45(lab, g, output); break;
            case "sustain": Sustain45(lab, g, output); break;
            case "speed": Search45(lab, g, output, mode); break;
            case "speed-recovery": Downstream45(lab, g, output); break;
            case "runtime": Runtime45(lab, g, output); break;
            case "holdouts": Holdouts45(lab, g, output); break;
            case "standing": Standing45(lab, g, output); break;
            case "verify": Verify45(lab, g, output); break;
            case "gates": SportingGates45(lab, g, output); break;
            case "best-lap": BestLaps45(lab, g, output); break;
            default: throw new ArgumentException("Use references, discrepancies, limits, lines, transition, support, support-exact, sustain, speed, speed-recovery, runtime, holdouts, standing, verify, gates or best-lap.");
        }
    }

    private static Plan45 Baseline45(Lab38 lab) => new(0, Line45.A,
        ReadPlain214<Piece44[]>(Input45(lab, "line-selected-policy.json"))[0].Route,
        .53 * lab.Bend.Length, lab.Bend.Length + 6, 23, 0, 0, 0);

    private static double Lane45(Lab38 lab, Plan45 p, double station)
    {
        if (p.Line == Line45.A) return Lane44(lab, p.Route, station);
        double phase = station - lab.Bend.Entry;
        phase -= Half44 * Math.Floor(phase / Half44);
        double approach = Half44 - p.TurnInMeters;
        double entry = p.Route.EntryOutside - .30 * p.Route.EarlyHeading;
        if (phase < p.ApexMeters)
            return Lerp44(entry, p.Route.Inside, phase / p.ApexMeters);
        if (phase < p.ReleaseMeters)
            return Lerp44(p.Route.Inside, p.Route.ExitOutside, (phase - p.ApexMeters) / (p.ReleaseMeters - p.ApexMeters));
        if (phase < approach)
            return Lerp44(p.Route.ExitOutside, p.Route.EntryOutside, (phase - p.ReleaseMeters) / (approach - p.ReleaseMeters));
        return Lerp44(p.Route.EntryOutside, entry, (phase - approach) / p.TurnInMeters);
    }

    private static void Adjust45(Lab38 lab, Geometry215 g, Plan45 plan, State215 state, Kinematics215 kin,
        Forces215? last, RiderView body, double station, double frontLoad, double slip, double aperture,
        double rollRate, double curve, ref double drive, ref double lean, ref Vec215 target)
    {
        double phase = station - lab.Bend.Entry;
        phase -= Half44 * Math.Floor(phase / Half44);
        double fromExit = phase - lab.Bend.Length;
        double untilEntry = Half44 - phase;
        double exit = Smooth37((fromExit + 29) / 26) * (1 - Smooth37((fromExit - 15) / 39));
        double heading = Difference38(Math.Atan2(body.Velocity.Y, body.Velocity.X),
            Math.Atan2(body.Road.Tangent.Y, body.Road.Tangent.X));
        double desiredOutward = Math.Atan2(Lane45(lab, plan, station - 3) - Lane45(lab, plan, station + 3), 6);
        double direction = Smooth37((.24 + desiredOutward - heading) / .29);
        double wheel = Smooth37((frontLoad - 105) / 265);
        double reserve = Smooth37((aperture + .015) / .13);
        double rearward = g.Neutral.X - state.Rider.X - .18 * kin.RiderRate.X;
        double need = Smooth37((rearward - .027) / .110);
        double slideAvailable = Math.Clamp(1 - .7 * Math.Max(0, slip - .30), .25, 1);
        double lateDrive = plan.DriveReserve * exit * direction * wheel * (.24 + .76 * reserve) * slideAvailable;
        drive += (1 - drive) * lateDrive;
        if (plan.DirectionalPace && last is not null && fromExit > -17)
        {
            // Project the reached force and velocity into the coming curve. Keep drive
            // available while support and travel direction allow recovery, and trade it
            // for steering room before the next entry if the state still points inward.
            double speed = body.Speed;
            double desiredSpeed = plan.SpeedBand.GetValueOrDefault(plan.Route.StraightKph) / 3.6;
            double power = Math.Clamp((desiredSpeed - speed + 1.5) * .40, 0, 1);
            double steerReserve = Smooth37((12 - Math.Max(0, curve * speed * speed)) / 9);
            double entryReserve = Smooth37((untilEntry - 4) / 30);
            double launch = power * wheel * (.10 + .90 * reserve) * direction * slideAvailable *
                Math.Max(exit * .82, steerReserve * entryReserve) * plan.DriveReserve;
            drive = Math.Max(drive, launch);
        }
        if (plan.BankingReserve > 0)
        {
            double guard = Smooth37((.095 - aperture) / .16) * Smooth37((-rollRate + .19) / .60);
            lean += plan.BankingReserve * guard;
        }
        if (plan.EntryCorrection != 0)
        {
            double build = Smooth37((36 - untilEntry - plan.TurnPhaseMeters) / 23);
            double arrive = 1 - Smooth37((phase - 2 - plan.TurnPhaseMeters) / 24);
            double beforeEntry = phase > lab.Bend.Length + 6 ? build : arrive;
            // Positive heading error already points inwards and calls for less bank.
            double adjust = Math.Clamp(.45 * (heading - desiredOutward) - .035, -.17, .19);
            lean += plan.EntryCorrection * beforeEntry * adjust;
        }
        if (plan.RiderReserve > 0)
        {
            double aftLoading = Math.Max(0, last?.Rear.Longitudinal ?? 0) / (g.Base.Mass * Gravity216);
            double forecast = Math.Clamp(.32 * drive + .48 * aftLoading, 0, .86);
            double earlySupport = Math.Max(need, Math.Max(exit * .46, forecast)) * plan.RiderReserve;
            target += new Vec215(.112 * earlySupport, 0, -.035 * earlySupport);
            drive = Math.Min(drive, 1 - .86 * need * plan.RiderReserve);
        }
    }

    private static Running45 Ride45(Lab38 lab, Geometry215 g, Fork40 source, double fork, Plan45 plan,
        double finish, bool exact)
    {
        var (at, prefix, distances, tape) = fork == source.Seed.Distance
            ? (source.Seed, Array.Empty<Step215>(), Array.Empty<double>(), source.Tape)
            : Slice42(source, fork);
        var rows = new List<Step215>(prefix);
        var stations = new List<double>(distances);
        var decisions = new List<StateDecision44>();
        var plant = lab.Engine(g);
        var last = rows.LastOrDefault()?.Forces;
        var clock = Stopwatch.StartNew();
        long controllerCpu = 0, integrationCpu = 0;
        double boundary = H36Footprint.Clearance(lab.Track, g, at.State, exact);
        double ground = H36Footprint.GroundMargin(g, at.State);
        string stop = "verified interval";
        RejectedStep45? reject = null;
        double limit = at.State.Time + 100;
        while (at.Distance < finish && at.State.Time < limit && stop == "verified interval")
        {
            long ticks = ThreadTicks43(), time = Stopwatch.GetTimestamp();
            var active = plan.TransitionMeters > 0
                ? plan with { Blend = plan.Blend * Smooth37((at.Distance - fork) / plan.TransitionMeters) } : plan;
            var action = Choose44(lab, g, plant, H36Footprint, at, last, tape, plan.Route, active);
            controllerCpu += ThreadTicks43() - ticks;
            decisions.Add(action.Note with { Milliseconds = Stopwatch.GetElapsedTime(time).TotalMilliseconds });
            if (action.Command is not { } command)
            {
                stop = action.Note.Refusal ?? "adaptive candidate exhaustion";
                break;
            }
            ticks = ThreadTicks43();
            tape = Submit38(at, tape, command);
            var window = Window38(lab, g, plant, at, tape, Cadence38, exact: exact,
                finishAt: finish, rejected: r => reject = r);
            integrationCpu += ThreadTicks43() - ticks;
            rows.AddRange(window.Steps);
            stations.AddRange(window.Distances);
            at = window.Final;
            last = window.Steps.LastOrDefault()?.Forces ?? last;
            stop = window.Stop;
            boundary = Math.Min(boundary, window.Boundary);
            ground = Math.Min(ground, window.Ground);
        }
        if (stop == "verified interval") stop = at.Distance >= finish ? "completed requested horizon" : "time horizon";
        var motion = new Fork40(plan.Id.ToString(), source.Seed,
            new(at, rows.ToArray(), stations.ToArray(), stop, boundary, ground), tape);
        return new(plan, motion, decisions.ToArray(), controllerCpu / 1e7, integrationCpu / 1e7,
            clock.Elapsed.TotalSeconds, reject);
    }

    private static Running45 Certify45(Lab38 lab, Geometry215 g, Running45 run)
    {
        var m = run.Motion;
        RejectedStep45? reject = null;
        double until = run.Rejected?.Candidate?.Time
            ?? (run.Rejected is { } r ? r.Before.Time + r.RequestedStep : m.Flight.Final.State.Time);
        var flight = ReplayTape38(lab, g, m.Seed, m.Tape, until, exact: true, rejected: r => reject = r);
        if (flight.Stop == "verified interval") flight = flight with { Stop = m.Flight.Stop };
        return run with { Motion = m with { Flight = flight }, Rejected = reject ?? run.Rejected };
    }

    private static Metrics45 Section45(Lab38 lab, Geometry215 g, Fork40 m, double begin, double end)
    {
        var f = m.Flight;
        int lo = Index43(f.Distances, begin);
        int hi = Index43(f.Distances, end);
        double seconds = 0, path = 0, peak = 0, min = double.PositiveInfinity, bend = 0, maxBend = 0,
            tyre = 0, energy = 0, wot = 0, drive = 0, floor = double.PositiveInfinity, steer = 0;
        for (int i = lo; i <= hi; i++)
        {
            var r = f.Steps[i];
            double dt = Duration215(r);
            double speed = Speed38(r.After, g);
            var velocity = r.Midpoint.Momentum.Plane / g.Base.Mass;
            var dv = (r.After.Momentum - r.Before.Momentum).Plane / (dt * g.Base.Mass);
            double curvature = velocity.Cross(dv) / Math.Max(.001, Math.Pow(velocity.Norm, 3));
            seconds += dt;
            path += (r.After.Center - r.Before.Center).Plane.Norm;
            peak = Math.Max(peak, speed);
            min = Math.Min(min, speed);
            bend += Math.Abs(curvature) * dt;
            maxBend = Math.Max(maxBend, Math.Abs(curvature));
            tyre += r.Forces.SlideLoss * dt;
            energy += r.Forces.EnginePower * dt;
            if (r.Before.Command.Bike.Drive >= .999) wot += dt;
            drive += r.Before.Command.Bike.Drive * dt;
            steer += Math.Abs(r.After.Steer) * dt;
            floor = Math.Min(floor, H36Footprint.GroundMargin(g, r.After));
        }
        var toe = PhaseAt43(lab, g, m, hi);
        var last = f.Steps[hi];
        return new(begin, end, f.Final.Distance >= end - .001 ? f.Steps[hi].After.Time - f.Steps[lo].After.Time : null,
            path, peak, min, bend / seconds, maxBend, tyre, energy, wot, drive / seconds, floor, steer / seconds,
            toe.Direction, SlipAngle38(last.After), toe.SideSpeed, last.After.Pose.Roll,
            lab.Course.Mark(last.After, g).AngularVelocity, last.Forces.RearSlip,
            last.Forces.Front.Normal, last.Forces.Rear.Normal);
    }

    private static Summary45 Summarize45(Lab38 lab, Geometry215 g, Running45 run, double finish, double exit)
    {
        var m = run.Motion;
        var f = m.Flight;
        double lap = Frame.LapMeters;
        double straightEnd = exit + Half44 - lab.Bend.Length;
        double peak = f.Final.Distance < exit ? 0 : Section45(lab, g, m, exit, straightEnd).PeakKph;
        var arcs = new List<Metrics45>();
        for (double b = exit; b <= f.Final.Distance && b <= finish; b += Half44)
        {
            foreach (var (start, end) in new[] { (b - 24, b), (b, b + 22), (b + 22, b + Half44 - lab.Bend.Length),
                (b + Half44 - lab.Bend.Length, b + Half44) })
                if (f.Final.Distance >= start) arcs.Add(Section45(lab, g, m, start, end));
        }
        var seams = Enumerable.Range(0, 5).Select(n => n * lap).Where(meter => meter <= f.Final.Distance)
            .Select(x => AtPhase43(lab, g, m, x)).ToArray();
        return new(run.Plan, f.Final.Distance, f.Stop, f.Steps.Length, run.Decisions.Length, f.Boundary, f.Ground,
            run.ControllerCpu, run.IntegrationCpu, run.WallSeconds, peak, exit + Half44, finish,
            f.Final.Distance >= finish - .001 && f.Boundary >= .50 && f.Ground > 0,
            Enumerable.Range(0, seams.Length).Select(n => Section45(lab, g, m, n * lap, (n + 1) * lap)).ToArray(),
            arcs.ToArray(), seams, f.Final.Distance >= finish - .001
                ? f.Final.State.Time - f.Steps[Index43(f.Distances, exit)].After.Time : null);
    }
}
