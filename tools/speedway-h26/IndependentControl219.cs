
using System.Diagnostics;
using SharedWorldSpeedway;

namespace SpeedwayHandling;

internal static partial class Study191
{
    private enum DriveIntent38 { EntryPulse, Distributed, SlipRegulated, AfterFormation, ExitPower, Archive }
    private sealed record Plan38(int Id, string Archive, DriveIntent38 Intent, int Posture, double ForkMeters = -29);
    private sealed record Attempt38(Plan38 Plan, Checkpoint38 Seed, Checkpoint38 Initial, Activation215[] InitialCommands,
        Activation215[] Commands, Flight38 Trail, int ForecastSteps, bool PrefixIncluded);
    private sealed record Outcome38(Plan38 Plan, string Stop, int ActualSteps, int ForecastSteps, double Distance,
        double Boundary, double Ground, bool BalancedBend, bool StableTwenty, double? EntryKph, double? Time,
        double? MinimumKph, double? ExitKph, double? TwentyKph, double? TwentyRoll, double? TwentyYawRate,
        Receipt38 InBend, int Requests);
    private sealed record Log38(double Distance, State215 State, float ActiveDrive, double Rpm,
        double Torque, double Wheelspin, double VehicleSlip, double TyreSlip, double InwardAcceleration,
        double VelocityCurvature, double FrontNormal, double RearNormal, double FrontFx, double FrontFy,
        double RearFx, double RearFy, double YawAuthority, double RollAuthority, Vec215 Moment,
        double FrontUse, double RearUse, double FootNormal, Vec215 RiderForce, double ActivePower, double LedgerResidual,
        double InstantEdge, double InstantGround, double SweptEdge, double SweptGround);
    private sealed record FullLog38(Outcome38 Result, State215 OriginalSeed, State215 Fork,
        Activation215[] EarlierCommands, Activation215[] Commands, Log38[] States);
    private sealed record CommandHistory38(Outcome38 Result, Checkpoint38 Seed, Checkpoint38 Fork,
        Activation215[] EarlierCommands, Activation215[] Commands);

    private static Attempt38 Continue38(Lab38 lab, Archive38 archive, Geometry215 g, Plan38 plan, Checkpoint38 start,
        Activation215[] pending, bool includePrefix, SpatialBike36? envelope = null, float? firstDrive = null,
        Command215? first = null, double firstDuration = .24)
    {
        var model = lab.Engine(g);
        var shape = envelope ?? H36Footprint;
        var state = start;
        var queue = pending;
        var rows = new List<Step215>();
        var distances = new List<double>();
        double boundary = shape.Clearance(lab.Track, g, start.State, false);
        double ground = shape.GroundMargin(g, start.State);
        int forecastSteps = 0;
        string stop = "verified interval";
        if (includePrefix)
        {
            for (int i = 0; i < archive.Run.Trace.Length && archive.Run.Trace[i].After.Time <= state.State.Time + 1e-10; i++)
            {
                rows.Add(archive.Run.Trace[i]);
                distances.Add(archive.Distances[i]);
            }
        }
        if (first is { } initial)
        {
            var input = firstDrive is { } drive ? initial with { Bike = initial.Bike with { Drive = drive } } : initial;
            queue = Submit38(state, queue, input);
            var launch = Window38(lab, g, model, state, queue, firstDuration, envelope, finishAt: lab.Bend.Length + 20);
            Append(launch);
        }

        double end = start.State.Time + 13;
        while (stop == "verified interval" && state.Distance < lab.Bend.Length + 20 && state.State.Time < end - 1e-9)
        {
            bool straight = state.Distance >= lab.Bend.Length + 7.5;
            Command215? input;
            if (straight)
            {
                var mark = lab.Course.Mark(state.State, g);
                var tune = archive.Run.Summary.Tune with
                {
                    Lane = Math.Clamp(archive.Run.Summary.Finish.Offset - .5, 3, 8.6),
                    Lookahead = 23, RollGain = 17, Damping = 8, Side = 0, Fore = 0, Vertical = 0,
                    ApexPaceDropKph = 0, ApproachSpeedCorrectionKph = 0, Boot = 0, FullDriveExitMeters = 0
                };
                var guide = H36Control(state.State, g, model, mark, lab.Bend.Entry + state.Distance, lab.Bend, tune,
                    Math.Max(80, (archive.Run.Summary.Exit?.Kph ?? 65) + 12), .14f, queue, lab.Course, rollReferenceScale: .60);
                forecastSteps += guide.Steps;
                input = guide.Command;
                if (input is null) stop = guide.Stop ?? "no straight control";
            }
            else
            {
                var drive = IndependentDrive38(archive, plan, state, lab.Bend.Length);
                var result = Choose38(lab, archive, g, model, state, queue, drive,
                    BodyChoice38(plan.Posture), searchPosture: false, shape);
                input = result.Command;
                forecastSteps += result.Steps;
                if (input is null) stop = "no certified coupled continuation";
            }
            if (input is not { } command) break;
            queue = Submit38(state, queue, command);
            var window = Window38(lab, g, model, state, queue, Cadence38, envelope, finishAt: lab.Bend.Length + 20);
            Append(window);
        }
        if (stop == "verified interval")
            stop = state.Distance >= lab.Bend.Length + 20 ? "twenty metres past exit" : "maneuver horizon";
        var seed = includePrefix ? new Checkpoint38(archive.Run.Seed, archive.Run.Summary.Start.Reference - lab.Bend.Entry) : start;
        var trail = new Flight38(state, rows.ToArray(), distances.ToArray(), stop, boundary, ground);
        return new(plan, seed, start, pending, queue, trail, forecastSteps, includePrefix);

        void Append(Flight38 window)
        {
            rows.AddRange(window.Steps);
            distances.AddRange(window.Distances);
            boundary = Math.Min(boundary, window.Boundary);
            ground = Math.Min(ground, window.Ground);
            state = window.Final;
            stop = window.Stop;
        }
    }

    private static float IndependentDrive38(Archive38 archive, Plan38 plan, Checkpoint38 s, double length)
    {
        var nominal = archive.Sample(s.Distance + 1).Before.Command.Bike.Drive;
        if (s.Distance < 0 || plan.Intent == DriveIntent38.Archive) return nominal;
        double distance = s.Distance, spin = archive.Sample(distance).Forces.RearSlip;
        double actualSpin = (archive.Geometry.Base.Radius * s.State.Wheel / archive.Geometry.Base.WheelInertia -
            s.State.Momentum.Plane.Norm / archive.Geometry.Base.Mass) /
            Math.Max(1, s.State.Momentum.Plane.Norm / archive.Geometry.Base.Mass);
        double beta = Math.Abs(SlipAngle38(s.State));
        float desired = plan.Intent switch
        {
            DriveIntent38.EntryPulse => distance < 20 ? .43f : distance < 62 ? .30f : .33f,
            DriveIntent38.Distributed => distance < 6 ? .12f : distance < 65 ? .30f : .43f,
            DriveIntent38.SlipRegulated => (float)Math.Clamp(.30 + .85 * (.105 - Math.Max(actualSpin, spin * .25)), .16, .53),
            DriveIntent38.AfterFormation => beta > Radians38(4.5) ? .48f : .18f,
            DriveIntent38.ExitPower => distance < 48 ? .11f : distance < 80 ? .40f : .58f,
            _ => nominal
        };
        return Math.Max(nominal, desired);
    }

    private sealed record ControlPick38(Command215? Command, Flight38? Prediction, int Steps, double Score);

    private static ControlPick38 Choose38(Lab38 lab, Archive38 archive, Geometry215 g, UnifiedModel215 model,
        Checkpoint38 state, Activation215[] pending, float drive, Vec215 posture, bool searchPosture, SpatialBike36? shape = null)
    {
        // Candidates act on the same mechanical state and irrevocable prefix.
        // Scores measure the propagated force, pose, velocity heading and angular
        // response. A candidate is ineligible if any swept/contact gate fails.
        double time = searchPosture ? .27 : .135;
        var reference = archive.Sample(state.Distance + 2.4);
        var current = state.State;
        var mark = lab.Course.Mark(current, g);
        var refMark = lab.Course.Mark(reference.Midpoint, archive.Geometry);
        double refRate = RollRate36(reference.Midpoint, archive.Geometry.Motion(reference.Midpoint).BodyRate);
        double rate = RollRate36(current, g.Motion(current).BodyRate);
        double error = reference.Midpoint.Pose.Roll - current.Pose.Roll;
        double alignment = Difference38(Heading38(reference.Midpoint), Heading38(current));
        double correction = .20 * error + .044 * (refRate - rate) + .11 * alignment +
            .020 * (mark.Road.OffsetMeters - refMark.Road.OffsetMeters);
        double nominal = archive.Sample(state.Distance + 1).Before.Command.Bike.SteeringRadians;
        double[] steers = searchPosture ? [nominal + correction - .056, nominal + correction, nominal + correction + .056] :
            [nominal + correction, nominal + correction * .53, nominal];
        var targets = searchPosture ? new[] { posture, BodyChoice38(1), BodyChoice38(3) }.Distinct().ToArray() : [posture];
        var pick = new ControlPick38(null, null, 0, double.PositiveInfinity);
        int count = 0;
        foreach (var request in steers.Distinct())
        foreach (var position in targets)
        {
            var command = new Command215(new((float)Math.Clamp(request, -Steering217.MaximumSteeringRadians,
                Steering217.MaximumSteeringRadians), drive), position)
            {
                LeftFoot = g.Leg != null ? Math.Clamp(archive.Sample(state.Distance + 1).Before.Command.LeftFoot, 0, 1) : 0
            };
            var window = Window38(lab, g, model, state, Submit38(state, pending, command), time, shape, forecast: true);
            count += window.Steps.Length;
            if (window.Stop != "verified interval" || window.Steps.Length == 0) continue;
            var after = window.Final.State;
            var follow = archive.Sample(window.Final.Distance);
            var tail = lab.Course.Mark(after, g);
            var target = lab.Course.Mark(follow.After, archive.Geometry);
            var next = window.Steps[^1];
            double bendAccel = LateralAcceleration38(next, g.Base.Mass);
            double refAccel = LateralAcceleration38(follow, archive.Geometry.Base.Mass);
            var w = g.Motion(after);
            var rw = archive.Geometry.Motion(follow.After);
            double rollError = after.Pose.Roll - follow.After.Pose.Roll;
            double rateError = RollRate36(after, w.BodyRate) - RollRate36(follow.After, rw.BodyRate);
            double yawMomentum = (after.Angular.Z - follow.After.Angular.Z) / g.Base.YawCentral;
            double signs = Math.Abs(Difference38(Heading38(after), Heading38(follow.After)));
            double support = Math.Max(0, 220 - next.Forces.Front.Normal) / 350;
            double distanceError = tail.Road.OffsetMeters - target.Road.OffsetMeters;
            double penalty = 5.1 * rollError * rollError + .53 * rateError * rateError + 1.1 * signs * signs +
                .32 * yawMomentum * yawMomentum + .026 * distanceError * distanceError +
                .004 * Math.Pow(bendAccel - refAccel, 2) + .12 * support * support +
                .035 * Math.Pow(Math.Max(0, .035 - window.Ground) / .035, 2);
            if (searchPosture)
                penalty -= .020 * Math.Min(.3, Math.Max(0, next.Forces.RearSlip)) +
                    .035 * Math.Min(.22, Math.Abs(SlipAngle38(after)));
            if (penalty < pick.Score) pick = new(command, window, 0, penalty);
        }
        return pick with { Steps = count };
    }

    private static Outcome38 Summarize38(Lab38 lab, Geometry215 g, Attempt38 attempt)
    {
        var f = attempt.Trail;
        Step215? entry = At(0), exit = At(lab.Bend.Length), twenty = At(lab.Bend.Length + 20);
        var bendRows = f.Steps.Where((s, i) => f.Distances[i] >= 0 && f.Distances[i] <= lab.Bend.Length).ToArray();
        var tail = At(lab.Bend.Length + 8);
        bool balanced = entry != null && exit != null && tail != null &&
            Math.Abs(tail.After.Pose.Roll) <= .22 && Math.Abs(lab.Course.Mark(tail.After, g).AngularVelocity) <= .28;
        double? endSpeed = twenty is null ? null : Speed38(twenty.After, g);
        double? endRoll = twenty?.After.Pose.Roll;
        double? endYaw = twenty is null ? null : lab.Course.Mark(twenty.After, g).AngularVelocity;
        double? exitSpeed = exit is null ? null : Speed38(exit.After, g);
        bool stable20 = balanced && endSpeed >= exitSpeed + .5 && Math.Abs(endRoll ?? 1) <= .22 && Math.Abs(endYaw ?? 1) <= .28;
        return new(attempt.Plan, f.Stop, f.Steps.Length, attempt.ForecastSteps, f.Final.Distance, f.Boundary, f.Ground,
            balanced, stable20, entry is null ? null : Speed38(entry.After, g),
            entry == null || exit == null ? null : exit.After.Time - entry.After.Time,
            bendRows.Length == 0 ? null : bendRows.Min(r => Speed38(r.After, g)), exitSpeed, endSpeed, endRoll, endYaw,
            Measure38(bendRows, g, f.Stop), attempt.Commands.Length);

        Step215? At(double distance)
        {
            int i = Array.FindIndex(f.Distances, s => s >= distance);
            return i < 0 ? null : f.Steps[i];
        }
    }

    private static void WriteRun38(Lab38 lab, string output, string name, Geometry215 g, Attempt38 run)
    {
        var samples = new List<Log38>(run.Trail.Steps.Length);
        double oldEdge = H36Footprint.Clearance(lab.Track, g, run.Seed.State, false);
        double oldGround = H36Footprint.GroundMargin(g, run.Seed.State);
        foreach (var (row, distance) in run.Trail.Steps.Zip(run.Trail.Distances))
        {
            var after = row.After;
            var t = row.Forces;
            var a = LateralAcceleration38(row, g.Base.Mass);
            double edge = H36Footprint.Clearance(lab.Track, g, after, false);
            double ground = H36Footprint.GroundMargin(g, after);
            samples.Add(new(distance, after, row.Before.Command.Bike.Drive, t.EngineRpm, t.NetEngineTorque,
                t.RearSlip, SlipAngle38(after), Math.Atan2(t.Rear.Patch.Velocity.Dot(t.Rear.Patch.Side),
                    t.Rear.Patch.Velocity.Dot(t.Rear.Patch.Forward)), a,
                a / Math.Max(1e-8, Math.Pow(row.Midpoint.Momentum.Plane.Norm / g.Base.Mass, 2)),
                t.Front.Normal, t.Rear.Normal, t.Front.Longitudinal, t.Front.Lateral, t.Rear.Longitudinal, t.Rear.Lateral,
                t.FrontYawBand, t.FrontRollBand, row.RollPitchYawMoment, t.Front.Utilization, t.Rear.Utilization,
                t.LeftFoot?.Contact.NormalForce ?? 0, t.Rider, row.ActuatorPower, row.EnergyResidual, edge, ground,
                Math.Min(edge, oldEdge) - H36Footprint.TransportBound(g, row) / 2,
                Math.Min(ground, oldGround) - H36Footprint.TransportBound(g, row, groundOnly: true) / 2));
            oldEdge = edge;
            oldGround = ground;
        }
        Save(Path.Combine(output, name + ".json.gz"), new FullLog38(Summarize38(lab, g, run), run.Seed.State,
            run.Initial.State, run.InitialCommands, run.Commands, samples.ToArray()), true);
    }
}
