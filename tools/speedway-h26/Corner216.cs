
using System.Numerics;
using CoreSim.PhysicalSpace;
using SharedWorldSpeedway;
using WholeCorner171;

namespace SpeedwayHandling;

internal static partial class Study191
{
    private const double FootprintRadius216 = 1.35;
    private const double ExitRunout216 = 8;
    private const double ControlPeriod216 = .035;
    private sealed record CornerTuning216(double Lane, float BendDrive, double InitialBank, double RollGain, bool Leg);
    private sealed record CornerOutcome216(int Id, string Scenario, double? RequestedKph, CornerTuning216 Settings,
        State215 Initial, State215 Final, string Stop, bool Entry, bool Exit, bool QualifiedCorner,
        double InitialReference, double FinalReference, double EntryReference, double ExitReference,
        double Seconds, double? EntrySpeedKph, double? MinimumBendKph,
        double? ExitSpeedKph, double PeakKph, double BendSeconds, double BendFullDrive, double BendFullDriveDecelerating,
        double StraightSeconds, double StraightFullDrive, double SlideSeconds, double MaxContinuousSlide,
        double MinimumSwept, double PeakWheelspin, double MaxRoll, double ExitRoll, double MaxYawRate,
        double PeakCurvature, double PeakLateralAcceleration, double PeakMuscleForce, double PeakMusclePower,
        double MinFront, double MinRear, double PeakFootLoad, double FootContactSeconds, double PassiveDissipation,
        double EnergyRemainder, double MaxEnergyRemainder, int Steps);
    private sealed record CornerTrace216(CornerOutcome216 Summary, Step215[] Trace);

    private static void CornerStudy216(string root, string output, Fixture216 fixture)
    {
        var visit = Map199.At(2, 0);
        var track = new RacingLineGeometry(Frame);
        var template = RootFor210(root, "C").State.State.Rider;
        const double horizon = 9;
        var attempts = new List<CornerOutcome216>();
        int id = 0;
        double[] points = [80, 90, 100, 110, 120];
        foreach (double requested in points)
        {
            foreach (bool boot in new[] { false, true })
            {
                var g = fixture.Geometry(boot ? new Leg216() : null);
                var model = fixture.Engine(g, NominalSupport216, Actuator215.Limited, true);
                CornerTrace216? best = null;
                foreach (double lane in new[] { 3.6, 6.0, 8.4 })
                foreach (float drive in new[] { 0f, .33f, .66f, 1f })
                foreach (double bank in new[] { -.075, -.24 })
                foreach (double gain in new[] { 15d, 28d })
                {
                    var settings = new CornerTuning216(lane, drive, bank, gain, boot);
                    const double approach = 4;
                    var anchor = Frame.At(visit.Entry - approach, lane);
                    var pose = Attitude215.Pose(anchor.Road.Heading, 0, bank);
                    double yawRate = -bank * .55;
                    var state = Seed(g, settings, anchor.Position, pose, requested / 3.6, anchor.Road.Heading, yawRate);
                    var result = Run(++id, "speed-grid", requested, g, model, state, visit.Entry - approach, visit, settings, horizon);
                    attempts.Add(result.Summary);
                    if (best is null || Rank(result.Summary, best.Summary)) best = result;
                }
                foreach (var (bank, drive, gain) in new[] { (-.43, 0f, 9d), (-.63, .33f, 12d) })
                {
                    var tuning = new CornerTuning216(4.5, drive, bank, gain, boot);
                    const double approach = 4;
                    double entryS = visit.Entry - approach;
                    var anchor = Frame.At(entryS, tuning.Lane);
                    double speed = requested / 3.6;
                    double radius = Frame.RoadAt(visit.Entry + .01, tuning.Lane).RadiusMeters;
                    var pose = Attitude215.Pose(anchor.Road.Heading + .15, 0, bank);
                    var state = Seed(g, tuning, anchor.Position, pose, speed, anchor.Road.Heading + .035,
                        Math.Min(.85, .75 * speed / radius));
                    var result = Run(++id, "speed-grid-prebank", requested, g, model, state, entryS, visit, tuning, horizon);
                    attempts.Add(result.Summary);
                    if (best is null || Rank(result.Summary, best.Summary)) best = result;
                }
                if (best is not null)
                {
                    string mode = boot ? "leg" : "without-leg";
                    Save(Path.Combine(output, $"corner-best-{requested:F0}-{mode}.json.gz"), best, true);
                    Console.WriteLine($"{requested:F0} km/h {mode}: {best.Summary.Stop}, " +
                        $"distance={best.Summary.FinalReference - visit.Entry:F2}m from entry, " +
                        $"drive={best.Summary.BendFullDrive:F3}s");
                }
            }
        }
        Save(Path.Combine(output, "corner-search.json"), new { Visit = visit, Horizon = horizon, Attempts = attempts });
        var phases = new List<CornerOutcome216>();
        foreach (var name in new[] { "corner-entry", "mid-bend", "exit" })
        {
            var frozenPath = Path.Combine(root, "data/experiments/speedway215", $"matched-{name}-G-limited-u.json.gz");
            var prior = Read199<Run215>(frozenPath).Summary.Initial;
            var p = new Vector2((float)prior.Center.X, (float)prior.Center.Y);
            var mark = Frame.Project(p);
            double start = 2 * Frame.LapMeters + mark.ReferenceMeters;
            var location = Map199.Locate(start);
            var matchingVisit = location.Current ?? location.Next;
            var v = prior.Momentum / fixture.Base.Mass;
            double direction = Math.Atan2(v.Y, v.X);
            double yawDiff = Math.Clamp(BikeAngles.Wrap(prior.Pose.Yaw - direction), -.34, .34);
            double curvature = mark.IsTurn ? 1 / mark.RadiusMeters : 0;
            double bank = -Math.Min(.66, Math.Atan(v.Plane.Dot(v.Plane) * curvature / Gravity216));
            foreach (bool boot in new[] { false, true })
            {
                var g = fixture.Geometry(boot ? new Leg216() : null);
                var model = fixture.Engine(g, NominalSupport216, Actuator215.Limited, true);
                var configuration = new CornerTuning216(Math.Clamp(mark.OffsetMeters, 2, 8.2), .66f, bank, 21, boot);
                var pose = Attitude215.Pose(direction + yawDiff, prior.Pose.Pitch, bank);
                var seed = Seed(g, configuration, p, pose, v.Plane.Norm, direction, v.Plane.Norm * curvature * .75);
                var result = Run(++id, name, null, g, model, seed, start, matchingVisit, configuration, 1.45);
                phases.Add(result.Summary);
                Save(Path.Combine(output, $"phase-{name}-{(boot ? "leg" : "without-leg")}.json.gz"), result, true);
                if (result.Summary.Steps == 0)
                {
                    double lane = Math.Clamp(mark.OffsetMeters, 2.8, 7.6);
                    var staged = Seed(g, configuration with { Lane = lane }, Frame.At(start, lane).Position,
                        pose, v.Plane.Norm, direction, v.Plane.Norm * curvature * .75);
                    var shifted = Run(++id, name + "-offset-diagnostic", null, g, model, staged, start, matchingVisit,
                        configuration with { Lane = lane }, 1.45);
                    phases.Add(shifted.Summary);
                    Save(Path.Combine(output, $"phase-{name}-offset-{(boot ? "leg" : "without-leg")}.json.gz"), shifted, true);
                }
            }
        }
        Save(Path.Combine(output, "reconstructed-phases.json"), phases);

        State215 Seed(Geometry215 g, CornerTuning216 tuning, Vector2 p, Attitude215 pose, double speed,
            double velocityBearing, double yawRate)
        {
            var seated = NominalSupport216.SeatedReference(g, Gravity216);
            var poseOffset = new Vec215(.014, tuning.InitialBank * -.04, 0);
            var localVelocity = Vec214.Axis(velocityBearing) * (fixture.Base.Mass * speed);
            var omega = pose.Body(Vec215.Up * yawRate);
            double wheelSpeed = speed / fixture.Base.Radius;
            var first = new State215(fixture.Release + 1, new(p.X, p.Y, .55), new(localVelocity.X, localVelocity.Y, 0),
                pose, default, seated + poseOffset, default, (wheelSpeed * fixture.Ratio + omega.Y) * fixture.Base.CrankInertia,
                (wheelSpeed + omega.Y) * fixture.Base.WheelInertia, 0, new(new(0, 0), poseOffset));
            first = g.Supported(first, Gravity216, UnifiedModel215.Stiffness);
            first = g.WithRates(first, first.Pose.Body(Vec215.Up * yawRate), default);
            var patch = g.Contact(first, g.Motion(first), true, 0);
            double angle = Math.Atan2(patch.Velocity.Dot(patch.Side), Math.Max(1, patch.Velocity.Dot(patch.Forward)));
            float steer = (float)Math.Clamp(angle - .022, -.20, .20);
            return first with { Steer = steer, Command = new(new(steer, 0), poseOffset) };
        }

        CornerTrace216 Run(int attempt, string scenario, double? request, Geometry215 g, UnifiedModel215 model, State215 start,
            double initialS, BendVisit target, CornerTuning216 tuning, double seconds)
        {
            var state = start;
            var steps = new List<Step215>();
            Activation215[] commands = [];
            var mark = Mark(state, g);
            double s = initialS, next = state.Time, here = Clearance(mark), minimum = here;
            double? entrySpeed = null, exitSpeed = null, minimumBend = null;
            bool entry = false, exit = false;
            double end = start.Time + seconds;
            double wot = 0, wotDecel = 0, inBend = 0, straight = 0, straightWot = 0, slide = 0, consecutive = 0, bestSlide = 0;
            double maxYaw = 0, maxCurve = 0, maxLat = 0, footSeconds = 0, energy = 0, maxEnergy = 0;
            string stop = "time budget";
            while (state.Time < end - 1e-9 && steps.Count < 30_000)
            {
                if (here < .50) { stop = "swept boundary clearance"; break; }
                if (state.Time >= next - 1e-10)
                {
                    var motion = g.Motion(state);
                    var previous = steps.Count > 0 ? steps[^1].Forces : null;
                    var act = Control216(g, state, motion, mark.Road, s, target, tuning, previous);
                    commands = [.. commands, new(state.Time, state.Time + ReactionSeconds216, act)];
                    next += ControlPeriod216;
                }
                try
                {
                    double interval = Math.Min(Math.Min(StepSeconds216, end - state.Time), next - state.Time);
                    var row = model.Step(state, commands, interval);
                    double dt = Duration215(row);
                    if (dt <= 0) throw new InvalidOperationException("clock stalled");
                    var after = Mark(row.After, g);
                    var delta = Frame.UnwrapDelta(mark.Road.ReferenceMeters, after.Road.ReferenceMeters);
                    double sAfter = s + delta;
                    double endClearance = Clearance(after);
                    double swept = Math.Min(here, endClearance) - (row.After.Center - row.Before.Center).Plane.Norm / 2;
                    minimum = Math.Min(minimum, swept);
                    steps.Add(row);
                    var v = row.Midpoint.Momentum.Plane / g.Base.Mass;
                    var accel = (row.After.Momentum - row.Before.Momentum).Plane / (dt * g.Base.Mass);
                    double lateral = v.Cross(accel) / Math.Max(.25, v.Norm);
                    maxLat = Math.Max(maxLat, Math.Abs(lateral));
                    maxCurve = Math.Max(maxCurve, Math.Abs(lateral) / Math.Max(.0625, v.Dot(v)));
                    maxYaw = Math.Max(maxYaw, Math.Abs(after.AngularVelocity));
                    energy += row.EnergyResidual;
                    maxEnergy = Math.Max(maxEnergy, Math.Abs(energy));
                    if (row.Forces.LeftFoot?.Contact.NormalForce > 0) footSeconds += dt;
                    if (s < target.Entry && sAfter >= target.Entry) { entry = true; entrySpeed = 3.6 * after.Speed; }
                    if (s < target.Exit && sAfter >= target.Exit) { exit = true; exitSpeed = 3.6 * after.Speed; }
                    if ((s + sAfter) / 2 >= target.Entry && (s + sAfter) / 2 < target.Exit)
                    {
                        inBend += dt;
                        minimumBend = Math.Min(minimumBend ?? double.PositiveInfinity, after.Speed * 3.6);
                        if (row.Before.Command.Bike.Drive >= .9999)
                        {
                            wot += dt;
                            if (accel.Dot(v) < 0) wotDecel += dt;
                        }
                        if (Math.Abs(after.SlideRadians) >= Math.PI / 30 && row.Forces.Rear.Normal > 20)
                        {
                            slide += dt;
                            consecutive += dt;
                            bestSlide = Math.Max(bestSlide, consecutive);
                        }
                        else consecutive = 0;
                    }
                    else { straight += dt; if (row.Before.Command.Bike.Drive >= .9999) straightWot += dt; }
                    state = row.After;
                    mark = after;
                    s = sAfter;
                    here = endClearance;
                    if (swept < .50) { stop = "swept boundary clearance"; break; }
                    if (Math.Abs(after.SlideRadians) > Math.PI / 4) { stop = "sideslip envelope"; break; }
                    if (Vector2.Dot(after.Road.Tangent, after.Velocity) < .15) { stop = "lost forward progress"; break; }
                    if (s >= target.Exit + ExitRunout216)
                    {
                        stop = !entry ? "exit diagnostic only" : bestSlide < .12 ? "no sustained slide" :
                            Math.Abs(state.Pose.Roll) > .22 || Math.Abs(after.AngularVelocity) > .28 ?
                                "exit balance gate" : "completed corner";
                        break;
                    }
                }
                catch (InvalidOperationException error) { stop = error.Message; break; }
            }
            if (steps.Count >= 30_000) stop = "step budget";
            double Max(Func<Step215, double> f) => steps.Select(f).DefaultIfEmpty(0).Max();
            double Min(Func<Step215, double> f) => steps.Select(f).DefaultIfEmpty(0).Min();
            double peakSpeed = Math.Max(3.6 * start.Momentum.Plane.Norm / g.Base.Mass,
                Max(row => 3.6 * row.After.Momentum.Plane.Norm / g.Base.Mass));
            var summary = new CornerOutcome216(attempt, scenario, request, tuning, start, state, stop, entry, exit,
                stop == "completed corner", initialS, s, target.Entry, target.Exit, state.Time - start.Time,
                entrySpeed, minimumBend, exitSpeed, peakSpeed,
                inBend, wot, wotDecel, straight, straightWot, slide, bestSlide, minimum,
                Max(r => Math.Abs(r.Forces.RearSlip)), Max(r => Math.Abs(r.After.Pose.Roll)), state.Pose.Roll, maxYaw,
                maxCurve, maxLat, Max(r => r.Forces.Rider.Norm + Math.Abs(r.Forces.LeftFoot?.MuscleForce ?? 0)),
                Max(r => r.Forces.LeftFoot?.AbsoluteMusclePower ?? Math.Abs(r.ActuatorPower)),
                Min(r => r.Forces.Front.Normal), Min(r => r.Forces.Rear.Normal),
                Max(r => r.Forces.LeftFoot?.Contact.NormalForce ?? 0), footSeconds,
                steps.Sum(r => (r.Forces.PassiveSupport?.Loss ?? 0) * Duration215(r)), energy, maxEnergy, steps.Count);
            return new(summary, steps.ToArray());
        }

        RiderView Mark(State215 state, Geometry215 g)
        {
            var point = new Vector2((float)state.Center.X, (float)state.Center.Y);
            var heading = state.Pose.Apply(Vec215.Forward);
            var forwardRate = g.Motion(state).WorldRate.Cross(heading);
            return template with
            {
                Position = point, Road = Frame.Project(point), Heading = (float)state.Pose.Yaw,
                Velocity = new((float)(state.Momentum.X / g.Base.Mass), (float)(state.Momentum.Y / g.Base.Mass)),
                AngularVelocity = (float)(heading.Plane.Cross(forwardRate.Plane) / heading.Plane.Dot(heading.Plane))
            };
        }

        double Clearance(RiderView s)
        {
            var (inner, outer) = track.Clearance(s);
            return Math.Min(inner, outer) - FootprintRadius216;
        }

        static bool Rank(CornerOutcome216 candidate, CornerOutcome216 best) =>
            candidate.QualifiedCorner != best.QualifiedCorner ? candidate.QualifiedCorner :
            candidate.QualifiedCorner ? candidate.Seconds < best.Seconds : candidate.FinalReference > best.FinalReference;
    }

    private static Command215 Control216(Geometry215 geometry, State215 s, Kinematics215 motion, RoadPoint road,
        double reference, BendVisit bend, CornerTuning216 tuning, Forces215? observed)
    {
        var speed = s.Momentum.Plane.Norm / geometry.Base.Mass;
        var heading = Math.Atan2(s.Momentum.Y, s.Momentum.X);
        double phase = Math.Clamp((reference - bend.Entry) / bend.Length, 0, 1);
        double line = tuning.Lane - 1.60 * Math.Sin(phase * Math.PI);
        double radius = Frame.RoadAt(Math.Clamp(reference + 5, bend.Entry + .01, bend.Exit - .01), line).RadiusMeters;
        double turnWeight = Math.Clamp((reference - bend.Entry + 8) / 13, 0, 1) *
            Math.Clamp((bend.Exit + 1 - reference) / 9, 0, 1);
        double course = road.Heading + Math.Atan2(road.OffsetMeters - line, 11);
        double correction = Math.Clamp(BikeAngles.Wrap(course - heading), -.34, .34);
        double centripetal = turnWeight * speed * speed / Math.Max(15, radius);
        double targetLean = -Math.Atan(Math.Clamp(centripetal + 1.8 * speed * correction, -10.5, 11.8) / Gravity216);
        double roll = s.Pose.Roll;
        double desiredRate = tuning.RollGain * (targetLean - roll - ReactionSeconds216 * motion.BodyRate.X) -
            5 * motion.BodyRate.X;
        var unsteered = geometry.Contact(s with { Steer = 0 }, motion, true, 0);
        double aligned = Math.Atan2(unsteered.Velocity.Dot(unsteered.Side), Math.Max(1, unsteered.Velocity.Dot(unsteered.Forward)));
        double steer = aligned - .036 * roll + .034 * desiredRate;
        if (observed is { Front.Capacity: > 40 } last)
        {
            var front = last.Front;
            var world = front.Patch.Arm.Cross(Vec215.Up * front.Normal) +
                last.Rear.Patch.Arm.Cross(last.Rear.Force) +
                (last.LeftFoot is { } boot ? boot.Arm.Cross(boot.Force) : default);
            double existing = s.Pose.Body(world).X;
            double lever = s.Pose.Body(unsteered.Arm.Cross(unsteered.Side)).X;
            double gyro = motion.BodyRate.Cross(s.Pose.Body(s.Angular)).X;
            double moment = 20 * desiredRate + gyro;
            double utilization = Math.Clamp((moment - existing) / (Math.Max(.15, lever) * front.Capacity), -.90, .90);
            steer = aligned + Math.Atan(.115 * utilization / Math.Sqrt(1 - utilization * utilization));
        }
        float drive = tuning.BendDrive;
        if (reference < bend.Entry + 2 && centripetal > 10.5) drive = 0;
        if (reference > bend.Exit - 8) drive = (float)Math.Min(1, drive + .35);
        var move = new Vec215(.085 - .085 * phase, -.10 * targetLean, -.025 * turnWeight);
        double foot = tuning.Leg ? Math.Clamp((Math.Abs(targetLean) - .24) * 1.23, 0, .75) : 0;
        return new Command215(new((float)Math.Clamp(steer, -.37, .37), drive), move) { LeftFoot = foot };
    }
}
