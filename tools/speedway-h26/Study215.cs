
using System.Numerics;
using CoreSim;
using Safety153;
using SharedWorldSpeedway;

namespace SpeedwayHandling;

internal static partial class Study191
{
    private const double Dt215 = .0025;
    private const double Gravity215 = LoadBasedTyreGrip.GravityMetersPerSecondSquared;
    private sealed record Arm215(string Name, Axes215 Axes, Actuator215 Actuator, bool InheritedSlip = false);
    private sealed record Fixture215(string Name, string Frozen, string Legacy, double Duration);
    private sealed record Sector215(string Name, double Time, double Wot, double FullDriveDeceleration,
        double MeanDrive, double PeakKph);
    private sealed record Probe215(string Case, string Arm, string InitialBank, string Stop, double Dt, double Seconds,
        State215 Initial, State215 Final, int Steps, double Progress, double? Sweep,
        double FinalKph, double PeakKph, double PeakRpm, double MinTorque, double MaxTorque,
        double PeakRearSlip, double MaxUtilization, double PeakAccel, double MinAccel, double PeakLatAccel,
        double MaxRoll, double PeakRollRate, double PeakPitch, double PeakPitchRate, double PeakYawRate, double PeakBeta,
        double MinFront, double MinRear, double FrontFlight, double FrontYawBand, double FrontRollBand,
        double MaxForce, double MaxPower, double ForceAtNinetyPercentSeconds, double MaxRiderSpeed, double MaxRiderAcceleration,
        double? SettledAfterActivationSeconds, double PositiveActuatorWork, double NegativeActuatorWork,
        double NormalImpulseAfterFirstTouchdown, double ActuatorImpulseAfterFirstTouchdown,
        Vec215 NetImpulseResidual, Vec215 AngularImpulseResidual, double FinalEnergyResidual,
        double PeakEnergyResidual, Vec215 FrameDisplacement, double LostEnergy, Sector215[] Sectors, int Subdivisions);
    private sealed record Run215(Probe215 Summary, Step215[] Trace);
    private sealed record Search215(int Id, float SteerBias, float DriveLimit, Vec215 RiderTarget, Probe215 Outcome);

    private static void Roll215(string root, string output, string stage)
    {
        if (stage is not ("mechanics" or "all")) throw new ArgumentException("Use mechanics or all.");
        const string h33 = "data/experiments/speedway214";
        const string h32 = "data/experiments/speedway213";
        var (_, spec) = CourseFixture208(root, "C");
        var standing = RootFor210(root, "C");
        var settings = Settings202("H28-F");
        var road = new RacingLineGeometry(Frame);
        var surface = Surface.SampleSurface(0, 2);
        double release = RiderController.ReactionSeconds(spec);
        var baseGeometry = Geometry214.Fixture(Mass, spec.Profile.Gameplay.Physical.MassKg,
            Physics.TwoContactBike ?? TwoContactBikeParameters.Default, Physics.RearWheel ?? RearWheelParameters.Default, Inertia);
        var fixedArm = new Arm215("C-fixed", Axes215.Fixed, Actuator215.Responsive);
        var sagittal = new Arm215("D-sagittal", Axes215.Sagittal, Actuator215.Responsive);
        var lateral = new Arm215("E-lateral", Axes215.Lateral, Actuator215.Responsive);
        var combined = new Arm215("F-spatial", Axes215.All, Actuator215.Responsive);
        var limited = new Arm215("G-limited", Axes215.All, Actuator215.Limited);
        var scaled = combined with { Name = "F-scaled", InheritedSlip = true };
        var arms = new[] { fixedArm, sagittal, lateral, combined, limited, scaled };
        var neutral = new Geometry215(baseGeometry, Axes215.Fixed).Neutral;
        var timing = new[] { .005, .0025, .00125 };
        Save(Path.Combine(output, "protocol.json"), new
        {
            Base = "7242ce5fc715f1643b6060da35bad5196db6aeab", baseGeometry, Gravity215, Dt215, release,
            Geometry215.RollInertia, UnifiedModel215.Stiffness, UnifiedModel215.Damping,
            Arms = arms, ActuationDelay = .05, NativeWorlds = 0,
            ThinRing = "ground tangent projected from 3D axle; no camber term",
            Support = "unilateral compressible radial tyre; closing damping ramp over first 2 mm",
            Drive = "normalized engine torque map request; no direct butterfly angle or acceleration override",
            Energy = "J; impulses N s, angular impulses N m s; coordinate lengths m",
            SearchGoal = "finish local horizon inside the planar sweep proxy and maximize actual reference progress"
        });

        var benches = new List<Probe215>();
        var ground = Static(fixedArm);
        StoreBench(Run("equilibrium", fixedArm, ground, [], .35));
        var fall = Static(fixedArm, roll: -.033);
        StoreBench(Run("gravity-fall", fixedArm, fall, [], .25));
        var free = new State215(0, new(0, 0, 3), new(40, -8, 0), Attitude215.Pose(.21, .12, -.21),
            default, neutral, default, 1.6, 10, 0, default);
        var angular = Geometry(combined).WithRates(free, new(.43, -.30, .28), new(.18, -.13, .06));
        StoreBench(Run("isolated-angular-momentum", combined, angular, [], .45, gravity: 0));
        var lateralFree = free with { Pose = Attitude215.Pose(0, 0, 0), Crank = 0, Wheel = 0, Momentum = default };
        StoreBench(Run("lateral-recoil", lateral, lateralFree, Move(lateralFree, new(0, .16, 0)), .65, gravity: 0));
        StoreBench(Run("free-flight-moving", combined, angular, Move(angular, new(.13, .15, -.09)), .45, gravity: 0));
        var stationary = Static(lateral);
        StoreBench(Run("stationary-body-shift", lateral, stationary, Move(stationary, new(0, .13, 0)), .44));
        var circle = Static(fixedArm, speed: 8, roll: -.25, steer: .059);
        const double yawRate = .31;
        circle = Geometry(fixedArm).WithRates(circle with { Momentum = new(baseGeometry.Mass * 8, -baseGeometry.Mass * .16, 0) },
            circle.Pose.Body(Vec215.Up * yawRate), default);
        StoreBench(Run("circular-motion", fixedArm, circle, [], .26));
        var front = Static(fixedArm, speed: 10);
        StoreBench(Run("countersteer-left", fixedArm, front, [new(0, .05, new(new(.11f, 0), default))], .21));
        StoreBench(Run("countersteer-right", fixedArm, front, [new(0, .05, new(new(-.11f, 0), default))], .21));
        var recovery = Static(combined, speed: 11, roll: -.086);
        StoreBench(Run("roll-recovery", combined, recovery, [], .70, feedback: true));
        var pitched = Geometry(combined).WithRates(Static(combined, speed: 6, roll: -.15) with
            { Pose = Attitude215.Pose(0, .013, -.15) }, new(.17, -.2, .11), default);
        StoreBench(Run("pitch-roll", combined, pitched, Move(pitched, new(.07, .09, -.05)), .22));
        var launch = Static(fixedArm, roll: -.07) with
            { Command = new(new(0, 1), default), Crank = 900 * Math.PI / 30 * baseGeometry.CrankInertia };
        foreach (double step in timing)
            StoreBench(Run("leaned-launch", fixedArm, launch, [], .82, dt: step, power: true));
        var impact = Static(limited, roll: -.32);
        impact = impact with { Center = impact.Center + Vec215.Up * .30, Momentum = -Vec215.Up * (baseGeometry.Mass * .7) };
        foreach (var arm in new[] { combined, limited, limited with { Name = "G-soft", Actuator = Actuator215.Limited with
                     { MaxForce = 350, MaxPower = 150 } }, limited with { Name = "G-firm", Actuator = Actuator215.Limited with
                     { MaxForce = 1400, MaxPower = 700 } } })
        {
            foreach (double step in arm.Name == "G-limited" ? timing : [Dt215])
                StoreBench(Run("leaned-touchdown", arm, impact, Move(impact, new(.16, .18, -.11)), .40, dt: step));
        }

        var circular = benches.Single(r => r.Case == "circular-motion");
        var drivenCircle = new UnifiedModel215(Geometry(fixedArm), spec, settings, Physics, surface,
            fixedArm.Actuator, release, true, Gravity215);
        var steady = SteadyCircle215(drivenCircle, Geometry(fixedArm), circle, 12, .23, release + 1,
            Engine500Parameters.Default.ShortOverallRatio + (Engine500Parameters.Default.LongOverallRatio -
            Engine500Parameters.Default.ShortOverallRatio) * spec.Setup.Gearing);
        if (steady.Residual < .005)
            StoreBench(Run("steady-driven-circle", fixedArm, steady.State, [], 1.25, power: true));
        var circleG = Geometry(fixedArm);
        var circleM = circleG.Motion(circle);
        var trialF = circleG.Contact(circle, circleM, true, 0);
        var trialR = circleG.Contact(circle, circleM, false, 0);
        // Thin-ring offset changes the exact balance. A point support at the center plane
        // gives the standard analytical limit for the same imposed acceleration.
        double a = 8 * yawRate, expectedRoll = Math.Atan(a / Gravity215);
        double benchmark = Math.Tan(expectedRoll) * Gravity215;
        double wheelbase = trialF.Arm.X - trialR.Arm.X;
        var net = new Vec215(0, baseGeometry.Mass * a, baseGeometry.Mass * Gravity215);
        var contactMoment = trialR.Arm.Cross(net * (trialF.Arm.X / wheelbase)) +
            trialF.Arm.Cross(net * (-trialR.Arm.X / wheelbase));
        Save(Path.Combine(output, "circle.json"), new
        {
            Speed = 8, YawRadPerSecond = yawRate, AnalyticalLateral = a, AnalyticalRollRadians = expectedRoll,
            PointContactLateralFromRoll = benchmark, Steady = steady,
            SteadyPointBank = Math.Atan(12 * .23 / Gravity215),
            ThinRingMomentAtSeed = contactMoment, MeasuredPeakLat = circular.PeakLatAccel,
            FinalRoll = circular.Final.Pose.Roll, End = circular.Stop,
            Classification = "transient dynamic circle seed with separate point-support analytical benchmark"
        });

        var equilibrium = benches.Single(r => r.Case == "equilibrium");
        var isolated = benches.Single(r => r.Case == "isolated-angular-momentum");
        var recoil = benches.Single(r => r.Case == "lateral-recoil");
        var saturated = benches.Single(r => r.Case == "leaned-touchdown" && r.Arm == "G-limited" && r.Dt == Dt215);
        bool mechanicalGate = equilibrium.Final.Momentum.Norm < 1e-6 && equilibrium.MaxRoll < 1e-7 &&
            isolated.NetImpulseResidual.Norm < 1e-8 && isolated.AngularImpulseResidual.Norm < 1e-8 &&
            Math.Abs(recoil.FrameDisplacement.Y) > .02 && saturated.MaxForce <= 700.000001 &&
            saturated.MaxPower is >= 349.999 and <= 350.000001 &&
            benches.Single(r => r.Case == "countersteer-left").Final.Pose.Roll > .06 &&
            benches.Single(r => r.Case == "countersteer-right").Final.Pose.Roll < -.06 &&
            Math.Abs(benches.Single(r => r.Case == "roll-recovery").Final.Pose.Roll) < .025 &&
            benches.All(r => r.Steps > 0 && r.Stop != "coupled midpoint did not converge");
        Save(Path.Combine(output, "mechanics.json"), new { Gate = mechanicalGate, Cases = benches });
        if (stage == "mechanics") return;
        if (!mechanicalGate) throw new InvalidOperationException("H34-R isolated mechanics gate failed before racing probes.");

        var fixtures = new[]
        {
            new Fixture215("launch-straight", "full-drive-launch", $"{h33}/launch-h28f.json.gz", 1.12),
            new Fixture215("corner-entry", "turn-preparation", $"{h33}/turn-preparation-h28f.json.gz", 1.05),
            new Fixture215("mid-bend", "high-drive-bend-5", $"{h32}/bend-5-212-h28f.json.gz", 1.45),
            new Fixture215("exit", "corner-exit-211", $"{h32}/corner-exit-211-h28f.json.gz", 1.35),
            new Fixture215("early-C", "early-recovery-probe", $"{h32}/corner-exit-211-h28f.json.gz", 1.35),
            new Fixture215("early-D", "early-recovery-probe", $"{h32}/corner-exit-211-h28f.json.gz", 1.35),
            new Fixture215("early-E", "early-recovery-probe", $"{h32}/corner-exit-211-h28f.json.gz", 1.35),
            new Fixture215("early-F", "early-recovery-probe", $"{h32}/corner-exit-211-h28f.json.gz", 1.35),
            new Fixture215("low-front-3", "rejected-bend-3", $"{h32}/bend-3-212-h28f-rejected.json.gz", .40),
            new Fixture215("low-front-5", "rejected-bend-5", $"{h32}/bend-5-212-h28f-rejected.json.gz", .40)
        };

        var results = new List<Probe215>();
        var frozenData = new List<object>();
        foreach (var fixture in fixtures)
        {
            var matched = Load(fixture);
            var legacy = Read199<Run209>(Path.Combine(root, fixture.Legacy));
            frozenData.Add(new { Fixture = fixture, H28 = legacy with { Trace = null }, H33 = matched.Summary,
                FrozenClocks = "H34 accepts the active H33 bike-command transition times; shared initial planar velocity, heading and spins" });
            var original = matched.Trace[0].Before;
            var tape = Tape(matched);
            foreach (var arm in arms)
            {
                var target = Stance(fixture.Name);
                var seed = FromFrozen(arm, original, 0);
                StoreRace(Run(fixture.Name, arm, seed, Commands(seed, tape, target), fixture.Duration, power: true,
                    reference: original.ReferenceOrigin));
            }
            if (fixture.Name is "corner-entry" or "mid-bend" or "exit" or "low-front-5")
            {
                var banked = FromFrozen(limited, original, -Math.Clamp(original.Lean, -.48, .48));
                StoreRace(Run(fixture.Name, limited, banked, Commands(banked, tape, Stance(fixture.Name)),
                    fixture.Duration, power: true, reference: original.ReferenceOrigin, bank: "proxy pre-bank"));
            }
        }
        Save(Path.Combine(output, "frozen-AB.json"), frozenData);
        Save(Path.Combine(output, "matched.json"), results);

        var bend = Load(fixtures.Single(f => f.Name == "mid-bend"));
        var source = bend.Trace[0].Before;
        var searchSeed = FromFrozen(limited, source, -Math.Clamp(source.Lean, -.48, .48));
        var searchTape = Tape(bend);
        var search = new List<Search215>();
        var retained = new List<(int Id, Run215 Run)>();
        foreach (float bias in new[] { -.13f, -.065f, 0f, .065f, .13f })
        foreach (float drive in new[] { .67f, .80f, .90f, 1f })
        foreach (var target in new Vec215[] { new(.12, -.12, -.10), new(.12, 0, -.10), new(.12, .12, -.10) })
        {
            var run = Run("joint-search-bend", limited, searchSeed,
                Commands(searchSeed, searchTape, target, bias, drive), 1.45, power: true,
                reference: source.ReferenceOrigin, bank: "proxy pre-bank");
            int id = search.Count + 1;
            search.Add(new(id, bias, drive, target, run.Summary));
            retained.Add((id, run));
        }

        var ranked = retained.OrderByDescending(p => p.Run.Summary.Stop == "horizon")
            .ThenByDescending(p => p.Run.Summary.Progress).ThenBy(p => p.Id);
        foreach (var pair in ranked.Take(3))
            Save(Path.Combine(output, $"search-{pair.Id:00}.json.gz"), pair.Run, true);
        Save(Path.Combine(output, "search.json"), search);
        var feedbackSearch = new List<Search215>();
        var feedbackBest = new List<(int Id, Run215 Run)>();
        foreach (float requestedBank in new[] { -.16f, -.24f, -.32f, -.40f, -.48f })
        foreach (float drive in new[] { .67f, .80f, .90f, 1f })
        foreach (double lateralTarget in new[] { -.12, 0, .12 })
        {
            var target = new Vec215(.06, lateralTarget, -.06);
            var run = Run("feedback-search-bend", limited, searchSeed, [], 1.45, power: true,
                reference: source.ReferenceOrigin, bank: "proxy pre-bank", feedback: true,
                bankDemand: requestedBank, feedbackDrive: drive, riderDemand: target);
            int id = feedbackSearch.Count + 1;
            feedbackSearch.Add(new(id, requestedBank, drive, target, run.Summary));
            feedbackBest.Add((id, run));
        }
        foreach (var pair in feedbackBest.OrderByDescending(p => p.Run.Summary.Stop == "horizon")
                     .ThenByDescending(p => p.Run.Summary.Progress).ThenBy(p => p.Id).Take(3))
            Save(Path.Combine(output, $"feedback-{pair.Id:00}.json.gz"), pair.Run, true);
        Save(Path.Combine(output, "feedback-search.json"), feedbackSearch.Select(p =>
            new { p.Id, RollDemand = p.SteerBias, Drive = p.DriveLimit, p.RiderTarget, p.Outcome }));
        Console.WriteLine($"H34-R recorded {benches.Count} isolated runs, {results.Count} matched runs and " +
            $"{search.Count + feedbackSearch.Count} search proposals.");

        Geometry215 Geometry(Arm215 arm) => new(baseGeometry, arm.Axes);

        State215 Static(Arm215 arm, double speed = 0, double roll = 0, double steer = 0)
        {
            var g = Geometry(arm);
            var s = new State215(0, new(0, 0, .55), new(baseGeometry.Mass * speed, 0, 0),
                Attitude215.Pose(0, 0, roll), default, neutral, default, 0, 0, steer, new(new((float)steer, 0), default));
            return g.Supported(s, Gravity215, UnifiedModel215.Stiffness);
        }

        State215 FromFrozen(Arm215 arm, State214 state, double bank)
        {
            var g = Geometry(arm);
            var pose = Attitude215.Pose(state.Plane.Yaw, state.Pitch, bank);
            var rider = new Vec215(state.Rider.Position.X, 0, state.Rider.Position.Y);
            var s = new State215(state.Time, new(state.Plane.Position.X, state.Plane.Position.Y, state.Height),
                new(state.Plane.Velocity.X * baseGeometry.Mass, state.Plane.Velocity.Y * baseGeometry.Mass, state.VerticalMomentum),
                pose, default, rider, default, state.CrankMomentum, state.WheelMomentum, state.Steer,
                new(state.Active.Bike, rider - neutral));
            // Add equal static tyre sag and preserve the archived pitch, yaw and planar COM.
            var m = g.Motion(s);
            double lowest = Math.Min(g.Contact(s, m, true, 0).Gap, g.Contact(s, m, false, 0).Gap);
            if (state.Mode != WheelContact213.Air)
                s = s with { Center = s.Center with { Z = s.Center.Z - lowest - baseGeometry.Mass * Gravity215 / (2 * UnifiedModel215.Stiffness) } };
            var w = Vec215.Up * baseGeometry.YawRate(state) - pose.Apply(Vec215.Left) * baseGeometry.PitchRate(state);
            return g.WithRates(s, pose.Body(w), default);
        }

        EvidenceRun214 Load(Fixture215 fixture)
        {
            string arm = fixture.Name switch
            {
                "early-D" => "D-longitudinal", "early-E" => "E-vertical", "early-F" => "F-combined", _ => "C-fixed"
            };
            return Read199<EvidenceRun214>(Path.Combine(root, h33, $"{fixture.Frozen}-{arm}-5000.json.gz"));
        }

        static Activation215[] Tape(EvidenceRun214 run) => run.Trace.Select(row => row.Before)
            .Where((row, i) => i == 0 || row.Active.Bike != run.Trace[i - 1].Before.Active.Bike)
            .Select(s => new Activation215(s.Time, s.Time, new(s.Active.Bike, default))).ToArray();

        static Vec215 Stance(string name) => name is "launch-straight" or "exit" ? new(.16, -.06, -.12) :
            new(.13, .12, -.10);

        static Activation215[] Commands(State215 initial, Activation215[] tape, Vec215 target, float bias = 0, float cap = 1)
        {
            var converted = new List<Activation215>();
            var current = initial.Command;
            foreach (double time in tape.Select(a => a.Time).Append(initial.Time + .05).Order().Distinct())
            {
                var last = tape.Last(a => a.Time <= time);
                bool ready = time >= initial.Time + .05 - 1e-10;
                var command = ready
                    ? new Command215(new(last.Command.Bike.SteeringRadians + bias, Math.Min(cap, last.Command.Bike.Drive)), target)
                    : current with { Bike = last.Command.Bike };
                if (command == current) continue;
                converted.Add(new(ready && time <= initial.Time + .05 + 1e-10 ? initial.Time : time, time, command));
                current = command;
            }
            return converted.ToArray();
        }

        static Activation215[] Move(State215 s, Vec215 offset) => [new(s.Time, s.Time + .05, s.Command with { Target = offset })];

        void StoreBench(Run215 run)
        {
            benches.Add(run.Summary);
            double dt = run.Summary.Dt;
            Save(Path.Combine(output, $"{run.Summary.Case}-{run.Summary.Arm}-{Math.Round(dt * 1e6):F0}.json.gz"), run, true);
            Console.WriteLine($"{run.Summary.Case}/{run.Summary.Arm}: {run.Summary.Stop} {run.Summary.Seconds:F3}s, " +
                $"roll {run.Summary.Final.Pose.Roll:F3}, front air {run.Summary.FrontFlight:F3}s, " +
                $"work defect {run.Summary.FinalEnergyResidual:F5} J; steps {run.Summary.Steps}");
        }

        void StoreRace(Run215 run)
        {
            results.Add(run.Summary);
            Save(Path.Combine(output, $"matched-{run.Summary.Case}-{run.Summary.Arm}-{(run.Summary.InitialBank == "upright" ? "u" : "b")}.json.gz"), run, true);
        }

        Run215 Run(string name, Arm215 arm, State215 initial, Activation215[] queue, double horizon,
            double dt = Dt215, bool power = false, double gravity = Gravity215, double? reference = null,
            string bank = "upright", bool feedback = false, double? bankDemand = null, float feedbackDrive = 0,
            Vec215 riderDemand = default)
        {
            var g = Geometry(arm);
            var engine = new UnifiedModel215(g, spec, settings, Physics, surface, arm.Actuator, release, power, gravity, arm.InheritedSlip);
            var state = initial;
            var rows = new List<Step215>();
            double terminal = initial.Time + horizon, minSweep = double.PositiveInfinity;
            double nextFeedback = initial.Time;
            string stop = "horizon";
            while (state.Time < terminal - 2e-10 && rows.Count < 20_000)
            {
                if (feedback && state.Time >= nextFeedback - 1e-10)
                {
                    var motion = g.Motion(state);
                    double roll = state.Pose.Roll, rate = motion.BodyRate.X;
                    float d = (float)Math.Clamp(-1.25 * roll - .23 * rate, -.19, .19);
                    var target = new Vec215(0, Math.Clamp(-.48 * roll - .075 * rate, -.12, .12), 0);
                    if (bankDemand is double desired)
                    {
                        var patch = g.Contact(state with { Steer = 0 }, motion, true, 0);
                        double slipAngle = Math.Atan2(patch.Velocity.Dot(patch.Side),
                            Math.Max(1, patch.Velocity.Dot(patch.Forward)));
                        d = (float)(slipAngle + .04 - 1.05 * (roll - desired) - .19 * rate);
                        target = riderDemand;
                    }
                    // Feedback activation follows an explicit held observation and 50 ms latency.
                    queue = [.. queue, new(state.Time, state.Time + .05, new(new(d, feedbackDrive), target))];
                    nextFeedback += .025;
                }
                try
                {
                    var row = engine.Step(state, queue, Math.Min(dt, terminal - state.Time), standing.State.State.Rider.Impairment);
                    if (row.After.Time <= state.Time) throw new InvalidOperationException("clock stalled");
                    state = row.After;
                    rows.Add(row);
                    if (reference.HasValue)
                    {
                        double path = Sweep(initial, row, reference.Value, g);
                        minSweep = Math.Min(minSweep, path);
                        if (path < .50) { stop = "planar sweep proxy"; break; }
                        if (Math.Abs(Beta(state)) > Math.PI / 4) { stop = "sideslip envelope"; break; }
                    }
                }
                catch (InvalidOperationException error) { stop = error.Message; break; }
            }
            if (rows.Count == 20_000) stop = "step budget";
            return Describe(name, arm, bank, initial, state, stop, rows, g, gravity, dt, minSweep, reference);
        }

        RiderView Body(State215 origin, State215 s, double reference, Geometry215 geometry)
        {
            var p = new Vector2((float)s.Center.X, (float)s.Center.Y);
            var q = new Vector2((float)origin.Center.X, (float)origin.Center.Y);
            var mark = Frame.Project(p);
            var forward = s.Pose.Apply(Vec215.Forward);
            var rate = geometry.Motion(s).WorldRate;
            double yawRate = rate.Cross(forward).Plane.Dot(forward.Plane.Left) / forward.Plane.Dot(forward.Plane);
            return standing.State.State.Rider with
            {
                Position = p, Velocity = new((float)(s.Momentum.X / baseGeometry.Mass), (float)(s.Momentum.Y / baseGeometry.Mass)),
                Heading = (float)s.Pose.Yaw, AngularVelocity = (float)yawRate, Road = mark,
                TotalReferenceMeters = reference + Frame.UnwrapDelta(Frame.Project(q).ReferenceMeters, mark.ReferenceMeters),
                Drive = s.Command.Bike.Drive
            };
        }

        double Sweep(State215 origin, Step215 row, double reference, Geometry215 geometry)
        {
            var a = Body(origin, row.Before, reference, geometry);
            var b = Body(origin, row.After, reference, geometry);
            return Clearance153.SweepLowerBound(a, b, Duration215(row), road.Clearance(a), road.Clearance(b));
        }

        double Kph(State215 s) => s.Momentum.Plane.Norm / baseGeometry.Mass * 3.6;

        double Beta(State215 s) => s.Momentum.Plane.Norm < .50 * baseGeometry.Mass ? 0 :
            CoreSim.PhysicalSpace.BikeAngles.Wrap(s.Pose.Yaw - Math.Atan2(s.Momentum.Y, s.Momentum.X));

        Run215 Describe(string name, Arm215 arm, string bank, State215 initial, State215 final, string stop,
            List<Step215> rows, Geometry215 g, double gravity, double dt, double sweep, double? reference)
        {
            double Min(Func<Step215, double> f) => rows.Select(f).DefaultIfEmpty(0).Min();
            double Max(Func<Step215, double> f) => rows.Select(f).DefaultIfEmpty(0).Max();
            double Sum(Func<Step215, double> f) => rows.Sum(f);
            double sum = 0, peak = 0, duration = final.Time - initial.Time;
            var p = default(Vec215);
            var l = default(Vec215);
            var sector = new double[3, 5];
            double normalImpulse = 0, riderImpulse = 0, firstContact = double.PositiveInfinity;
            double? activation = null, settled = null;
            foreach (var row in rows)
            {
                double seconds = Duration215(row);
                sum += row.EnergyResidual;
                peak = Math.Max(peak, Math.Abs(sum));
                p += (row.Forces.Front.Force + row.Forces.Rear.Force + row.Forces.Drag -
                    Vec215.Up * (baseGeometry.Mass * gravity)) * seconds;
                l += row.RollPitchYawMoment * seconds;
                foreach (var e in row.Contacts)
                    if (e.Touchdown) firstContact = Math.Min(firstContact, row.Before.Time + e.Seconds);
                if (row.Midpoint.Time >= firstContact && row.Midpoint.Time < firstContact + .040)
                {
                    normalImpulse += (row.Forces.Front.Normal + row.Forces.Rear.Normal) * seconds;
                    riderImpulse += row.Forces.Rider.Norm * seconds;
                }
                var posture = g.Free(g.Neutral + row.Before.Command.Target - initial.Rider);
                if (activation is null && posture.Norm > .03) activation = row.Before.Time;
                if (activation.HasValue && !settled.HasValue &&
                    g.Free(g.Neutral + row.After.Command.Target - row.After.Rider).Norm < .10 * posture.Norm)
                    settled = row.After.Time - activation;
                if (reference is not double r) continue;
                var body = Body(initial, row.Midpoint, r, g);
                int i = body.TotalReferenceMeters >= Map199.At(2, 1).Exit ? 2 : body.Road.IsTurn ? 1 : 0;
                sector[i, 0] += seconds;
                sector[i, 1] += row.Before.Command.Bike.Drive >= .9999 ? seconds : 0;
                sector[i, 2] += row.Before.Command.Bike.Drive >= .9999 && Longitudinal(row) < 0 ? seconds : 0;
                sector[i, 3] += row.Before.Command.Bike.Drive * seconds;
                sector[i, 4] = Math.Max(sector[i, 4], Kph(row.After));
            }
            var sectors = new[] { "ordinary straight", "bend", "finishing straight" }.Select((s, i) =>
                new Sector215(s, sector[i, 0], sector[i, 1], sector[i, 2], sector[i, 3] / Math.Max(1e-10, sector[i, 0]), sector[i, 4])).ToArray();
            var d = Math.Max(1e-10, duration);
            var displacement = final.Center - final.Pose.Apply(final.Rider) * g.Base.RiderFraction -
                (initial.Center - initial.Pose.Apply(initial.Rider) * g.Base.RiderFraction);
            return new(new(name, arm.Name, bank, stop, dt, duration, initial, final, rows.Count,
                reference is double rel ? Body(initial, final, rel, g).TotalReferenceMeters - rel : 0,
                double.IsFinite(sweep) ? sweep : null, Kph(final), Max(r => Kph(r.After)),
                Max(r => r.Forces.EngineRpm), Min(r => r.Forces.NetEngineTorque), Max(r => r.Forces.NetEngineTorque),
                Max(r => Math.Abs(r.Forces.RearSlip)), Max(r => Math.Max(r.Forces.Front.Utilization, r.Forces.Rear.Utilization)),
                Max(Longitudinal), Min(Longitudinal), Max(r => Math.Abs(Lateral(r))),
                Max(r => Math.Abs(r.After.Pose.Roll)), Max(r => Math.Abs(g.Motion(r.Midpoint).BodyRate.X)),
                Max(r => Math.Abs(r.After.Pose.Pitch)), Max(r => Math.Abs(g.Motion(r.Midpoint).BodyRate.Y)),
                Max(r => Math.Abs(g.Motion(r.Midpoint).WorldRate.Z)), Max(r => Math.Abs(Beta(r.After))),
                Min(r => r.Forces.Front.Normal), Min(r => r.Forces.Rear.Normal),
                Sum(r => r.Forces.Front.Normal == 0 ? Duration215(r) : 0),
                Sum(r => r.Forces.FrontYawBand * Duration215(r)) / d, Sum(r => r.Forces.FrontRollBand * Duration215(r)) / d,
                Max(r => r.Forces.Rider.Norm), Max(r => Math.Abs(r.ActuatorPower)),
                Sum(r => r.Forces.Rider.Norm >= .90 * arm.Actuator.MaxForce ? Duration215(r) : 0),
                Max(r => g.Motion(r.Midpoint).RiderRate.Norm), Max(r => r.RiderAcceleration.Norm),
                settled, Sum(r => Math.Max(0, r.ActuatorPower) * Duration215(r)),
                Sum(r => Math.Min(0, r.ActuatorPower) * Duration215(r)), normalImpulse, riderImpulse,
                final.Momentum - initial.Momentum - p, final.Angular - initial.Angular - l, sum, peak, displacement,
                Sum(r => r.Forces.Loss * Duration215(r)), sectors, rows.Sum(r => r.Subdivisions)), rows.ToArray());

            double Longitudinal(Step215 r) => (Kph(r.After) - Kph(r.Before)) / (3.6 * Duration215(r));

            double Lateral(Step215 r)
            {
                var v = r.Midpoint.Momentum.Plane;
                return v.Norm > .1 ? v.Cross((r.After.Momentum - r.Before.Momentum).Plane /
                    (baseGeometry.Mass * Duration215(r))) / v.Norm : 0;
            }
        }
    }

    private static double Duration215(Step215 step) => step.After.Time - step.Before.Time;
}
