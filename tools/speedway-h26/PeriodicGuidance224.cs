using SharedWorldSpeedway;
using WholeCorner171;

namespace SpeedwayHandling;

internal static partial class Study191
{
    private readonly record struct LinePoint44(double Offset, double Curvature, double ToEntry, double FromExit);
    private readonly record struct Choice44(Command215? Command, StateDecision44 Note);

    private static BendVisit Bend44(Lab38 lab, double distance, double lead)
    {
        int n = (int)Math.Floor((distance + lead) / Half44);
        int index = (n % 2 + 2) % 2;
        int lap = (n - index) / 2;
        return Map199.At(lab.Bend.Lap + lap, index);
    }

    private static double Lane44(Lab38 lab, Route44 p, double reference)
    {
        double phase = reference - lab.Bend.Entry;
        phase -= Half44 * Math.Floor(phase / Half44);
        double exit = lab.Bend.Length;
        double middle = .53 * exit;
        double early = Math.Clamp(p.EarlyHeading, 1, 10);
        double entry = p.EntryOutside - .30 * early;
        double straight = Half44 - exit;
        double wide = Half44 - Math.Clamp(23 + .36 * (p.LeadMeters - 36), 17, straight - 6);
        if (phase < middle) return Lerp44(entry, p.Inside, phase / middle);
        if (phase < exit + 6) return Lerp44(p.Inside, p.ExitOutside, (phase - middle) / (exit + 6 - middle));
        if (phase < wide) return Lerp44(p.ExitOutside, p.EntryOutside, (phase - exit - 6) / (wide - exit - 6));
        return Lerp44(p.EntryOutside, entry, (phase - wide) / (Half44 - wide));
    }

    private static double Lerp44(double a, double b, double t)
    {
        t = Math.Clamp(t, 0, 1);
        double blend = t * t * t * (10 + t * (-15 + 6 * t));
        return a + (b - a) * blend;
    }

    private static LinePoint44 LineAt44(Lab38 lab, Route44 p, double reference)
    {
        const double sample = 2.8;
        var a = Frame.At(reference - sample, Lane44(lab, p, reference - sample)).Position;
        double lane = Lane44(lab, p, reference);
        var middle = Frame.At(reference, lane).Position;
        var b = Frame.At(reference + sample, Lane44(lab, p, reference + sample)).Position;
        var l = middle - a;
        var r = b - middle;
        double curv = 2 * ((double)l.X * r.Y - (double)l.Y * r.X) /
            Math.Max(1e-8, (double)l.Length() * r.Length() * (b - a).Length());
        var next = Bend44(lab, reference - lab.Bend.Entry, p.LeadMeters);
        return new(lane, curv, next.Entry - reference, reference - next.Exit);
    }

    private static Choice44 Choose44(Lab38 lab, Geometry215 g, UnifiedModel215 model, SpatialBike36 shape,
        Checkpoint38 at, Forces215? last, Activation215[] tape, Route44 p)
    {
        var s = at.State;
        var mark = lab.Course.Mark(s, g);
        var motion = g.Motion(s);
        double rollRate = RollRate36(s, motion.BodyRate);
        double reference = lab.Bend.Entry + at.Distance;
        var bend = Bend44(lab, at.Distance, p.LeadMeters);
        var previous = bend.Index == 0 ? Map199.At(bend.Lap - 1, 1) : Map199.At(bend.Lap, 0);
        double ground = shape.GroundMargin(g, s);
        double frontLoad = last?.Front.Normal ?? 600;
        double slip = ContactSlip39(g, s);
        double heading = Difference38(Heading38(s), Math.Atan2(mark.Road.Tangent.Y, mark.Road.Tangent.X));
        var force = last is null ? null : ProjectForce42(g, s, last);
        double floor = ClearanceRoll37(g, s, -Geometry215.MaxRoll, ground < .028 ? .012 : .009);
        double outwardAuthority = force is null ? 6 : Math.Clamp(force.RollMaximum / Geometry215.RollInertia, 3, 13);
        double oscillation = Math.Max(0, -rollRate);
        double angularDistance = oscillation * ReactionSeconds216 + oscillation * oscillation / (2 * outwardAuthority);
        double aperture = s.Pose.Roll - floor - angularDistance;
        bool settling = reference >= previous.Exit - 15 && reference < previous.Exit + 10;
        double steerGain = p.Guide is Guide44.Continuous ? 22 : 20;
        var tune = new Tune36(p.EntryOutside, p.PreviewMeters, steerGain, p.Damping, 0, 0, 0,
            Math.Max(0, p.EntryOutside - p.Inside), 0, true);
        RollReference36 guide = default;
        double desiredOffset = mark.Road.OffsetMeters;
        int forecasts = 0, recovery = 0;
        bool risking = (ground < .010 && rollRate < -.16) ||
            (aperture < -.045 && rollRate < -.75) || (frontLoad < 75 && Math.Abs(rollRate) > .55);

        var first = Solver(0);
        string? lastRejection = first.Stop;
        if (first.Command is { } accepted && !risking)
            return Finish(accepted, null);

        var pick = new List<(Command215 Cmd, double Score, RollReference36 Guide, double Offset)>();
        if (first.Command is { } held) Check(held);
        foreach (int attempt in new[] { 1, 2, 3 })
        {
            recovery++;
            var option = Solver(attempt);
            if (option.Command is { } command) Check(command);
            else lastRejection = option.Stop ?? lastRejection;
        }
        if (pick.Count == 0)
            return Finish(null, "adaptive candidate exhaustion: " + lastRejection);
        var best = pick.MaxBy(x => x.Score);
        guide = best.Guide;
        desiredOffset = best.Offset;
        return Finish(best.Cmd, null);

        (Command215? Command, int Steps, string? Stop) Solver(int rescue)
        {
            var gains = rescue == 0 ? tune : tune with { Damping = tune.Damping + 2.5 * rescue };
            var result = H36Control(s, g, model, mark, reference, bend, gains, p.StraightKph, .16f, tape, lab.Course,
                observe: q => guide = q, referenceBasis: (body, station) => Target(body, station, rescue));
            forecasts += result.Steps;
            return result;
        }

        void Check(Command215 command)
        {
            // The medium forecast carries the submitted FIFO. The tested action remains
            // executable for two review intervals after its physical activation.
            var possible = Window38(lab, g, model, at, Submit38(at, tape, command), BrakeReaction44, shape, forecast: true);
            forecasts += possible.Steps.Length;
            if (possible.Stop != "verified interval") { lastRejection = possible.Stop; return; }
            var target = possible.Final;
            var b = lab.Course.Mark(target.State, g);
            var rate = RollRate36(target.State, g.Motion(target.State).BodyRate);
            double velocityHeading = Difference38(Heading38(target.State), Math.Atan2(b.Road.Tangent.Y, b.Road.Tangent.X));
            double cost = target.Distance - at.Distance + 3.4 * Math.Min(.08, possible.Ground) +
                .08 * Math.Min(3, possible.Boundary) + .18 * velocityHeading
                - .40 * Math.Max(0, -.25 - rate) - .18 * Math.Abs(target.State.Pose.Roll - guide.Roll);
            pick.Add((command, cost, guide, desiredOffset));
        }

        Choice44 Finish(Command215? action, string? refusal) =>
            new(action, new(s.Time, at.Distance, p.Id, action?.Bike.Drive ?? guide.Drive,
                action?.Bike.SteeringRadians ?? s.Steer, desiredOffset, guide.Roll, guide.SpeedTargetKph, heading, aperture,
                0, forecasts, recovery, refusal));

        RollReference36 Target(RiderView body, double station, int rescue)
        {
            double distanceToTurn = bend.Entry - station;
            double fromLastExit = station - previous.Exit;
            double local = station - bend.Entry;
            double speed = body.Speed;
            double preview = p.PreviewMeters + Math.Clamp((speed - 21) * 1.15, -3, 7);
            double future = station + preview;
            // Near the join, let the next bend's planned path take over through the
            // projected heading, while the current state supplies the lateral correction.
            var aim = LineAt44(lab, p, future);
            double crossError = Lane44(lab, p, station) - body.Road.OffsetMeters;
            double settle = Math.Clamp(fromLastExit / Math.Max(8, (Half44 - lab.Bend.Length) * .42), 0, 1);
            double offset = aim.Offset;
            if (p.Guide == Guide44.PriorBlend)
            {
                double observed = Math.Clamp(body.Road.OffsetMeters, 2.0, 11.3);
                double blend = .36 + .64 * Smooth37((p.LeadMeters + 10 - distanceToTurn) / (p.LeadMeters + 10));
                offset = observed + (offset - observed) * blend;
            }
            desiredOffset = offset;
            double pursuit = PreviewCurvature36(body, future, offset);
            double middle = LineAt44(lab, p, station + .58 * preview).Curvature;
            double curve = p.Guide switch
            {
                Guide44.Continuous => .76 * pursuit + .24 * middle,
                Guide44.PriorBlend => pursuit,
                Guide44.Lookahead => .54 * pursuit + .46 * middle,
                _ => .87 * pursuit + .13 * middle
            };
            // The soft curvature demand can widen on exit. Roll and steering themselves
            // are still generated by the physical actuator, never set on the plant.
            curve = Math.Clamp(curve, -.013 - .005 * (1 - settle), .063);
            if (p.Guide == Guide44.Measured && force is not null)
                curve += .0012 * Math.Clamp((force.CurrentInward - speed * speed * curve) / 5, -1, 1);
            double lean = -Math.Atan(p.LeanScale * speed * speed * curve / Gravity216);
            lean = Math.Clamp(lean, floor, .43);
            if (rescue > 0 || (aperture < .018 && rollRate < -.14))
            {
                double brake = Math.Clamp(.026 - aperture, 0, .30);
                double support = rescue * .046 + .76 * brake;
                lean = Math.Clamp(Math.Max(lean, floor + support), floor, .43);
            }

            double available = p.TurnAcceleration;
            if (p.Guide == Guide44.Measured && force is not null)
                available = Math.Min(available, Math.Max(.67 * available,
                    force.MaximumInward + .64 * (last?.Rear.Normal ?? 0) * Grip42 / g.Base.Mass));
            double targetSpeed = p.StraightKph / 3.6;
            // A curve farther downstream is reachable after a physical coast interval.
            // The horizon is tied to distance and tyre load, independently of the endpoint.
            foreach (double look in new[] { 4d, 10, 18, 29, 42 })
            {
                double nextCurve = Math.Max(.001, LineAt44(lab, p, station + look).Curvature);
                double compatible = Math.Sqrt(available / nextCurve + 2 * p.Deceleration * look);
                targetSpeed = Math.Min(targetSpeed, compatible);
            }
            double free = Math.Clamp(.16 + .19 * (targetSpeed - speed) + p.ThrottleBias * settle, 0, 1);
            if (p.PreserveFrontier && distanceToTurn > 17 && distanceToTurn < 42 && frontLoad > 250)
                free = Math.Max(free, Math.Clamp((distanceToTurn - 17) / 17, 0, 1) * .88);
            double leave = bend.Exit - station;
            if (leave >= -4 && leave < 16 && frontLoad > 225 && Math.Abs(rollRate) < .85)
                free = Math.Max(free, p.ExitDrive * Smooth37((16 - leave) / 20));
            if (settling && fromLastExit >= -3 && fromLastExit < 10 && frontLoad > 130)
                free = Math.Max(free, .27 * (1 - .4 * settle));
            free *= Math.Clamp(1 - 1.4 * Math.Max(0, slip - .23), .24, 1);
            if (rescue > 0) free = Math.Min(free, rescue == 1 ? .28 : rescue == 2 ? .14 : 0);
            if (aperture < -.05 && rollRate < -.50) free = Math.Min(free, .15);
            double phase = Math.Sin(Math.PI * Math.Clamp(local / bend.Length, 0, 1));
            double prepare = Smooth37((p.LeadMeters - distanceToTurn) / Math.Max(1, p.LeadMeters)) *
                (1 - Smooth37(Math.Max(0, local) / 19));
            var posture = new Vec215(.045 * phase + p.RiderBias * prepare, .020 * phase + .005 * Math.Clamp(-crossError, -2, 2),
                -.025 * phase - .32 * p.RiderBias * prepare);
            return new(curve, lean, preview, 3.6 * targetSpeed, (float)Math.Clamp(free, 0, 1), posture, .35 * phase * phase);
        }
    }
}
