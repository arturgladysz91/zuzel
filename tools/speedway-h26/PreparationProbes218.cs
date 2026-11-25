
using SharedWorldSpeedway;
using WholeCorner171;

namespace SpeedwayHandling;

internal static partial class Study191
{
    private sealed record Perturbation37(double StartLaneDelta, double StartSpeedDeltaKph, int ActualSteps,
        bool Balanced, bool Recovered, bool InBand, Corner36 Corner, Metric37 Metrics, double? AccelerationKph);
    private sealed record StraightTuning37(double Preview, double Gain, double Damping, double ReferenceScale,
        double LaneBias, double AccelerationTarget);
    private sealed record ForkPoint37(double Elapsed, double Roll, double RollRate, double Yaw, double Sideslip,
        double SpeedKph, double FrontNormal, double FrontRollBand, double ActualSteer, double NetRollMoment,
        double RearSlip, double Ground, double Boundary);
    private sealed record Fork37(string Archive, string Intervention, double Distance, Command215 Command,
        int Steps, string Stop, double MinimumFrontNormal, double MeanFrontNormal, double MeanFrontRollBand,
        double MeanFrontRollMoment, double RollDelta, double RollRateDelta, double SpeedDeltaKph,
        double RotorCouple, double RotorSpeedMatchedCounterpartCouple, double MatchedRotorRollAccelDifference,
        ForkPoint37 Start, ForkPoint37 Final, State215 InitialState, Activation215[] Commands, ForkPoint37[] Timeline);

    private static Runout37 CheckRunout37(Fixture216 fixture, Geometry215 g, RacingLineGeometry track,
        CornerCourse216 course, BendVisit bend, Trace36 replay, bool progressive = false,
        double continuationSeconds = ContinuationSeconds37, StraightTuning37? tuning = null)
    {
        var engine = fixture.Engine(g, NominalSupport216, Actuator215.Limited, true);
        var state = replay.Trace[^1].After;
        var initial = state;
        double next = state.Time, endTime = initial.Time + continuationSeconds, reference = replay.Summary.Finish.Reference;
        var mark = course.Mark(state, g);
        double startSpeed = mark.Speed * 3.6;
        double boundary = H36Footprint.Clearance(track, g, state, exact: !progressive);
        double ground = H36Footprint.GroundMargin(g, state);
        double min = boundary, lowest = ground, maxRoll = Math.Abs(state.Pose.Roll), work = 0;
        var queue = replay.Commands;
        var rows = new List<Step215>();
        string stop = "stable acceleration";
        var exitTune = replay.Summary.Tune with
        {
            ApexPaceDropKph = 0, ApproachSpeedCorrectionKph = 0, Fore = 0, Side = 0, Vertical = 0, Boot = 0,
            Lane = Math.Clamp(mark.Road.OffsetMeters, 4, 8.6)
        };
        if (progressive) exitTune = exitTune with { Lookahead = 17, RollGain = 19, Damping = 13 };
        if (tuning is { } adjustment)
            exitTune = exitTune with { Lookahead = adjustment.Preview, RollGain = adjustment.Gain,
                Damping = adjustment.Damping, Lane = exitTune.Lane + adjustment.LaneBias };
        while (state.Time < endTime - 1e-9)
        {
            if (state.Time >= next - 1e-10)
            {
                double speedGain = progressive ? 2 + (tuning?.AccelerationTarget ?? 7) *
                    Smooth37((state.Time - initial.Time - .19) / .75) : 17;
                var control = H36Control(state, g, engine, mark, reference, bend, exitTune,
                    startSpeed + speedGain, .15f, queue, course, rollReferenceScale: tuning?.ReferenceScale ?? 1);
                if (control.Command is not { } request) { stop = control.Stop!; break; }
                queue = [.. queue, new(state.Time, state.Time + ReactionSeconds216, request)];
                next += H36ControlPeriod;
            }
            try
            {
                var step = engine.Step(state, queue, Math.Min(StepSeconds216, Math.Min(next - state.Time, endTime - state.Time)));
                if (!Accept37(step, track, course, g, boundary, ground, out var tail, out double edge, out double floor, out stop, exact: !progressive))
                    break;
                reference += Frame.UnwrapDelta(mark.Road.ReferenceMeters, tail.Road.ReferenceMeters);
                state = step.After;
                mark = tail;
                boundary = H36Footprint.Clearance(track, g, state, exact: !progressive);
                ground = H36Footprint.GroundMargin(g, state);
                min = Math.Min(min, edge);
                lowest = Math.Min(lowest, floor);
                maxRoll = Math.Max(maxRoll, Math.Abs(state.Pose.Roll));
                work += step.EnergyResidual;
                rows.Add(step);
            }
            catch (InvalidOperationException e) { stop = e.Message; break; }
        }
        double finalSpeed = mark.Speed * 3.6;
        bool stable = state.Time >= endTime - 1e-9 && Math.Abs(state.Pose.Roll) <= .22 &&
            Math.Abs(mark.AngularVelocity) <= .28 && finalSpeed >= startSpeed + 1;
        if (continuationSeconds > ContinuationSeconds37 && stable)
        {
            var settled = rows.Where(r => r.After.Time > endTime - .25).ToArray();
            stable = reference < bend.NextEntry && settled.All(r => Math.Abs(r.After.Pose.Roll) <= .22 &&
                Math.Abs(course.Mark(r.After, g).AngularVelocity) <= .28) &&
                finalSpeed >= course.Mark(settled[0].Before, g).Speed * 3.6;
        }
        if (state.Time >= endTime - 1e-9) stop = stable ? "stable acceleration" : "straight balance or acceleration gate";
        return new(initial.Time, state.Time, startSpeed, finalSpeed, min, lowest, maxRoll, stable, work,
            stop, state, queue, rows.ToArray());
    }

    private static Perturbation37[] Perturbations37(Fixture216 fixture, Geometry215 g, RacingLineGeometry track,
        CornerCourse216 course, BendVisit bend, Candidate37 setup, Trace36 nominal, float equilibriumDrive,
        bool progressive = false, double continuationSeconds = ContinuationSeconds37, StraightTuning37? tuning = null)
    {
        var roots = new[] { (-.65, -.14), (-.65, .14), (.65, -.14), (.65, .14) };
        var cases = new Perturbation37[roots.Length];
        Parallel.For(0, roots.Length, new ParallelOptions { MaxDegreeOfParallelism = Math.Min(4, Environment.ProcessorCount) }, i =>
        {
            var engine = fixture.Engine(g, NominalSupport216, Actuator215.Limited, true);
            var (speed, lane) = roots[i];
            var b = SteadyCircle215(engine, g, fixture.Rest(g, NominalSupport216), (40 + speed) / 3.6,
                0, nominal.Seed.Time, fixture.Ratio, balanceRider: true);
            if (b.Residual > .005) throw new InvalidOperationException("Perturbed approach equilibrium did not converge.");
            var seed = Place36(b.State, bend.Entry - PreparationStart37, nominal.Summary.Start.Offset + lane);
            var replay = RunH36(setup.Id, "approach perturbation", setup.TargetKph, setup.Tune, g,
                fixture.Engine(g, NominalSupport216, Actuator215.Limited, true), seed, bend, track, course,
                equilibriumDrive, null, !progressive, preparation: setup.Plan, startingMeters: PreparationStart37);
            var runout = replay.Summary.Balanced ? CheckRunout37(fixture, g, track, course, bend, replay, progressive, continuationSeconds, tuning) : null;
            cases[i] = new(lane, speed, replay.Trace.Length + (runout?.Trace.Length ?? 0), replay.Summary.Balanced,
                runout?.Stable == true, replay.Summary.Entry is { } e && Math.Abs(e.Kph - setup.TargetKph) <= 2,
                replay.Summary, Metrics37(replay, g.Base.Mass, bend),
                runout is { } r ? r.FinalKph - r.InitialKph : null);
        });
        return cases;
    }

    private static bool Accept37(Step215 row, RacingLineGeometry track, CornerCourse216 course,
        Geometry215 g, double beforeEdge, double beforeGround, out RiderView mark, out double boundary, out double ground,
        out string stop, bool exact = true)
    {
        mark = course.Mark(row.After, g);
        boundary = Math.Min(beforeEdge, H36Footprint.Clearance(track, g, row.After, exact)) -
            H36Footprint.TransportBound(g, row) / 2;
        ground = Math.Min(beforeGround, H36Footprint.GroundMargin(g, row.After)) -
            H36Footprint.TransportBound(g, row, groundOnly: true) / 2;
        stop = ground <= 0 ? "chassis swept ground margin" :
            CornerCourse216.Violation(mark, boundary) ?? "verified interval";
        return stop == "verified interval";
    }

    private static Fork37[] Interventions37(Fixture216 fixture, Geometry215 g, RacingLineGeometry track,
        CornerCourse216 course, BendVisit bend, Trace36 tape, Evidence37 evidence, float equilibriumDrive)
    {
        var reviews = new List<Fork37>();
        var engine = fixture.Engine(g, NominalSupport216, Actuator215.Limited, true);
        foreach (double target in new[] { -22d, -12d, -5d, 0d, 5d })
        {
            var request = evidence.Requests.First(e => e.Distance >= target);
            var initial = tape.Trace.First(r => r.Before.Time >= request.RequestedAt - 1e-10).Before;
            var history = tape.Commands[..request.Index];
            var mark = course.Mark(initial, g);
            double reference = bend.Entry + request.Distance;
            var tune = tape.Summary.Tune;
            var baseCommand = request.Command;
            var perturb = new List<(string, Command215)> { ("held recorded request", baseCommand) };
            perturb.Add(("coast", baseCommand with { Bike = baseCommand.Bike with { Drive = 0 } }));
            perturb.Add(("full drive", baseCommand with { Bike = baseCommand.Bike with { Drive = 1 } }));
            perturb.Add(("countersteer", baseCommand with { Bike = baseCommand.Bike with
            {
                SteeringRadians = (float)Math.Max(-Steering217.MaximumSteeringRadians, initial.Steer - .085)
            } }));
            perturb.Add(("steer inward", baseCommand with { Bike = baseCommand.Bike with
            {
                SteeringRadians = (float)Math.Min(Steering217.MaximumSteeringRadians, initial.Steer + .085)
            } }));
            perturb.Add(("earlier lateral posture", baseCommand with { Target = baseCommand.Target with { Y = .065 } }));
            perturb.Add(("forward posture", baseCommand with { Target = baseCommand.Target with { X = .055 } }));
            AddSteering("longer curvature preview", tune with { Lookahead = tune.Lookahead + 9 });
            AddSteering("lower roll damping", tune with { Damping = tune.Damping * .5 });
            AddSteering("stronger roll damping", tune with { Damping = tune.Damping * 1.5 });
            foreach (var (name, position) in perturb) reviews.Add(Fork(name, position));

            void AddSteering(string label, Tune36 settings)
            {
                var control = H36Control(initial, g, engine, mark, reference, bend, settings,
                    tape.Summary.NominalEntryKph, equilibriumDrive, history, course);
                if (control.Command is { } c)
                    perturb.Add((label, baseCommand with { Bike = baseCommand.Bike with { SteeringRadians = c.Bike.SteeringRadians } }));
            }

            Fork37 Fork(string name, Command215 control)
            {
                var model = fixture.Engine(g, NominalSupport216, Actuator215.Limited, true);
                var queue = history.Append(new(initial.Time, initial.Time + ReactionSeconds216, control)).ToArray();
                var state = initial;
                double horizon = initial.Time + .27;
                double boundary = H36Footprint.Clearance(track, g, state);
                double ground = H36Footprint.GroundMargin(g, state);
                var before = Sample37(g, course, initial, initial.Time, boundary, ground, null);
                var after = before;
                var series = new List<ForkPoint37>();
                double load = 0, band = 0, moment = 0, seconds = 0, minLoad = double.MaxValue;
                string stop = "verified horizon";
                while (state.Time < horizon - 1e-9)
                {
                    try
                    {
                        var row = model.Step(state, queue, Math.Min(StepSeconds216, horizon - state.Time));
                        if (!Accept37(row, track, course, g, boundary, ground, out _, out _, out _, out var failure))
                        { stop = failure; break; }
                        state = row.After;
                        boundary = H36Footprint.Clearance(track, g, state);
                        ground = H36Footprint.GroundMargin(g, state);
                        after = Sample37(g, course, state, initial.Time, boundary, ground, row);
                        if (row.After.Time >= initial.Time + ReactionSeconds216 - 1e-10)
                        {
                            double dt = Duration215(row);
                            load += after.FrontNormal * dt;
                            band += after.FrontRollBand * dt;
                            moment += state.Pose.Apply(Vec215.Forward).Dot(row.Forces.Front.Patch.Arm.Cross(row.Forces.Front.Force)) * dt;
                            minLoad = Math.Min(minLoad, after.FrontNormal);
                            seconds += dt;
                        }
                        series.Add(after);
                    }
                    catch (InvalidOperationException e) { stop = e.Message; break; }
                }
                var axes = g.Motion(initial);
                var axis = initial.Pose.Apply(Vec215.Left);
                double combined = initial.Crank + initial.Wheel;
                double speedMatchedWheel = initial.Wheel * (77.50 / Math.Max(77.50, before.SpeedKph));
                var rotor = -axes.WorldRate.Cross(axis * combined);
                var matched = -axes.WorldRate.Cross(axis * (speedMatchedWheel + initial.Crank));
                var forward = initial.Pose.Apply(Vec215.Forward);
                var deltaBodyRate = g.Motion(initial with { Angular = initial.Angular + rotor - matched }).BodyRate - axes.BodyRate;
                return new(tape.Summary.Case, name, request.Distance, control, series.Count, stop,
                    seconds > 0 ? minLoad : 0, seconds > 0 ? load / seconds : 0, seconds > 0 ? band / seconds : 0,
                    seconds > 0 ? moment / seconds : 0, after.Roll - before.Roll, after.RollRate - before.RollRate,
                    after.SpeedKph - before.SpeedKph, forward.Dot(rotor), forward.Dot(matched),
                    RollRate36(initial, deltaBodyRate), before, after, initial, queue, series.ToArray());
            }
        }
        return reviews.ToArray();
    }

    private static ForkPoint37 Sample37(Geometry215 g, CornerCourse216 course, State215 state, double t,
        double boundary, double ground, Step215? row)
    {
        var mark = course.Mark(state, g);
        return new(state.Time - t, state.Pose.Roll, RollRate36(state, g.Motion(state).BodyRate),
            state.Pose.Yaw, mark.SlideRadians, mark.Speed * 3.6, row?.Forces.Front.Normal ?? 0,
            row?.Forces.FrontRollBand ?? 0, state.Steer, row is { } n ? state.Pose.Apply(Vec215.Forward).Dot(n.RollPitchYawMoment) : 0,
            row?.Forces.RearSlip ?? 0, ground, boundary);
    }
}
