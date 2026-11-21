using System.Diagnostics;
using System.Numerics;
using System.Text.Json;
using CoreSim;
using SharedWorldSpeedway;

namespace SpeedwayHandling;

internal static partial class Study191
{
    private static readonly string[] Archives203 =
    [
        "H25-same", "H26-L-same", "H26-E-same", "H26-LE-same",
        "H26-L-learned-C", "H26-E-learned-C", "H26-LE-learned-C", "H26-LE-learned-C-alt"
    ];

    private sealed record Probe203(HandlingSettings Settings, RiderSpec Spec, HandlingForecast State,
        FreeRacingAction Action, float Dt)
    {
        internal TrackSurfaceState Grip
        {
            get
            {
                var road = State.Rider.Road;
                var width = LaneModel.UsableRacingWidthMeters(Frame.Track.Segments[road.Segment].Type, Frame.Track.Geometry);
                return Surface.SampleSurface(road.Segment, Math.Clamp((float)(road.OffsetMeters / width) * 4, 0, 4));
            }
        }

        internal (HandlingStep Step, ClutchStep144 Power) Evaluate(
            LoadBasedTyreBudget? loads = null, bool combined = false, WorldParameters? physics = null)
        {
            physics ??= Physics;
            var body = State.Rider with { Drive = Action.Drive };
            var shaft = new CrankClutch144(omega: State.Shaft, lossH26: Settings.H26?.EngineLoss);
            var wheel = Speed148.ForecastFork148.Wheel(State.Wheel, physics.RearWheel);
            double release = RiderController.ReactionSeconds(Spec);
            var step = loads is null
                ? HandlingContact.Prepare(Settings, Grip, body, Spec, physics, State.Time, Mass, Inertia,
                    wheel, State.Steer, State.Lean, Dt, Action.SteeringRadians, shaft, release)
                : HandlingContact.AtLoads(Settings, Grip, body, Spec, physics, State.Time, Mass, Inertia,
                    wheel, State.Steer, State.Lean, Dt, Action.SteeringRadians, shaft, release, loads, combined);
            return (step, shaft.Prepared);
        }

        internal LoadBasedTyreBudget Split(double fraction)
        {
            var budget = LoadBasedTyreGrip.Budget(Mass, Grip, TwoContactBikeParameters.Default, Physics.LoadBasedTyres);
            double normal = budget.FrontNormalNewtons + budget.RearNormalNewtons;
            return budget with { FrontNormalNewtons = normal * fraction, RearNormalNewtons = normal * (1 - fraction) };
        }

        internal (HandlingStep Step, ClutchStep144 Power) AtAcceleration(double ax)
        {
            var bike = Physics.TwoContactBike ?? TwoContactBikeParameters.Default;
            var original = LoadBasedTyreGrip.Budget(Mass, Grip, bike, Physics.LoadBasedTyres);
            double normal = Mass * LoadBasedTyreGrip.GravityMetersPerSecondSquared;
            var split = DynamicAxleLoad.AtAcceleration(ax, bike, Settings.H26!.ComHeightMeters);
            double rear = normal * split.RearFraction;
            return Evaluate(original with { FrontNormalNewtons = normal - rear, RearNormalNewtons = rear });
        }
    }

    private static Probe203 Accepted203(Archive198 archive, int i)
    {
        var run = archive.Run.Riders.Single();
        var before = run.Samples[i - 1];
        var after = run.Samples[i];
        return new(archive.Handling, run.Spec, Sample199(before, archive.Lean[0][i - 1]).State,
            after.Input.Active, (float)after.Powertrain!.Seconds);
    }

    private static object Authority203(Probe203 p, HandlingStep step, ClutchStep144 power)
    {
        var f = step.Forces;
        var loads = f.TyreLoads!;
        var bike = Physics.TwoContactBike ?? TwoContactBikeParameters.Default;
        var body = p.State.Rider;
        double leanFraction = Math.Abs((p.State.Lean + step.LeanAfter) / 2) / p.Settings.MaximumLeanRadians;
        var before = f.SteeringBefore;
        var left = f.BodyLeft;
        var frontMin = double.PositiveInfinity;
        var frontMax = double.NegativeInfinity;
        var frontFxMin = double.PositiveInfinity;
        var frontFxMax = double.NegativeInfinity;
        var frontFyMin = double.PositiveInfinity;
        var frontFyMax = double.NegativeInfinity;
        double reach = bike.SteeringRateRadiansPerSecond * p.Dt;
        double lo = Math.Max(-bike.MaximumSteeringRadians, before - reach);
        double hi = Math.Min(bike.MaximumSteeringRadians, before + reach);
        for (int i = 0; i <= 32; i++)
        {
            float angle = body.Heading + (float)((before + lo + (hi - lo) * i / 32) / 2);
            var forward = new Vector2(MathF.Cos(angle), MathF.Sin(angle));
            var normal = new Vector2(-forward.Y, forward.X);
            double scale = HandlingContact.Scale(Vector2.Dot(f.FrontVelocity, forward), .115 * (1 - .13 * leanFraction), f.Budget / Mass);
            var force = normal * RearWheelDynamics.FrontLateralForce(Vector2.Dot(f.FrontVelocity, normal),
                loads.FrontCapacityNewtons, scale * scale);
            double torque = TwoContactForces.Cross(f.FrontArm, force);
            frontMin = Math.Min(frontMin, torque);
            frontMax = Math.Max(frontMax, torque);
            double fx = Vector2.Dot(force, f.BodyForward);
            double fy = Vector2.Dot(force, left);
            frontFxMin = Math.Min(frontFxMin, fx);
            frontFxMax = Math.Max(frontFxMax, fx);
            frontFyMin = Math.Min(frontFyMin, fy);
            frontFyMax = Math.Max(frontFyMax, fy);
        }
        double yawRequest = body.Speed > 0 ? f.Demand / body.Speed : 0;
        double torqueRequest = Inertia * (yawRequest - body.AngularVelocity) / p.Settings.YawResponseSeconds;
        double target = (torqueRequest - f.RearTorque) / bike.FrontAxleFromComMeters;
        double min = frontMin + f.RearTorque, max = frontMax + f.RearTorque;
        return new
        {
            p.State.Time, body.TotalReferenceMeters, body.Speed, Beta = body.SlideRadians, Yaw = body.AngularVelocity,
            p.Action.Drive, SteeringCommand = p.Action.SteeringRadians, SteeringTarget = f.SteeringRequest,
            f.SteeringBefore, f.SteeringUsed, f.SteeringAfter, YawRequest = yawRequest,
            TorqueRequest = torqueRequest, YawTorque = f.NetTorque, f.FrontTorque, f.RearTorque,
            YawMin = min, YawMax = max, YawMiss = Math.Max(0, Math.Max(min - torqueRequest, torqueRequest - max)),
            ForceTargetOutside98Percent = Math.Abs(target) > .98 * loads.FrontCapacityNewtons,
            ForceTargetOutsideCapacity = Math.Abs(target) > loads.FrontCapacityNewtons,
            FrontFyTarget = target, FrontFy = f.FrontLateral, f.RearLateral, f.RearLongitudinal,
            FrontFx = Vector2.Dot(f.FrontForce, f.BodyForward),
            FrontFxMin = frontFxMin, FrontFxMax = frontFxMax, FrontFyMin = frontFyMin, FrontFyMax = frontFyMax,
            RearFxLimitAtFy = Math.Sqrt(Math.Max(0, Math.Pow(loads.RearCapacityNewtons, 2) - Math.Pow(f.RearLateral, 2))),
            RearFyLimitAtFx = Math.Sqrt(Math.Max(0, Math.Pow(loads.RearCapacityNewtons, 2) - Math.Pow(f.RearLongitudinal, 2))),
            loads.FrontNormalNewtons, loads.RearNormalNewtons, loads.EffectiveFrictionCoefficient,
            Ax = Vector2.Dot(f.NetForce, f.BodyForward) / Mass, ResistanceX = Vector2.Dot(f.Resistance, f.BodyForward),
            power.Engine.EngineRpm, power.ClutchTorque, power.Wheel.InstantSlipRatio, power.Wheel.MidSlipVelocityMetersPerSecond,
            power.Wheel.DriveTorqueNewtonMeters, power.Wheel.LongitudinalLossJoules,
            RearLateralLossJoules = -f.RearLateral * Vector2.Dot(f.RearVelocity, left) * p.Dt,
            power.EnergyResidualJoules, p.Dt
        };
    }

    private static object Residual203(Probe203 p, int subdivisions)
    {
        var bike = Physics.TwoContactBike ?? TwoContactBikeParameters.Default;
        double front = bike.FrontAxleFromComMeters, rear = bike.RearAxleFromComMeters;
        double h = p.Settings.H26!.ComHeightMeters;
        double lo = -9.81 * front / h + .001, hi = 9.81 * rear / h - .001;
        var rows = new List<object>();
        double minimum = double.PositiveInfinity, maximum = double.NegativeInfinity, minSlope = double.PositiveInfinity;
        double? previous = null;
        int crossings = 0;
        double EvaluatedAx(double ax)
        {
            var (step, _) = p.AtAcceleration(ax);
            return Vector2.Dot(step.Forces.NetForce, step.Forces.BodyForward) / Mass;
        }
        for (int i = 0; i <= subdivisions; i++)
        {
            double ax = lo + (hi - lo) * i / subdivisions;
            var (step, power) = p.AtAcceleration(ax);
            double actual = Vector2.Dot(step.Forces.NetForce, step.Forces.BodyForward) / Mass;
            double residual = ax - actual;
            if (previous is { } last)
            {
                minSlope = Math.Min(minSlope, (residual - last) / ((hi - lo) / subdivisions));
                if (Math.Sign(last) != Math.Sign(residual)) crossings++;
            }
            minimum = Math.Min(minimum, residual);
            maximum = Math.Max(maximum, residual);
            previous = residual;
            if (i == subdivisions || i % Math.Max(1, subdivisions / 128) == 0)
                rows.Add(new { TrialAx = ax, EvaluatedAx = actual, Residual = residual, Authority = Authority203(p, step, power) });
        }
        double physicalHigh = hi + .001 - 2e-6;
        double? bisectionAx = null, bisectionResidual = null;
        object? bisectionBracket = null;
        if (lo - EvaluatedAx(lo) < 0 && hi - EvaluatedAx(hi) > 0)
        {
            double left = lo, right = hi;
            for (int i = 0; i < 60; i++)
            {
                double mid = (left + right) / 2;
                double residual = mid - EvaluatedAx(mid);
                if (bisectionResidual is null || Math.Abs(residual) < Math.Abs(bisectionResidual.Value))
                {
                    bisectionAx = mid;
                    bisectionResidual = residual;
                }
                if (residual < 0) left = mid;
                else right = mid;
            }
            object Boundary(double ax)
            {
                var (step, power) = p.AtAcceleration(ax);
                return new { Ax = ax, Residual = ax - Vector2.Dot(step.Forces.NetForce, step.Forces.BodyForward) / Mass,
                    Data = Authority203(p, step, power) };
            }
            bisectionBracket = new { Lower = Boundary(left), Upper = Boundary(right) };
        }
        return new { Lo = lo, Hi = hi, MinResidual = minimum, MaxResidual = maximum, MinSampledSlope = minSlope,
            Crossings = crossings, BisectionAx = bisectionAx, BisectionResidual = bisectionResidual,
            BisectionBracket = bisectionBracket, NearZeroFrontAx = physicalHigh,
            NearZeroFrontResidual = physicalHigh - EvaluatedAx(physicalHigh), Samples = rows };
    }

    private static void Audit203(string repository, string output)
    {
        string input = Path.Combine(repository, "data/experiments/speedway202");
        var ledgers = new List<object>();
        var examples = new Dictionary<string, Probe203>();
        var clock = Stopwatch.StartNew();
        foreach (string name in Archives203)
        {
            var archive = Read199<Archive198>(Path.Combine(input, name + ".json.gz"));
            var run = archive.Run.Riders.Single();
            var rows = new List<object>();
            int minIndex = 1, maxBeta = 1;
            double lowest = double.PositiveInfinity;
            double duration = 0;
            double[] under = new double[4];
            double[] thresholds = [.01, .05, .10, .20];
            int exact = 0;
            for (int i = 1; i < run.Samples.Length; i++)
            {
                var p = Accepted203(archive, i);
                var (step, power) = p.Evaluate();
                var after = run.Samples[i];
                var f = step.Forces;
                if (f.FrontForce == after.Forces!.Value.FrontForce && f.RearForce == after.Forces.Value.RearForce &&
                    f.SteeringAfter == after.Forces.Value.SteeringAfter && power.AfterOmega == after.CrankOmega &&
                    step.LeanAfter == archive.Lean[0][i].ActualRadians)
                    exact++;
                double dt = after.Powertrain!.Seconds;
                duration += dt;
                var z = f.TyreLoads!;
                if (z.FrontNormalNewtons < lowest) { lowest = z.FrontNormalNewtons; minIndex = i; }
                if (Math.Abs(p.State.Rider.SlideRadians) > Math.Abs(run.Samples[maxBeta - 1].Input.Rider.SlideRadians))
                    maxBeta = i;
                for (int j = 0; j < thresholds.Length; j++)
                    if (z.FrontNormalNewtons < thresholds[j] * (z.FrontNormalNewtons + z.RearNormalNewtons))
                        under[j] += dt;
                rows.Add(new { Step = i, Data = Authority203(p, step, power), Sweep = after.Certificate,
                    Road = p.State.Rider.Road, after.Input.Pending, after.RequestedDrive });
            }
            if (exact != rows.Count) throw new InvalidOperationException("Accepted-state mismatch: " + name);
            if (name.Contains("learned-C") || name == "H25-same")
            {
                examples[name + "-min-front"] = Accepted203(archive, minIndex);
                examples[name + "-max-beta"] = Accepted203(archive, maxBeta);
            }
            if (name == "H26-LE-same")
            {
                var last = run.Samples[^1];
                var start = Sample199(last, archive.Lean[0][^1]);
                examples["start-0.37"] = new(archive.Handling, run.Spec, start.State, start.Active, .01f);
            }
            if (name == "H25-same")
            {
                int acceleration = Array.FindIndex(run.Samples, s => s.Input.Time >= 2.61 - 1e-9);
                examples["straight-2.60"] = Accepted203(archive, acceleration);
            }
            var decisions = Decisions203(archive);
            ledgers.Add(new { Name = name, run.FinishSeconds, Duration = duration, Steps = rows.Count, Exact = exact,
                Thresholds = thresholds, UnderSeconds = under, UnderPercent = under.Select(t => t * 100 / duration),
                MinimumFrontNewtons = lowest, MinimumFrontTime = run.Samples[minIndex - 1].Input.Time, Decisions = decisions.Count });
            Save(Path.Combine(output, name + "-model.json.gz"), new { Steps = rows, Decisions = decisions }, true);
        }

        using var matched = JsonDocument.Parse(File.ReadAllText(Path.Combine(input, "matched-irrevocable-prefix.json")));
        var root = matched.RootElement.GetProperty("Start").Deserialize<Start197>(Json)!;
        var spec = Policies202().Single(p => p.Variant == "H26-LE").Spec;
        examples["196-irrevocable"] = new(Settings202("H26-LE"), spec, root.State, root.Active, (float)(25.49 - root.State.Time));
        var residuals = new Dictionary<string, object>();
        var maps = new List<object>();
        var posture = new List<object>();
        foreach (var (name, probe) in examples)
        {
            var p = probe with { Settings = Settings202("H26-LE") };
            residuals[name] = Residual203(p, name is "start-0.37" or "196-irrevocable" ? 8192 : 96);
            foreach (float drive in new[] { 0f, .25f, .5f, .75f, .9f, 1f })
            foreach (float steer in new[] { -.16f, -.08f, -.04f, 0, .04f, .08f, .16f, p.Action.SteeringRadians }.Distinct())
            foreach (double front in new[] { .0005, .003435, .01, .05, .10, .20, .60 / 1.30 })
            {
                var q = p with { Action = new(steer, drive) };
                var loads = q.Split(front);
                var (lateral, lateralPower) = q.Evaluate(loads);
                var (combined, combinedPower) = q.Evaluate(loads, combined: true);
                maps.Add(new { Name = name, FrontFraction = front, Model = "lateral-first", Data = Authority203(q, lateral, lateralPower) });
                maps.Add(new { Name = name, FrontFraction = front, Model = "combined-slip", Data = Authority203(q, combined, combinedPower) });
            }
            foreach (double height in new[] { .45, .48, .50, .52, .55, .58, .60, .65 })
            foreach (float shift in new[] { -.06f, 0, .06f })
            {
                var bike = TwoContactBikeParameters.Default;
                var geometry = bike with { FrontAxleFromComMeters = bike.FrontAxleFromComMeters - shift,
                    RearAxleFromComMeters = bike.RearAxleFromComMeters + shift };
                var adjusted = p with { Settings = p.Settings with { H26 = p.Settings.H26! with { ComHeightMeters = height } } };
                try
                {
                    var (step, power) = adjusted.Evaluate(physics: Physics with { TwoContactBike = geometry });
                    posture.Add(new { Name = name, Height = height, Shift = shift, Data = Authority203(adjusted, step, power) });
                }
                catch (InvalidOperationException failure)
                {
                    posture.Add(new { Name = name, Height = height, Shift = shift, Failure = failure.Message });
                }
            }
            double v = p.State.Rider.ForwardSpeed;
            foreach (double slip in new[] { -.8, -.4, -.2, -.1, 0, .1, .2, .4, .8 })
            {
                double omega = (v + slip * Math.Sqrt(v * v + .25)) / .33;
                if (omega < 0)
                {
                    maps.Add(new { Name = name, ImposedKappa = slip, Outcome = "outside nonnegative wheel-speed domain" });
                    continue;
                }
                var q = p with { State = p.State with { Wheel = omega } };
                foreach (bool combined in new[] { false, true })
                {
                    var (step, power) = q.Evaluate(q.Split(.20), combined);
                    maps.Add(new { Name = name, ImposedKappa = slip, FrontFraction = .20,
                        Model = combined ? "combined-slip" : "lateral-first", Data = Authority203(q, step, power) });
                }
            }
        }
        var matchedProbe = examples["196-irrevocable"];
        foreach (float speed in new[] { 10f, 22f, 30f })
        foreach (float beta in new[] { -.20f, 0f, .20f })
        foreach (float yaw in new[] { -.8f, 0f, .8f })
        foreach (float gas in new[] { 0f, .5f, 1f })
        foreach (float steer in new[] { -.08f, 0f, .08f })
        foreach (double fraction in new[] { .01, .05, .20, .60 / 1.30 })
        {
            var state = matchedProbe.State;
            float direction = state.Rider.Heading - beta;
            var rider = state.Rider with { AngularVelocity = yaw,
                Velocity = new Vector2(MathF.Cos(direction), MathF.Sin(direction)) * speed };
            var probe = matchedProbe with { State = state with { Rider = rider }, Action = new(steer, gas), Dt = .01f };
            var (step, power) = probe.Evaluate(probe.Split(fraction));
            maps.Add(new { Name = "synthetic grid from 196", FrontFraction = fraction,
                Model = "lateral-first", Data = Authority203(probe, step, power) });
        }
        Save(Path.Combine(output, "reachability-model.json.gz"), maps, true);
        Save(Path.Combine(output, "posture-sensitivity-model.json.gz"), posture, true);
        Save(Path.Combine(output, "load-residual-model.json"), residuals);
        Save(Path.Combine(output, "summary-model.json"), new { Mass, Inertia, Runs = ledgers,
            ReachabilityRows = maps.Count, CpuWallSeconds = clock.Elapsed.TotalSeconds, NativeWorlds = 0 });
    }

    private static List<object> Decisions203(Archive198 archive)
    {
        var run = archive.Run.Riders.Single();
        var rows = new List<object>();
        var predictor = new Predictor198(Frame, Surface, Physics, archive.Handling, Timing, run.Spec, Mass, Inertia);
        var actions = run.Decisions.Select(d => d.Choice.Action).ToArray();
        for (int j = 0, sample = 0; j < actions.Length; j++)
        {
            var choice = run.Decisions[j].Choice;
            while (sample + 1 < run.Samples.Length && run.Samples[sample + 1].Input.Time <= choice.Time + 1e-8)
                sample++;
            if (actions[j].Drive >= .999f || Math.Abs(run.Samples[sample].Input.Time - choice.Time) > 1e-8 ||
                choice.Time + .20 > run.Samples[^1].Input.Time)
                continue;
            // Causal snapshot and original pending queue. Replace ONE review decision.
            var start = Sample199(run.Samples[sample], archive.Lean[0][sample]) with { Review = choice.Time };
            if (start.Pending.Any(x => x.ObservedAt >= choice.Time - 1e-9)) continue;
            var controls = actions.Skip(j).Take(25).ToArray();
            object Check(FreeRacingAction action, out string? outcome)
            {
                controls[0] = action;
                var cursor = predictor.Begin(start, controls, 0, Frame.LapMeters * 3 + 10, .5);
                cursor.Advance(100, choice.Time + .20);
                outcome = cursor.Outcome;
                return new { Action = action, Outcome = outcome, cursor.Sweep, cursor.Steps, End = cursor.Snapshot };
            }
            var baseline = Check(actions[j], out var accepted);
            var full = Check(actions[j] with { Drive = 1 }, out var failure);
            var heldActions = controls.ToArray();
            for (int k = 0; k < Math.Min(10, heldActions.Length); k++)
                heldActions[k] = heldActions[k] with { Drive = 1 };
            var sustained = predictor.Begin(start, heldActions, 0, Frame.LapMeters * 3 + 10, .5);
            sustained.Advance(130, Math.Min(choice.Time + .80, run.Samples[^1].Input.Time));
            var alternatives = new List<object>();
            bool fullAlternative = false;
            if (accepted is null && failure is not null)
                foreach (float delta in new[] { -.08f, -.04f, -.02f, .02f, .04f, .08f })
                {
                    var steer = Math.Clamp(actions[j].SteeringRadians + delta, -.61f, .61f);
                    alternatives.Add(Check(new(steer, 1), out var result));
                    fullAlternative |= result is null;
                }
            rows.Add(new { choice.Time, start.State.Rider.Road, start.State.Rider.TotalReferenceMeters,
                Baseline = baseline, BaselineOutcome = accepted, Full = full, FullOutcome = failure, FullAlternative = fullAlternative,
                SustainedFull = new { sustained.Outcome, sustained.Sweep, sustained.Steps, End = sustained.Snapshot },
                Alternatives = alternatives, start.Pending });
        }
        return rows;
    }
}
