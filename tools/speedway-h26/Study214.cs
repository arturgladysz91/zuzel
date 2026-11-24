using System.Numerics;
using CoreSim;
using Safety153;
using SharedWorldSpeedway;

namespace SpeedwayHandling;

internal static partial class Study191
{
    private sealed record Rejected214([property: System.Text.Json.Serialization.JsonPropertyName("fault")] ContactFailure203 Fault);
    private sealed record Summary214(string Case, string Arm, double Dt, string Stop, double Time,
        double FinalKph, double Reference, double MinFront, double MinRear, double FrontAir,
        double FirstLift, double LastTouchdown, double MaxPitchDeg, double FrontYawSpan, double MeanFrontYawSpan,
        double FinalYawRate, double ExitCourseDegrees, double PeakAcceleration, double MaxFrontLoad,
        double MaxRearFx, double PeakRpm, double MinSweepProxy, double MinPaddedScreen, double RiderWork,
        double ImpactRiderWork, double ImpactLoss, double MaxEnergyDefect, double FinalEnergyDefect,
        double PeakAccumulatedDefect, double MinimumGap, double MaxAirForce,
        double MaxNormalWork, double MaxPitchImpulseError, double MaxVerticalImpulseError,
        double MaxRiderForce, double MaxRiderSpeed, double MaxRiderAcceleration,
        double OrdinarySeconds, double OrdinaryWot, double OrdinaryPeakKph,
        double FinishSeconds, double FinishWot, double FinishPeakKph,
        double BendSeconds, double BendWot, double BendPeakKph, int Steps, int Impacts, int Subdivisions);
    private sealed record EvidenceRun214(Summary214 Summary, Step214[] Trace);
    private sealed record Arm214(string Name, bool MoveLong, bool MoveVertical);

    private static void Unified214(string root, string output)
    {
        const string base213 = "data/experiments/speedway213";
        const double nominalDt = .005;
        var standing = RootFor210(root, "C");
        var (_, spec) = CourseFixture208(root, "C");
        var settings = Settings202("H28-F");
        double release = RiderController.ReactionSeconds(spec);
        var grip = Surface.SampleSurface(0, 2);
        var geometry = Geometry214.Fixture(Mass, spec.Profile.Gameplay.Physical.MassKg,
            Physics.TwoContactBike ?? TwoContactBikeParameters.Default,
            Physics.RearWheel ?? RearWheelParameters.Default, Inertia);
        var backend = new ModelHorizontal214();
        var model = new UnifiedModel214(geometry, new(geometry, spec, settings, Physics, grip, release, false),
            backend, release);
        var dry = new UnifiedModel214(geometry, new(geometry, spec, settings, Physics, grip, double.PositiveInfinity, true),
            backend, double.PositiveInfinity);
        var passive = new UnifiedModel214(geometry, new(geometry, spec, settings, Physics, grip, double.PositiveInfinity,
            true, LoadBasedTyreGrip.GravityMetersPerSecondSquared), backend, double.PositiveInfinity);
        var arms = new Arm214[] { new("C-fixed", false, false), new("D-longitudinal", true, false),
            new("E-vertical", false, true), new("F-combined", true, true) };
        var all = new List<Summary214>();
        var road = new RacingLineGeometry(Frame);
        State214 bench = Seed(To213(standing.State), standing.State.Active, default);
        Save(Path.Combine(output, "protocol.json"), new
        {
            Base = "e77950cf370850afbb4af9c4c56536ea104b57a8", Geometry = geometry, Gravity = LoadBasedTyreGrip.GravityMetersPerSecondSquared,
            Stroke214.MaximumSpeed, Stroke214.MaximumAcceleration, Latency = .05, Release = release, ModelStep = nominalDt,
            NeutralActuator = "rest-to-rest quintic in rider relative coordinates", NoNativeWorlds = true, Arms = arms
        });

        Store(Run("equilibrium", arms[0].Name, passive, bench, .30, nominalDt));
        var ground = bench.Request(new(new(0, 0), new(.16, 0)), .05);
        Store(Run("ground-recoil", arms[1].Name, passive, ground, .8, nominalDt));
        var repeatedCommands = Enumerable.Range(0, 8).Select(i => new Queued214(i + .10, i + .15,
            new(new(0, 0), i == 7 ? default : new Vec214(i % 2 == 0 ? .16 : -.15, i % 2 == 0 ? -.12 : .03))));
        Store(Run("long-work-ledger", arms[3].Name, passive, bench with { Pending = repeatedCommands.ToArray() },
            8.2, nominalDt));
        var free = bench with { Height = 3, Mode = WheelContact213.Air, CrankMomentum = 1.6, WheelMomentum = 10,
            Plane = bench.Plane with { Velocity = new(3, -.5) } };
        free = free with { PitchMomentum = -free.CrankMomentum - free.WheelMomentum };
        var fly = free.Request(new(new(0, 0), new(.16, -.12)), .05);
        var repeated = Run("free-flight", arms[3].Name, dry, fly, 1.2, nominalDt);
        Store(repeated);
        if (!repeated.Trace.SequenceEqual(Run("free-flight", arms[3].Name, dry, fly, 1.2, nominalDt).Trace))
            throw new InvalidOperationException("H33-U repeated in-process trajectory differs.");
        var yawing = fly with { Plane = fly.Plane with { YawMomentum = 20 } };
        Store(Run("free-yaw", arms[3].Name, dry, yawing, 1.2, nominalDt));
        var falling = free with { Height = .85, VerticalMomentum = -80 };
        falling = falling.Request(new(new(0, 0), new(.16, -.12)), .025);
        Store(Run("moving-rider-drop", arms[3].Name, passive, falling, .9, nominalDt));

        var oldLaunch = Follow209("C", standing.Model, standing.State, standing.Goal, _ => new(0, 1), true, 1.12);
        Save(Path.Combine(output, "launch-h28f.json.gz"), oldLaunch, true);
        var frozenLaunch = Read199<Run213>(Path.Combine(root, base213, "launch-full-neutral-5000.json.gz"));
        var oldExit = Read199<Run209>(Path.Combine(root, base213, "corner-exit-211-h28f.json.gz"));
        var oldBend = Read199<Run209>(Path.Combine(root, base213, "bend-5-212-h28f.json.gz"));
        var frozenExit = Read199<Run213>(Path.Combine(root, base213, "corner-exit-211-neutral-5000.json.gz"));
        var frozenBend = Read199<Run213>(Path.Combine(root, base213, "bend-5-212-neutral-5000.json.gz"));
        var law = Law211(SourceFor211(root), Read211(Path.Combine(root, Evidence211, "frozen-candidate.json")));
        var prepareBend = Map199.At(1, 0);
        var prepare = Prefix210(standing, law, prepareBend.Entry - 19);
        var oldPreparation = Follow209("C", prepare.Model, prepare.State, prepare.Goal, law, true, 1.05);
        var preparation = To213(prepare.State);
        Save(Path.Combine(output, "turn-preparation-h28f.json.gz"), oldPreparation, true);
        foreach (var arm in arms)
        {
            var launch = Seed(frozenLaunch.Initial, new(0, 1), default)
                .Request(new(new(0, 1), Target(arm, .17, -.13)), .05);
            Store(Run("full-drive-launch", arm.Name, model, launch, 1.12, nominalDt));
            var exit = MatchedTape(frozenExit.Initial, oldExit.Trace!, arm, .15, -.12);
            Store(Run("corner-exit-211", arm.Name, model, exit, 1.35, nominalDt));
            var early = RecoveryProbe(exit, .35, .65, .85f);
            Store(Run("early-recovery-probe", arm.Name, model, early, 1.35, nominalDt));
            var gas = MatchedTape(frozenBend.Initial, oldBend.Trace!, arm, .16, -.12);
            Store(Run("high-drive-bend-5", arm.Name, model, gas, 1.45, nominalDt));
            var turn = MatchedTape(preparation, oldPreparation.Trace!, arm, .16, -.12);
            Store(Run("turn-preparation", arm.Name, model, turn, 1.05, nominalDt));
        }

        foreach (int visit in new[] { 3, 5 })
        {
            var old = ReadPlain214<Rejected214>(Path.Combine(root, base213, $"bend-{visit}-212-reconstructed.json"));
            var frozen = Read199<Run213>(Path.Combine(root, base213, $"bend-{visit}-212-exact-failure-recorded-{visit}-5000.json.gz"));
            var offset = new Vec214(-.18, old.Fault.RiderHeightOffsetMeters);
            foreach (var arm in arms)
            {
                var current = Seed(frozen.Initial, old.Fault.Action, offset);
                var target = new Vec214(arm.MoveLong ? .16 : offset.X, arm.MoveVertical ? -.10 : offset.Y);
                current = current.Request(new(old.Fault.Action, target), .05);
                Store(Run($"rejected-bend-{visit}", arm.Name, model, current, .40, nominalDt));
            }
        }
        var precise = Seed(frozenLaunch.Initial, new(0, 1), default);
        foreach (double step in new[] { .010, .005, .0025, .00125 })
            Store(Run("timestep-launch", arms[0].Name, model, precise, 1.12, step));

        var numeric = Read199<Run213>(Path.Combine(root, base213, "terminal-certification-211-neutral-5000.json.gz"));
        var terminal = Seed(numeric.Initial, numeric.Trace![0].Active, default);
        Store(Run("terminal-certificate-211", arms[0].Name, model, terminal, .25, nominalDt));
        Save(Path.Combine(output, "summary.json"), all);
        Save(Path.Combine(output, "reference.json"), new
        {
            H28Launch = oldLaunch with { Trace = null }, H32Launch = frozenLaunch with { Trace = null },
            H28Exit = oldExit with { Trace = null }, H32Exit = frozenExit with { Trace = null },
            H28HighDrive = oldBend with { Trace = null }, H32HighDrive = frozenBend with { Trace = null }
        });

        void Store(EvidenceRun214 run)
        {
            var summary = run.Summary;
            all.Add(summary);
            string name = $"{summary.Case}-{summary.Arm}-{Math.Round(summary.Dt * 1e6):F0}";
            Save(Path.Combine(output, name + ".json.gz"), run, true);
            Console.WriteLine($"{name}: {summary.Stop}, t {summary.Time:F4}, front {summary.MinFront:F2} N," +
                $" flight {summary.FrontAir:F4} s, energy {summary.FinalEnergyDefect:G4} J");
        }

        EvidenceRun214 Run(string name, string arm, UnifiedModel214 engine, State214 initial, double horizon, double step)
        {
            var history = new List<Step214>();
            var state = initial;
            double terminalTime = initial.Time + horizon;
            string stop = "horizon";
            while (state.Time < terminalTime - 1e-10 && history.Count < 12000)
            {
                try
                {
                    var row = engine.Step(state, Math.Min(step, terminalTime - state.Time), standing.State.State.Rider.Impairment);
                    if (row.After.Time <= state.Time + 1e-13)
                        throw new InvalidOperationException("H33-U clock did not advance.");
                    history.Add(row);
                    state = row.After;
                    if (Math.Abs(state.Pitch) > 1.0) { stop = "pitch envelope"; break; }
                    if (name.StartsWith("free", StringComparison.Ordinal) || name == "moving-rider-drop") continue;
                    var after = Body(initial, state);
                    if (Math.Abs(after.SlideRadians) > Math.PI / 4) { stop = "beta"; break; }
                    if (Sweep(initial, row) < .50) { stop = "planar sweep proxy"; break; }
                }
                catch (InvalidOperationException e) { stop = e.Message; break; }
            }
            if (history.Count >= 12000) stop = "step budget";
            return Describe(name, arm, step, initial, state, stop, history);
        }

        State214 MatchedTape(State213 initial, Tick209[] tape, Arm214 arm, double forward, double lower)
        {
            var already = tape[0].Before.Active;
            var current = Seed(initial, already, default);
            var move = Target(arm, forward, lower);
            var commands = new List<Queued214> { new(initial.Time, initial.Time + .05, new(already, move)) };
            foreach (var row in tape.Skip(1))
            {
                var active = row.Before.Active;
                if (active == already) continue;
                already = active;
                commands.Add(new(Math.Max(initial.Time, row.Before.State.Time - .05), row.Before.State.Time,
                    new(active, row.Before.State.Time < initial.Time + .05 ? default : move)));
            }
            // Keep longitudinal and vertical commands intact when a frozen steering activation is close to first rider activation.
            for (int i = 0; i < commands.Count; i++)
                if (commands[i].Activates == initial.Time + .05)
                {
                    var latest = commands.Where(c => c.Activates <= commands[i].Activates).OrderBy(c => c.Activates).Last();
                    commands[i] = commands[i] with { Command = new(latest.Command.Bike, move) };
                    break;
                }
            return current with { Pending = commands.OrderBy(c => c.Activates).ToArray() };
        }

        State214 RecoveryProbe(State214 state, double from, double until, float drive)
        {
            double start = state.Time + from, end = state.Time + until;
            var controller = state.Active;
            var events = new List<Queued214>();
            foreach (double at in state.Pending.Select(c => c.Activates).Append(start).Append(end).Distinct().Order())
            {
                foreach (var q in state.Pending.Where(q => q.Activates == at)) controller = q.Command;
                var bike = at >= start && at < end ? controller.Bike with { Drive = Math.Max(drive, controller.Bike.Drive) }
                    : controller.Bike;
                events.Add(new(at - .05, at, controller with { Bike = bike }));
            }
            return state with { Pending = events.ToArray() };
        }

        State214 Seed(State213 frozen, FreeRacingAction action, Vec214 offset)
        {
            var at = geometry.RiderNeutral + offset;
            var rider = frozen.Body;
            var plane = new Plane214(new(rider.Position.X, rider.Position.Y), new(rider.Velocity.X, rider.Velocity.Y),
                rider.Heading, rider.AngularVelocity * geometry.YawInertia(at, 0));
            double pc = frozen.Crank * geometry.CrankInertia, pw = frozen.Wheel * geometry.WheelInertia;
            return new(frozen.Time, plane, geometry.FrameHeight + at.Y * geometry.RiderFraction, 0, 0, -pc - pw,
                pc, pw, frozen.Steer, frozen.Lean, Stroke214.Stationary(frozen.Time, at), new(action, offset),
                [], WheelContact213.Both, rider.TotalReferenceMeters);
        }

        RiderView Body(State214 origin, State214 state)
        {
            var point = new Vector2((float)state.Plane.Position.X, (float)state.Plane.Position.Y);
            var at = Frame.Project(point);
            var original = Frame.Project(new Vector2((float)origin.Plane.Position.X, (float)origin.Plane.Position.Y));
            // Fixture probes cross at most one start line and never span half a lap.
            double total = origin.ReferenceOrigin +
                Frame.UnwrapDelta(original.ReferenceMeters, at.ReferenceMeters);
            return standing.State.State.Rider with
            {
                Position = point, Velocity = new((float)state.Plane.Velocity.X, (float)state.Plane.Velocity.Y),
                Heading = (float)state.Plane.Yaw, AngularVelocity = (float)geometry.YawRate(state), Road = at,
                TotalReferenceMeters = total, Drive = state.Active.Bike.Drive
            };
        }

        double Sweep(State214 original, Step214 row)
        {
            var a = Body(original, row.Before);
            var b = Body(original, row.After);
            return Clearance153.SweepLowerBound(a, b, row.After.Time - row.Before.Time, road.Clearance(a), road.Clearance(b));
        }

        EvidenceRun214 Describe(string name, string arm, double step, State214 initial, State214 final,
            string reason, List<Step214> rows)
        {
            double duration = final.Time - initial.Time;
            double Min(Func<Step214, double> f) => rows.Select(f).DefaultIfEmpty(0).Min();
            double Max(Func<Step214, double> f) => rows.Select(f).DefaultIfEmpty(0).Max();
            double impulse = 0, peak = 0, width = 0;
            var sector = new double[3, 3];
            double lastBend = Map199.At(2, 1).Exit;
            foreach (var row in rows)
            {
                impulse += row.EnergyResidual;
                peak = Math.Max(peak, Math.Abs(impulse));
                width += (row.Forces.FrontYawMaximum - row.Forces.FrontYawMinimum) * Dt(row);
                var after = Body(initial, row.After);
                int n = after.TotalReferenceMeters >= lastBend ? 1 : after.Road.IsTurn ? 2 : 0;
                sector[n, 0] += Dt(row);
                if (row.Before.Active.Bike.Drive >= .9999) sector[n, 1] += Dt(row);
                sector[n, 2] = Math.Max(sector[n, 2], row.After.Plane.Velocity.Norm * 3.6);
            }
            var end = Body(initial, final);
            return new(new(name, arm, step, reason, duration, final.Plane.Velocity.Norm * 3.6, end.TotalReferenceMeters,
                Min(r => r.Forces.Normal.Front), Min(r => r.Forces.Normal.Rear),
                rows.Where(r => !r.Forces.Normal.Mode.HasFlag(WheelContact213.Front)).Sum(Dt),
                rows.FirstOrDefault(r => !r.Forces.Normal.Mode.HasFlag(WheelContact213.Front))?.Before.Time ?? -1,
                rows.LastOrDefault(r => r.Landing?.Impulse.Front > 0)?.After.Time ?? -1,
                Max(r => Math.Abs(r.After.Pitch * 180 / Math.PI)), Max(r => r.Forces.FrontYawMaximum - r.Forces.FrontYawMinimum),
                width / Math.Max(duration, 1e-10), geometry.YawRate(final), Corridor199.CourseError(end) * 180 / Math.PI,
                Max(r => (r.After.Plane.Velocity.Norm - r.Before.Plane.Velocity.Norm) / Dt(r)),
                Max(r => r.Forces.Normal.Front), Max(r => r.Forces.Rear.Force.Dot(Vec214.Axis(r.Before.Plane.Yaw))),
                Max(r => r.Forces.EngineRpm), Min(r => Sweep(initial, r)), Min(r => Sweep(initial, r) -
                    Math.Max(Padding(r.Before), Padding(r.After))), rows.Sum(r => r.RiderWork),
                rows.Sum(r => r.Landing?.RiderWork ?? 0), rows.Sum(r => r.Landing?.Dissipated ?? 0),
                Max(r => Math.Abs(r.EnergyResidual)), impulse, peak, Min(r => Math.Min(r.Front.Gap, r.Rear.Gap)),
                Max(r => Math.Max(r.Forces.Normal.Front == 0 ? r.Forces.Front.Force.Norm : 0,
                    r.Forces.Normal.Rear == 0 ? r.Forces.Rear.Force.Norm : 0)),
                Max(r => Math.Abs(r.NormalWork)), Max(r => Math.Abs(r.PitchImpulseResidual)),
                Max(r => Math.Abs(r.VerticalImpulseResidual)), Max(r => r.RiderForce.Norm),
                Max(r => Math.Max(r.Before.Rider.Velocity.Norm, r.After.Rider.Velocity.Norm)),
                Max(r => Math.Max(r.Before.Rider.Acceleration.Norm, r.After.Rider.Acceleration.Norm)),
                sector[0, 0], sector[0, 1], sector[0, 2], sector[1, 0], sector[1, 1], sector[1, 2],
                sector[2, 0], sector[2, 1], sector[2, 2], rows.Count, rows.Count(r => r.Landing is not null),
                rows.Sum(r => r.Subdivisions)), rows.ToArray());
        }

        double Padding(State214 s) => Math.Abs((s.Rider.Position.Rotate(s.Pitch) - geometry.RiderNeutral).X) *
            geometry.RiderFraction + 1.5 * (1 - Math.Cos(s.Pitch)) + Math.Abs(Math.Sin(s.Pitch));
    }

    private static Vec214 Target(Arm214 arm, double forward, double lower) =>
        new(arm.MoveLong ? forward : 0, arm.MoveVertical ? lower : 0);
    private static double Dt(Step214 row) => row.After.Time - row.Before.Time;
    private static State213 To213(Start197 s) => new(s.State.Time, s.State.Rider, default, s.State.Shaft,
        s.State.Wheel, s.State.Steer, s.State.Lean);
    private static T ReadPlain214<T>(string path) => System.Text.Json.JsonSerializer.Deserialize<T>(File.ReadAllText(path), Json)
        ?? throw new InvalidDataException("Missing " + path);
}
