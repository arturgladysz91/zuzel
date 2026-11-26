using System.Diagnostics;
using SharedWorldSpeedway;
using WholeCorner171;

namespace SpeedwayHandling;

internal static partial class Study191
{
    private static Run40 RunLink40(Lab38 lab, Geometry215 g, Checkpoint38 seed, Checkpoint38 start,
        Activation215[] earlier, Step215[] prefix, double[] stations, Setup40 setup, WarmLink40? cache = null)
    {
        var model = lab.Engine(g);
        var next = Map199.At(2, 1);
        var steps = new List<Step215>(prefix);
        var distances = new List<double>(stations);
        var decisions = new List<Decision40>();
        var state = start;
        var tape = earlier;
        var last = prefix.LastOrDefault()?.Forces;
        double end = next.Exit - lab.Bend.Entry + 22;
        double minimumSwept = H36Footprint.Clearance(lab.Track, g, state.State, false);
        double minimumHard = H36Footprint.GroundMargin(g, state.State);
        string stop = "verified interval";
        double limit = state.State.Time + 14;
        while (state.Distance < end && state.State.Time < limit && stop == "verified interval")
        {
            long time = Stopwatch.GetTimestamp();
            var (action, data) = CachedDecision40(g, state, last, cache) ??
                DecideLink40(lab, g, model, setup, state, tape, next, last);
            decisions.Add(data with { Milliseconds = Stopwatch.GetElapsedTime(time).TotalMilliseconds });
            if (action is null) { stop = data.Refusal ?? "linked controller candidate exhaustion"; break; }
            tape = Submit38(state, tape, action.Value);
            var w = Window38(lab, g, model, state, tape, Cadence38, finishAt: end);
            steps.AddRange(w.Steps);
            distances.AddRange(w.Distances);
            minimumSwept = Math.Min(minimumSwept, w.Boundary);
            minimumHard = Math.Min(minimumHard, w.Ground);
            last = w.Steps.LastOrDefault()?.Forces ?? last;
            state = w.Final;
            stop = w.Stop;
        }
        if (stop == "verified interval") stop = state.Distance >= end ? "completed linked horizon" : "linked time horizon";
        return new(setup, seed, start, earlier, tape, new(state, steps.ToArray(), distances.ToArray(), stop,
            minimumSwept, minimumHard), decisions.ToArray());
    }

    private static (Command215? Action, Decision40 Receipt) DecideLink40(Lab38 lab, Geometry215 g, UnifiedModel215 model,
        Setup40 setup, Checkpoint38 at, Activation215[] tape, BendVisit bend, Forces215? last,
        Limit42 ablation = Limit42.None, Func<RollReference36, RiderView, double, RollReference36>? refine = null,
        double extraDamping = 0)
    {
        var state = at.State;
        var road = lab.Course.Mark(state, g);
        var k = g.Motion(state);
        double time = state.Time, reference = lab.Bend.Entry + at.Distance, distance = reference - bend.Entry;
        double rs = RollRate36(state, k.BodyRate);
        double rearSlip = ContactSlip39(g, state);
        double fz = last?.Front.Normal ?? 600;
        double frontUse = last?.Front.Utilization ?? 0;
        double rearUse = last?.Rear.Utilization ?? 0;
        double speed = Speed38(state, g);
        double reserve = ((1 - frontUse) * fz + (1 - rearUse) * (last?.Rear.Normal ?? 800)) * 1.605625 / g.Base.Mass;
        double preparation = Smooth37((distance + setup.PreparationMeters) / setup.PreparationMeters);
        double turn = Smooth37((distance + 6) / 18);
        double exit = Smooth37((reference - bend.Exit + 18) / 18);
        double apex = setup.Lane > 7.5 ? .8 : .4;
        double preview = distance < 0 ? 22 + 5 * preparation : 23 - 5 * turn + 3 * exit;
        if (setup.Method == Prep40.Coast) preview += 4;
        double gain = setup.Method == Prep40.Force ? 25 : 20;
        double damping = (setup.Method == Prep40.Force ? 13 : 9) + extraDamping;
        var tune = new Tune36(setup.Lane, preview, gain, damping, 0, 0, 0, apex, 0, true);
        var target = Plan(road, reference);
        int forecasts = 0;
        Command215? command;
        string? failure;
        if (setup.Cheap && Math.Abs(state.Pose.Roll - target.Roll) < .18 && Math.Abs(rs) < .55 &&
            frontUse < .84 && fz > 180)
        {
            // Local inversion from the validated front-force slope. Retain the same actuator and reaction.
            var axle = g.Contact(state, k, true, 0);
            double localGain = Math.Abs(axle.Arm.Z) * fz * 1.605625 * k.BodyRate.Norm * .02 + 26;
            double correction = (gain * (target.Roll - state.Pose.Roll) - damping * rs) / localGain;
            command = new Command215(new(Steering39(state.Steer + .31 * correction), target.Drive), target.Rider);
            failure = null;
        }
        else
        {
            var exact = H36Control(state, g, model, road, reference, bend, tune,
                setup.CornerRequestKph, .16f, tape, lab.Course, referenceBasis: Plan);
            command = exact.Command;
            forecasts = exact.Steps;
            failure = exact.Stop;
        }
        if (command is null)
        {
            double correction = .07 * (4 * (target.Roll - state.Pose.Roll) - rs);
            foreach (double angle in new[] { state.Steer + correction, state.Steer, state.Steer - correction })
            {
                var trial = new Command215(new(Steering39(angle), 0), target.Rider);
                var forecast = Window38(lab, g, model, at, Submit38(at, tape, trial), .125, forecast: true);
                forecasts += forecast.Steps.Length;
                if (forecast.Stop != "verified interval") continue;
                command = trial;
                break;
            }
        }
        return (command, new(time, at.Distance, command?.Bike.Drive ?? target.Drive, target.Roll, fz,
            frontUse, rearUse, rs, rearSlip, target.Curvature, reserve, forecasts, 0,
            command is null ? "candidate exhaustion: " + failure : null));

        RollReference36 Plan(RiderView body, double s)
        {
            double local = s - bend.Entry, toEntry = -local;
            double progress = Math.Clamp((s + preview - bend.Entry) / bend.Length, 0, 1);
            double currentOffset = Math.Clamp(body.Road.OffsetMeters, 2, 10);
            double line = setup.Lane - apex * Math.Pow(Math.Sin(Math.PI * progress), 2);
            if (local < 0) line = currentOffset + (line - currentOffset) * (.45 + .55 * preparation);
            double curve = PreviewCurvature36(body, s + preview, line);
            double normalDemand = body.Speed * body.Speed * curve;
            double roll = -Math.Atan(normalDemand / Gravity216);
            roll = ClearanceRoll37(g, state, roll, ablation == Limit42.GroundReserve ? .001 : .009);
            double corner = setup.CornerRequestKph;
            double predictedCurve = Math.Max(ablation == Limit42.CurvatureFloor ? .001 : .010,
                Math.Abs(PreviewCurvature36(body, s + 29, line)));
            double forceLimit = 7.8 + .45 * Math.Max(0, reserve);
            if (ablation == Limit42.ScalarReserve && last is not null)
                forceLimit = ProjectForce42(g, state, last).MaximumInward;
            if (ablation != Limit42.AccelerationCap) forceLimit = Math.Min(11.4, forceLimit);
            forceLimit = Math.Max(.001, forceLimit);
            double compatible = 3.6 * Math.Sqrt(forceLimit / predictedCurve);
            if (ablation != Limit42.CornerCeiling) compatible = Math.Min(corner + 10, compatible);
            double availableDecel = ablation == Limit42.Deceleration && last is not null
                ? Math.Max(.05, -(last.Drag + last.Front.Force).Plane.Dot(state.Momentum.Plane / state.Momentum.Plane.Norm) / g.Base.Mass)
                : 1.45 + .00009 * speed * speed;
            double approachSpeed = Math.Sqrt(Math.Pow(compatible / 3.6, 2) +
                2 * availableDecel * Math.Max(0, toEntry - 2)) * 3.6;
            double request = local < 0 ? Math.Min(setup.PeakRequestKph, approachSpeed) :
                corner - (ablation == Limit42.BendDrop ? 0 : 5) * turn + exit * 11;
            if (setup.Method == Prep40.Pace && local < 0)
                request = setup.PeakRequestKph + (corner - setup.PeakRequestKph) * preparation;
            if (reference > bend.Exit) request = ablation == Limit42.ExitReference ? body.Speed * 3.6 + 18 : 94;
            double drive = Math.Clamp(.17 + .16 * (request / 3.6 - body.Speed), 0, 1);
            if (local < 0)
            {
                if (setup.Method == Prep40.Legacy)
                {
                    drive = Math.Clamp(.16 + .20 * ((setup.PeakRequestKph - speed) / 3.6), 0, 1);
                    if (toEntry <= 17)
                        drive = Math.Min(drive, Math.Clamp(.17 + .013 * toEntry - .014 * Math.Max(0, speed - 84), .10, .42));
                }
                if (ablation != Limit42.Coast && setup.Method == Prep40.Coast && preparation > .12 && body.Speed * 3.6 > compatible - 2)
                    drive = 0;
                if (setup.Method == Prep40.Force && (fz < 275 || rearSlip > .25 || Math.Abs(rs) > .65))
                    drive *= Math.Clamp(fz / 425, .18, 1) * Math.Clamp(1.15 - rearSlip, .25, 1);
            }
            else
            {
                if (ablation != Limit42.RearSlip) drive *= Math.Clamp(1.25 - .65 * Math.Max(0, rearSlip), .16, 1);
                if (exit > .35 && fz > 280 && Math.Abs(rs) < .8)
                    drive = Math.Max(drive, setup.Boost * exit);
            }
            if (reference > bend.Exit + 6 && (fz < 190 || Math.Abs(rs) > .65)) drive = Math.Min(drive, .28);
            double bodyShape = Math.Max(.70 * preparation * (1 - turn), Math.Sin(Math.PI * progress)) * (1 - exit);
            var guide = new RollReference36(curve, roll, preview, request, (float)drive,
                new Vec215(.055 * bodyShape, .025 * bodyShape, -.030 * bodyShape), .45 * bodyShape * bodyShape);
            return refine?.Invoke(guide, body, s) ?? guide;
        }
    }

    private sealed record WarmLink40(Activation215[] Commands, State215[] States, double[] Times);

    private static WarmLink40 WarmStart40(Run40 reference)
    {
        var commands = reference.Commands.Where(x => x.Requested >= reference.Fork.State.Time - 1e-8).ToArray();
        var times = commands.Select(x => x.Requested).ToArray();
        var states = new State215[times.Length];
        int j = 0;
        for (int i = 0; i < times.Length; i++)
        {
            while (j + 1 < reference.Flight.Steps.Length &&
                   reference.Flight.Steps[j].After.Time < times[i] - 1e-8) j++;
            states[i] = reference.Flight.Steps[j].After;
        }
        return new(commands, states, times);
    }

    private static (Command215?, Decision40)? CachedDecision40(Geometry215 g, Checkpoint38 at, Forces215? forces,
        WarmLink40? warm)
    {
        if (warm is null) return null;
        int i = Array.BinarySearch(warm.Times, at.State.Time - 1e-8);
        if (i < 0) i = ~i;
        if (i >= warm.Commands.Length || Math.Abs(warm.Times[i] - at.State.Time) > 1e-8) return null;
        var r = warm.States[i];
        var s = at.State;
        if (s.Next != r.Next || Math.Abs(s.Steer - r.Steer) > .018 || Math.Abs(s.Pose.Roll - r.Pose.Roll) > .045 ||
            (s.Center - r.Center).Norm > .20 || (s.Momentum - r.Momentum).Norm > .30 * g.Base.Mass ||
            (s.Angular - r.Angular).Norm > 4 || (s.Rider - r.Rider).Norm > .013 ||
            Math.Abs(s.Wheel - r.Wheel) > 1.0 || Math.Abs(s.Leg.Extension - r.Leg.Extension) > .03)
            return null;
        double leanError = s.Pose.Roll - r.Pose.Roll;
        double requestDelta = .1 * leanError;
        var nominal = warm.Commands[i].Command;
        var nudged = Math.Abs(requestDelta) < 1e-7 ? nominal : nominal with
        {
            Bike = nominal.Bike with { SteeringRadians = Steering39(nominal.Bike.SteeringRadians + requestDelta) }
        };
        var front = forces?.Front;
        var rear = forces?.Rear;
        return (nudged, new(s.Time, at.Distance, nudged.Bike.Drive, r.Pose.Roll, front?.Normal ?? 0,
            front?.Utilization ?? 0, rear?.Utilization ?? 0, RollRate36(s, g.Motion(s).BodyRate), ContactSlip39(g, s),
            0, 0, 0, 0, null));
    }

    private static Run40 ExactLink40(Lab38 lab, Geometry215 geometry, Run40 trace, SpatialBike36? shape = null)
    {
        var replay = ReplayTape38(lab, geometry, trace.Root, trace.Commands, trace.Flight.Final.State.Time,
            shape, exact: true);
        if (replay.Stop == "verified interval") replay = replay with { Stop = trace.Flight.Stop };
        return trace with { Flight = replay };
    }
}
