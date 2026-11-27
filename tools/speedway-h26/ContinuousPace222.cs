using System.Diagnostics;
using SharedWorldSpeedway;
using WholeCorner171;

namespace SpeedwayHandling;

internal static partial class Study191
{
    private static Run40 Ride42(Lab38 lab, Geometry215 g, Checkpoint38 root, Checkpoint38 initial, Step215[] prefix,
        double[] stations, Activation215[] earlier, Policy42 policy, double? targetEnd = null, VerifiedCache42? cache = null,
        Action<LegacyStep46>? observed = null)
    {
        var model = lab.Engine(g);
        var next = Map199.At(2, 1);
        var steps = new List<Step215>(prefix);
        var distances = new List<double>(stations);
        var decisions = new List<Decision40>();
        var s = initial;
        var tape = earlier;
        var last = prefix.LastOrDefault()?.Forces;
        double secondExit = next.Exit - lab.Bend.Entry;
        double end = targetEnd ?? secondExit + 22;
        double swept = H36Footprint.Clearance(lab.Track, g, s.State, false);
        double clearance = H36Footprint.GroundMargin(g, s.State);
        double limit = s.State.Time + (targetEnd is null ? 14 : 46);
        string stop = "verified interval";
        while (s.Distance < end && s.State.Time < limit && stop == "verified interval")
        {
            if (lab.Bend.Entry + s.Distance >= next.NextEntry - policy.Line.PreparationMeters && targetEnd is not null)
                next = next.Index == 0 ? Map199.At(next.Lap, 1) : Map199.At(next.Lap + 1, 0);
            long started = Stopwatch.GetTimestamp();
            var (action, summary) = MatchCache42(lab, g, s, last, tape, policy, cache) ??
                Control42(lab, g, model, s, tape, next, last, policy);
            decisions.Add(summary with { Milliseconds = Stopwatch.GetElapsedTime(started).TotalMilliseconds });
            if (action is not { } command) { stop = summary.Refusal ?? "controller candidate exhaustion"; break; }
            tape = Submit38(s, tape, command);
            var r = Window38(lab, g, model, s, tape, Cadence38, finishAt: end, observed: observed);
            s = r.Final;
            steps.AddRange(r.Steps);
            distances.AddRange(r.Distances);
            last = r.Steps.LastOrDefault()?.Forces ?? last;
            swept = Math.Min(swept, r.Boundary);
            clearance = Math.Min(clearance, r.Ground);
            stop = r.Stop;
        }
        if (stop == "verified interval")
            stop = s.Distance >= end ? (targetEnd is null ? "completed linked horizon" : "completed extended horizon") : "linked time horizon";
        return new(policy.Line, root, initial, earlier, tape, new(s, steps.ToArray(), distances.ToArray(), stop, swept, clearance),
            decisions.ToArray());
    }

    private static (Command215? Action, Decision40 Receipt) Control42(Lab38 lab, Geometry215 g, UnifiedModel215 model,
        Checkpoint38 at, Activation215[] tape, BendVisit b, Forces215? last, Policy42 policy)
    {
        if (policy.Family == Family42.Archive)
            return DecideLink40(lab, g, model, policy.Line, at, tape, b, last, policy.Ablation);
        var s = at.State;
        var mark = lab.Course.Mark(s, g);
        var movement = g.Motion(s);
        double yawRate = mark.AngularVelocity;
        double rollRate = RollRate36(s, movement.BodyRate);
        double speed = s.Momentum.Plane.Norm / g.Base.Mass;
        double spin = ContactSlip39(g, s);
        var authority = last is null ? null : ProjectForce42(g, s, last);
        double front = last?.Front.Normal ?? 600;
        double rear = last?.Rear.Normal ?? 800;
        double ground = H36Footprint.GroundMargin(g, s);
        var desired = DecideLink40(lab, g, model, policy.Line, at, tape, b, last, refine: Plan,
            extraDamping: policy.DampingAdd);
        if (policy.SteeringBias != 0 && desired.Action is { } command && front > 205 && ground > .015 &&
            lab.Bend.Entry + at.Distance < b.Entry + 4)
        {
            var steering = Steering39(command.Bike.SteeringRadians + policy.SteeringBias);
            desired = (command with { Bike = command.Bike with { SteeringRadians = steering } }, desired.Receipt);
        }
        return desired;

        RollReference36 Plan(RollReference36 old, RiderView body, double reference)
        {
            double toEntry = b.Entry - reference;
            double fromExit = reference - b.Exit;
            double frontShare = front / Math.Max(1, front + rear);
            double drift = Math.Abs(SlipAngle38(s));
            double powerAuthority = Math.Clamp((frontShare - .12) * 3.4, 0, 1) *
                Math.Clamp(1 - Math.Max(0, spin - .15) * 1.9, 0, 1);
            double roll = old.Roll;
            double drive = old.Drive;
            var rider = old.Rider;
            if (toEntry > 0)
            {
                double braking = last is null ? 2.2 : Math.Clamp(-(last.Drag + last.Rear.Force).Plane
                    .Dot(s.Momentum.Plane / Math.Max(1, s.Momentum.Plane.Norm)) / g.Base.Mass, 1.25, 3.4);
                double lookStation = b.Entry + 12;
                double curve = Math.Max(.001, LineCurvature42(lookStation, policy.Line.Lane));
                double leanAuthority = Gravity216 * Math.Tan(Math.Min(.86, Math.Max(.52, Math.Abs(old.Roll) + .40)));
                double forceBound = authority is null ? leanAuthority : Math.Clamp(
                    authority.MaximumInward + .55 * rear * Grip42 / g.Base.Mass, 4.3, leanAuthority + 2.1);
                forceBound = Math.Min(forceBound, leanAuthority);
                double along = Math.Max(0, toEntry + 8);
                double usable = Math.Sqrt(forceBound / curve + 2 * braking * along);
                double margin = usable - speed;
                double active = powerAuthority * Math.Clamp((margin + policy.CoastBias) / 4.0, 0, 1);
                if (policy.Family == Family42.FullThenPrepare)
                    drive = toEntry / Math.Max(1, speed) > 1.04 && margin > -1 ? Math.Max(drive, active) : drive;
                if (policy.Family == Family42.Concurrent)
                    drive = Math.Max(drive, .30 * active * Math.Clamp(toEntry / 17, 0, 1));
                if (policy.Family == Family42.Directional)
                    drive = Math.Max(drive, active * Math.Clamp(toEntry / 28, 0, 1));
                if (policy.Family == Family42.Offline)
                    drive = Math.Max(drive, active * Smooth37((toEntry - 13) / 24) * .86);
                if (drive > old.Drive)
                {
                    double keep = Math.Clamp(1 - Math.Max(0, Math.Abs(rollRate) - .44) * 1.5 - drift, 0, 1);
                    drive = old.Drive + (drive - old.Drive) * keep;
                }
                double look = Smooth37((policy.Line.PreparationMeters - toEntry) / policy.Line.PreparationMeters);
                rider += new Vec215(policy.BodyBias * look, 0, -.4 * policy.BodyBias * look) * .55;
            }
            else if (ground > .013 && front > 175 && Math.Abs(rollRate) < .65)
            {
                double authorityFactor = Math.Clamp((frontShare - .15) * 3.3, 0, 1);
                double phase = Math.Clamp((reference - b.Entry - 8) / 17, 0, 1);
                double extra = fromExit >= -14 ? policy.ExitDrive * Smooth37((fromExit + 14) / 15) : policy.BendGain * phase;
                if (authority is not null && Math.Abs(yawRate) > .65 && authority.YawMaximum - authority.YawMinimum < 100)
                    extra *= .45;
                drive += extra * authorityFactor * Math.Clamp(1 - drift, .2, 1);
            }
            if (policy.EntryShape != 0)
            {
                double blend = Smooth37((policy.Line.PreparationMeters - toEntry) / 18) *
                    (1 - Smooth37((-toEntry - 1) / 14));
                if (policy.EntryShape == 1 && front > 160 && ground > .012)
                    roll = ClearanceRoll37(g, s, roll - .085 * blend, .009);
                if (policy.EntryShape == 2)
                    rider += new Vec215(.040, .017, -.018) * blend;
                if (policy.EntryShape == 3 && toEntry > 12 && front > 165 && spin < .26)
                    drive = Math.Max(drive, .36 * Math.Clamp((toEntry - 11) / 12, 0, 1) *
                        Math.Clamp((front - 135) / 130, 0, 1));
                if (policy.EntryShape == 4 && front > 200 && ground > .011)
                {
                    double heading = Difference38(Heading38(s), Math.Atan2(mark.Road.Tangent.Y, mark.Road.Tangent.X));
                    roll = ClearanceRoll37(g, s, roll + Math.Clamp(.24 * heading, -.095, .075) * blend, .009);
                }
            }
            if (policy.RollBackup > 0 && toEntry < 23 && fromExit < -24 && rollRate < -.12)
            {
                double brakeRoll = authority is null ? 5 :
                    Math.Clamp(authority.RollMaximum / Geometry215.RollInertia, 3, 11);
                double overshoot = rollRate * rollRate / (2 * brakeRoll);
                // When the currently inward-moving chassis cannot stop before the
                // ground limit, spend some steering authority on angular braking.
                double margin = ClearanceRoll37(g, s, -Geometry215.MaxRoll, .010);
                double predicted = s.Pose.Roll - overshoot;
                if (predicted < margin + .09)
                {
                    roll = Math.Min(0, Math.Max(roll, s.Pose.Roll) + policy.RollBackup *
                        Math.Clamp(margin + .09 - predicted, 0, .38));
                    drive = Math.Min(drive, .19);
                }
            }
            return old with { Drive = (float)Math.Clamp(drive, 0, 1), Roll = roll, Rider = rider };
        }
    }
}
