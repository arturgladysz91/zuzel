using CoreSim;
using SharedWorldSpeedway;

namespace SpeedwayHandling;

internal static partial class Study191
{
    private sealed record Stance213(string Name, double ForeAft, double Lower);
    private sealed record Run213(string Name, string Stance, double Dt, State213 Initial, State213 Final, string Stop,
        double MinGap, double MinFront, double MinRear, double PeakPitchDeg, double PeakAbsPitchRate,
        double FirstLift, double FrontAirSeconds, double LastTouchdown, double MinSweep, int Impacts,
        double MaxEnergyResidual, double EnergyResidualSum, double ImpactLoss, double MaxVerticalImpulseResidual,
        double MaxPitchImpulseResidual, double MaxAirForce, double MaxNormalWork, double MaxImpactError, int Steps,
        Step213[]? Trace);
    private sealed record Pair213(string Name, Root210 Origin, Run209 Reference, Tick209[] Tape);
    private sealed record Proposal213(int Id, double Drive, double Duration, float SteerDelta, Run213 Run);
    private sealed record Root213(ContactFailure203 Failed);

    private static readonly Stance213[] Stances213 =
        [new("neutral", 0, 0), new("forward", .18, 0), new("rearward", -.18, 0), new("lowered", 0, .14)];

    private static void Pitch213(string root, string output)
    {
        const double dt = .005;
        string reference211 = Path.Combine(root, Evidence211, "frozen-candidate.json");
        var baseline = Law211(SourceFor211(root), Read211(reference211));
        var standing = RootFor210(root, "C");
        var (_, spec) = CourseFixture208(root, "C");
        var settings = Settings202("H28-F");
        var steering = Physics.TwoContactBike ?? TwoContactBikeParameters.Default;
        var wheel = Physics.RearWheel ?? RearWheelParameters.Default;
        var grip = Surface.SampleSurface(0, 2);
        var riderMass = spec.Profile.Gameplay.Physical.MassKg;
        double release = RiderController.ReactionSeconds(spec);
        var bikes = Stances213.ToDictionary(s => s.Name, s =>
            PitchBike213.FixedRider(Mass, riderMass, steering, wheel.WheelRadiusMeters, s.ForeAft, s.Lower));
        var models = bikes.ToDictionary(e => e.Key, e => new PitchModel213(e.Value, Frame, spec, grip, settings, Physics, Inertia, release));
        var pure = new PitchThreshold213(bikes);
        Save(Path.Combine(output, "protocol.json"), new
        {
            Variant = "H32-P MODEL", Candidate211 = Hash210(reference211), RiderKg = riderMass, Wheel = wheel,
            Steering = steering, YawInertia = Inertia, ReleaseSeconds = release, Grip = grip,
            Geometry = bikes, Thresholds = pure.Analytical(), NativeWorldAttempts = Array.Empty<object>()
        });

        var benchmark = new List<Run213>();
        // All dynamic launch trajectories start at the original fixture's stationary gate.
        foreach (var stance in Stances213)
        {
            var initial = Start(standing.State, stance.Name);
            foreach (float drive in new[] { .45f, 1f })
            {
                string name = drive == 1 ? "launch-full" : "launch-partial";
                var run = Execute(name, stance.Name, initial, (_, _) => new(0, drive), 2.2, dt);
                Store(run);
                Console.WriteLine($"{name} {stance.Name}: {run.Stop}; front {run.MinFront:F1} N; air {run.FrontAirSeconds:F3} s");
            }
            var landing = Execute("launch-lift-reduce", stance.Name, initial,
                (_, lift) => new(0, lift < .13 - 1e-10 ? 1 : .20f), 2.1, dt, .13);
            Store(landing);
        }

        var frozen = Follow209("C", standing.Model, standing.State, standing.Goal, baseline, true);
        var oldReceipt = System.Text.Json.JsonSerializer.Deserialize<Preflight211>(
            File.ReadAllText(Path.Combine(root, Evidence211, "official", "admission.json")), Json)!;
        var registered = oldReceipt.Registered.Single(r => r.Fixture == "C");
        if (frozen.Finish != registered.Finish || frozen.Sweep != registered.Sweep || frozen.Steps != registered.Steps ||
            frozen.LastLegalS != registered.LastLegalS)
            throw new InvalidOperationException("Disabled variant parity failed.");
        Save(Path.Combine(output, "disabled.json"), new { frozen.Finish, frozen.Sweep, frozen.LastLegalS,
            frozen.Steps, Match = true, H28FContactSource = Hash210(Path.Combine(root, "tools/speedway-handling/HandlingContact64.cs")) });

        var pairs = new List<Pair213>();
        var corner = Map199.At(1, 1);
        var exit = Prefix210(standing, baseline, corner.Exit - 16);
        var exited = Follow209("C", exit.Model, exit.State, exit.Goal, baseline, true, 1.35);
        pairs.Add(new("corner-exit-211", exit, exited, exited.Trace!));
        foreach (int visit in new[] { 2, 4 })
        {
            var bend = Map199.At(visit / 2, visit % 2);
            var at = Prefix210(standing, baseline, bend.Entry + 16);
            FreeRacingAction Gas(Start197 start) => baseline(start) with { Drive = 1 };
            var stock = Follow209("C", at.Model, at.State, at.Goal, Gas, true, 1.45);
            var rearward = Follow209("C", at.Model, at.State, at.Goal, Gas, true, 1.45,
                posture: _ => new PostureCommand212(1, -.18));
            string name = $"bend-{visit + 1}-212";
            pairs.Add(new(name, at, stock, stock.Trace!));
            Save(Path.Combine(output, $"{name}-h28f-rejected.json.gz"), rearward, true);
            if (rearward.Contact is not {} fault) continue;
            Save(Path.Combine(output, $"{name}-reconstructed.json"), Reconstruct213(rearward, spec, settings, grip));
            string recorded = $"recorded-{visit + 1}";
            var bike = PitchBike213.FixedRider(Mass, riderMass, steering, wheel.WheelRadiusMeters, -.18,
                Math.Clamp(-fault.RiderHeightOffsetMeters, 0, .14));
            bikes.Add(recorded, bike);
            models.Add(recorded, new(bike, Frame, spec, grip, settings, Physics, Inertia, release));
            var exact = new Start197(fault.State, fault.Action, [], fault.State.Time);
            Store(Execute($"{name}-exact-failure", recorded, Start(exact, recorded), (_, _) => fault.Action, .25, dt));
        }

        var numeric = Read199<Root213>(Path.Combine(root, Evidence211, "root", "root-audit.json.gz")).Failed;
        var numericState = new Start197(numeric.State, numeric.Action, [], numeric.State.Time);
        var isolated = Execute("terminal-certification-211", "neutral", Start(numericState, "neutral"),
            (_, _) => numeric.Action, .25, dt);
        Store(isolated);
        Save(Path.Combine(output, "terminal-certification-211.json"), new
        {
            FrozenFailure = numeric, History = "Study #211 independent enclosure retained one positive-load root.",
            LocalH32 = Summary(isolated)
        });

        foreach (var pair in pairs)
        {
            Save(Path.Combine(output, $"{pair.Name}-h28f.json.gz"), pair.Reference, true);
            foreach (var stance in Stances213)
            {
                var run = Execute(pair.Name, stance.Name, Start(pair.Origin.State, stance.Name),
                    (state, _) => At(pair.Tape, state.Time), pair.Name == "corner-exit-211" ? 1.35 : 1.45, dt);
                Store(run);
                Console.WriteLine($"{pair.Name} {stance.Name}: {run.Stop}; front {run.MinFront:F1} N; air {run.FrontAirSeconds:F3} s");
            }
        }

        var selected = Stances213[2].Name;
        var start = Start(standing.State, selected);
        FreeRacingAction LandingLaw(State213 _, double lift) => new(0, lift < .13 - 1e-10 ? 1 : .20f);
        var convergence = new[] { .010, .005, .0025, .00125 }.Select(step =>
            Execute("convergence", selected, start, LandingLaw, 2.1, step, .13)).ToArray();
        var replay = Execute("convergence", selected, start, LandingLaw, 2.1, .0025, .13);
        if (!convergence[2].Trace!.SequenceEqual(replay.Trace!))
            throw new InvalidOperationException("H32-P same-process replay differs.");
        Save(Path.Combine(output, "convergence.json"), new { Deterministic = true, Runs = convergence.Select(Summary) });
        foreach (var row in convergence) Store(row);

        var target = pairs[0];
        var trials = (from gas in new[] { .25, .40, .55, .70, .85, 1 }
                      from dwell in new[] { .15, .24, .33, .42, .51 }
                      from trim in new[] { -.006f, .006f }
                      select (gas, dwell, trim)).ToArray();
        var results = new Proposal213[trials.Length];
        Parallel.For(0, results.Length, new ParallelOptions { MaxDegreeOfParallelism = 4 }, i =>
        {
            var p = trials[i];
            var s = Start(target.Origin.State, "neutral");
            var run = Execute("corner-envelope", "neutral", s, (state, _) =>
            {
                var q = At(target.Tape, state.Time);
                return state.Time - s.Time < p.dwell ? new FreeRacingAction(q.SteeringRadians + p.trim, (float)p.gas) : q;
            }, 1.35, dt);
            results[i] = new(i, p.gas, p.dwell, p.trim, Summary(run));
        });
        var complete = results.Where(x => x.Run.Stop == "horizon" && x.Run.MinSweep >= .50)
            .OrderByDescending(x => x.Run.Final.Body.TotalReferenceMeters).FirstOrDefault();
        Save(Path.Combine(output, "corner-envelope.json"), new { Winner = complete, Trials = results });
        Save(Path.Combine(output, "mechanical-summary.json"), benchmark);

        State213 Start(Start197 initial, string stance) => new(initial.State.Time,
            initial.State.Rider, bikes[stance].Rest, initial.State.Shaft, initial.State.Wheel,
            initial.State.Steer, initial.State.Lean);

        void Store(Run213 row)
        {
            benchmark.Add(Summary(row));
            string name = $"{row.Name}-{row.Stance}-{(int)Math.Round(row.Dt * 1e6)}";
            Save(Path.Combine(output, $"{name}.json.gz"), row, true);
        }

        Run213 Execute(string name, string stance, State213 initial, Func<State213, double, FreeRacingAction> law,
            double horizon, double step, double? airborneCut = null)
        {
            var model = models[stance];
            var trace = new List<Step213>();
            var state = initial;
            double air = 0;
            string stop = "horizon";
            while (state.Time < initial.Time + horizon - 1e-9)
            {
                try
                {
                    double interval = Math.Min(step, initial.Time + horizon - state.Time);
                    if (airborneCut is double cut && air > 0 && air < cut - 1e-10)
                        interval = Math.Min(interval, cut - air);
                    var command = law(state, air);
                    var row = Advance(interval);
                    trace.Add(row);
                    if (!row.Forces.Support.HasFlag(WheelContact213.Front) &&
                        (row.FrontGap > PitchDynamics213.GapToleranceMeters || row.Impact?.Impulse.Front > 0))
                        air += row.After.Time - state.Time;
                    state = row.After;
                    if (Math.Abs(state.Pitch.Angle) > Math.PI / 3) { stop = "pitch-model domain"; break; }
                    if (Math.Abs(state.Body.SlideRadians) > Math.PI / 4) { stop = "beta"; break; }
                    if (row.PlanarSweep < .50) { stop = "planar swept proxy"; break; }

                    Step213 Advance(double duration)
                    {
                        try { return model.Advance(state, command, duration); }
                        catch (PitchConvergence213) when (duration > step / 16) { return Advance(duration / 2); }
                    }
                }
                catch (InvalidOperationException error) { stop = error.Message; break; }
            }
            return Summarize213(name, stance, step, initial, state, stop, trace, air);
        }
    }

    private static FreeRacingAction At(Tick209[] tape, double time)
    {
        int a = 0, b = tape.Length;
        while (b - a > 1)
        {
            int middle = (a + b) / 2;
            if (tape[middle].Before.State.Time <= time + 1e-9) a = middle;
            else b = middle;
        }
        return tape[a].Before.Active;
    }

    private static Run213 Summary(Run213 run) => run with { Trace = null };

    private static Run213 Summarize213(string name, string stance, double dt, State213 initial, State213 final,
        string stop, List<Step213> rows, double air)
    {
        var lift = rows.FirstOrDefault(x => x.FrontGap > PitchDynamics213.GapToleranceMeters);
        var last = rows.LastOrDefault(x => x.Impact?.Impulse.Front > 0);
        double Min(Func<Step213, double> f) => rows.Select(f).DefaultIfEmpty(0).Min();
        double Max(Func<Step213, double> f) => rows.Select(f).DefaultIfEmpty(0).Max();
        return new(name, stance, dt, initial, final, stop, Min(r => Math.Min(r.FrontGap, r.RearGap)),
            Min(r => r.Forces.Loads.Normal.Front), Min(r => r.Forces.Loads.Normal.Rear),
            Max(r => r.After.Pitch.Angle * 180 / Math.PI), Max(r => Math.Abs(r.After.Pitch.Rate)),
            lift?.Before.Time ?? -1, air, last?.After.Time ?? -1, Min(r => r.PlanarSweep), rows.Count(r => r.Impact is not null),
            Max(r => Math.Abs(r.EnergyResidual)), rows.Sum(r => r.EnergyResidual),
            rows.Sum(r => r.Impact?.EnergyLostJoules ?? 0), Max(r => Math.Abs(r.VerticalImpulseResidual)),
            Max(r => Math.Abs(r.PitchImpulseResidual)), Max(r => Math.Max(
                r.Forces.Loads.Normal.Front == 0 ? r.Forces.Front.Length() : 0,
                r.Forces.Loads.Normal.Rear == 0 ? r.Forces.Rear.Length() : 0)),
            Max(r => Math.Abs(r.Forces.NormalWork)), Max(r => Math.Abs(r.Impact?.WorkIdentityError ?? 0)),
            rows.Count, rows.ToArray());
    }

    private sealed class PitchThreshold213(Dictionary<string, PitchBike213> geometry)
    {
        internal object Analytical() => geometry.ToDictionary(e => e.Key, e => new
        {
            Rest = PitchDynamics213.Loads(e.Value, e.Value.Rest, 0),
            SteadyLiftAx = -PitchDynamics213.Gravity * e.Value.Rear.X / e.Value.Height
        });
    }

    private static object Reconstruct213(Run209 failed, RiderSpec spec, HandlingSettings settings, TrackSurfaceState grip)
    {
        var fault = failed.Contact!;
        var before = failed.Trace![^1].Before;
        double previous = before.RiderForeAftMeters;
        double current = Math.Clamp(-.18, previous - .50 * failed.Trace[^1].Power.Seconds,
            previous + .50 * failed.Trace[^1].Power.Seconds);
        double next = Math.Clamp(-.18, current - .50 * fault.Dt, current + .50 * fault.Dt);
        double travel = (current + next) / 2;
        var bike = HandlingContact.AxlesAtForeAft(Physics.TwoContactBike ?? TwoContactBikeParameters.Default,
            spec.Profile.Gameplay.Physical.MassKg / Mass, travel);
        double height = settings.PostureH27!.Prepare(Mass, spec.Profile.Gameplay.Physical.MassKg, settings.H26!.ComHeightMeters,
            fault.RiderHeightOffsetMeters, 1, fault.Dt).CombinedMidpointHeightMeters;
        var query = new HandlingContact.ContactQuery64(settings, fault.State.Rider with { Drive = fault.Action.Drive }, spec,
            bike, LoadBasedTyreGrip.Budget(Mass, grip, bike, Physics.LoadBasedTyres), Mass, Inertia, height,
            fault.State.Time, fault.Dt, fault.State.Steer, fault.Action.SteeringRadians, fault.State.Lean,
            Speed148.ForecastFork148.Wheel(fault.State.Wheel, Physics.RearWheel),
            new CrankClutch144(omega: fault.State.Shaft, lossH26: settings.H26.EngineLoss),
            RiderController.ReactionSeconds(spec));
        double lo = -PitchDynamics213.Gravity * bike.FrontAxleFromComMeters / height + .001;
        double hi = PitchDynamics213.Gravity * bike.RearAxleFromComMeters / height - .001;
        var points = Enumerable.Range(0, 4097).Select(i =>
        {
            var point = query.Evaluate(lo + (hi - lo) * i / 4096);
            return new { point.Ax, point.Residual };
        }).ToArray();
        string status;
        try { query.Solve(lo, hi, null); status = "accepted"; }
        catch (ContactRootException faultStatus) { status = faultStatus.Status.ToString(); }
        return new { fault, LastFrontNormal = failed.Trace[^1].Forces.TyreLoads!.FrontNormalNewtons, Height = height,
            Travel = travel, Status = status, Low = points[0], High = points[^1],
            Minimum = points.Min(x => x.Residual), Maximum = points.Max(x => x.Residual),
            FrozenRejection = fault.Error, Interpretation = "No positive quasi-static contact equilibrium; not a stability result." };
    }
}
