using System.Diagnostics;
using System.IO.Compression;
using System.Numerics;
using System.Security.Cryptography;
using System.Text.Json;
using CoreSim;
using CoreSim.PhysicalSpace;
using Efficient147;
using Efficient152;
using FullDrive169;
using Safety153;
using SharedWorldSpeedway;

namespace TrackGrip175;

internal static class Program
{
    private const float Mass = 147, Inertia = 52.72097f;
    private const double PoseSweep = .5, Horizon = .44;
    private const string Docs = "docs/experiments/";
    private static readonly JsonSerializerOptions Json = new(FreeLearningExperiment.Json) { WriteIndented = false };
    private static readonly WorldParameters Physics = FreeLearningExperiment.Parameters();
    private static readonly RearWheelParameters Wheel = RearWheelParameters.Default;
    private static readonly TwoContactBikeParameters Bike = TwoContactBikeParameters.Default;
    private static readonly TrackFrame Base = StandingWorld144.Frame();
    private static readonly TrackFrame Width = TrackAwarenessExperiment134.Frame("progressive-c");
    private static readonly TrackFrame Round = TrackAwarenessExperiment134.Frame("round-smooth");
    private static readonly TrackStateSnapshot Surface = MultiActionExperiment135.Surface(Base);
    private static readonly TrackStateSnapshot Damp = TrackState.CreateDefault(Base.Track, new(1, 0, .35f)).Snapshot();
    private static readonly LoadBasedTyreParameters Grip = LoadBasedTyreParameters.Default;
    private static readonly RacingLineGeometry WidthClearance = new(Width);
    private static readonly PredictionBoundary147 Cache = new(Base);
    private static readonly Boundary152 Boundary = new(Base);
    private static double Cpu => Process.GetCurrentProcess().TotalProcessorTime.TotalSeconds;

    private static int Main(string[] args)
    {
        if (args.Length != 2 || args[0] != "--measure")
        {
            Console.Error.WriteLine("Study #175 OFF. Explicit --measure and a new evidence directory required.");
            return 2;
        }
        var target = Path.GetFullPath(args[1]);
        if (Directory.Exists(target))
            throw new IOException("Choose a new evidence directory.");
        Directory.CreateDirectory(target);
        var started = Cpu;
        string? failure = null;
        try
        {
            Save(target, "geometry.json.gz", new[]
            {
                Geometry("progressive-b", Base), Geometry("progressive-c", Width),
                Geometry("round-smooth", Round), Geometry("circular-baseline", TrackGeometryAudit.Frame(null))
            });
            var rows = new List<object>();
            using var old = ZipFile.OpenRead(Docs + "three-lap-global-planning/172-validation-K4.zip");
            for (int i = 1; i <= 5; i++)
            {
                string trial = $"v1-K4-S{i}";
                rows.AddRange(Measure(ReadRun(old, trial + ".zip"), "172/" + trial));
            }
            using var newer = ZipFile.OpenRead(Docs + "state-feasible-throttle/174-native.zip");
            foreach (var id in new[] { "P7", "P8", "X4" })
                rows.AddRange(Measure(ReadRun(newer, $"d1-{id}.zip"), "174/" + id));
            Save(target, "contact-pairs.json.gz", rows);
            Save(target, "parameters.json.gz", new
            {
                Mass, Inertia, Physics, Bike, Wheel, Engine = Engine500Parameters.Default,
                Engine500.Curve, Engine500.CutoffRpm, Clutch = LaunchParameters144.Default,
                OriginalSurface = Surface.SampleSurface(5, 2),
                M1Surface = Damp.SampleSurface(5, 2),
                Capacities = LoadBasedTyreGrip.Budget(Mass, Surface.SampleSurface(5, 2), Bike)
            });
            Console.WriteLine($"{rows.Count} paired archived observations. No native worlds.");
            return 0;
        }
        catch (Exception e)
        {
            failure = e.GetType().Name + ": " + e.Message;
            throw;
        }
        finally
        {
            Save(target, "measure-cost.json.gz", new { CpuSeconds = Cpu - started, NativeWorlds = 0, Failure = failure });
        }
    }

    private static void Save<T>(string dir, string name, T data)
    {
        using var file = new FileStream(Path.Combine(dir, name), FileMode.CreateNew);
        using var gzip = new GZipStream(file, CompressionLevel.SmallestSize);
        JsonSerializer.Serialize(gzip, data, Json);
    }

    private static Run144 ReadRun(ZipArchive source, string name)
    {
        using var buffer = new MemoryStream();
        using (var member = source.GetEntry(name)!.Open()) member.CopyTo(buffer);
        buffer.Position = 0;
        using var inner = new ZipArchive(buffer);
        using var native = inner.GetEntry("native.json")!.Open();
        return JsonSerializer.Deserialize<Run144>(native, Json)!;
    }

    private static object Geometry(string name, TrackFrame frame)
    {
        var starts = Enumerable.Range(0, 9).Select(frame.SegmentStartMeters).Append(frame.LapMeters).ToArray();
        var denseCount = (int)Math.Ceiling(frame.LapMeters / .025);
        var dense = Enumerable.Range(0, denseCount + 1)
            .Select(i => GeometryPoint(frame, i * frame.LapMeters / denseCount)).ToArray();
        var bends = new[] { 1, 5 }.Select(i =>
        {
            var length = starts[i + 3] - starts[i];
            return Enumerable.Range(0, (int)Math.Ceiling(length / .5))
                .Select(j => .5 * j).Append(length)
                .Select(d => new { Progress = d, Geometry = GeometryPoint(frame, starts[i] + d) }).ToArray();
        });
        return new { Name = name, frame.LapMeters, Starts = starts, Dense = dense, Bends = bends };
    }

    private static GeometrySample GeometryPoint(TrackFrame frame, double reference)
    {
        var road = frame.RoadAt(reference, 0);
        var width = frame.PhysicalWidthMeters(road);
        var point = frame.Shape?.At(reference);
        var p = point?.Position ?? frame.Embedding.Segments[road.Segment].Map(road.Progress, 0).Position;
        var k = point?.Curvature ?? (road.IsTurn ? 1 / road.RadiusMeters : 0);
        var outward = new MeterPoint(Math.Sin(road.Heading), -Math.Cos(road.Heading));
        return new(reference, k, road.Heading, width, p, p - outward, p + outward * (width - 1),
            p + outward * (width / 2 - 1), p + outward * 3.5);
    }

    private sealed record GeometrySample(double S, double K, double Heading, double Width, MeterPoint Reference,
        MeterPoint Inner, MeterPoint Outer, MeterPoint Middle, MeterPoint Offset35);

    private static IEnumerable<object> Measure(Run144 run, string source)
    {
        var rider = run.Riders.Single();
        var entry = Base.SegmentStartMeters(5);
        var exit = Base.SegmentStartMeters(8);
        // Spatial locations fixed across all worlds. One extra actual high-spin exit witness.
        var targets = new (string Name, double S)[]
        {
            ("straight", 150), ("prepare", entry - 10), ("entry", entry + 10),
            ("middle", (entry + exit) / 2), ("exit", exit - 4), ("following", exit + 15)
        };
        foreach (var (name, s) in targets)
        {
            var i = Array.FindIndex(rider.Samples, x => x.Input.Rider.TotalReferenceMeters >= s);
            if (i < 1 || i + 1 >= rider.Samples.Length)
                throw new InvalidDataException($"Missing {source} at {name}.");
            yield return Pair(source, name, rider, i);
        }
        var spin = Enumerable.Range(1, rider.Samples.Length - 2).Where(i =>
            Math.Abs(rider.Samples[i].Input.Rider.TotalReferenceMeters - exit) <= 8)
            .MaxBy(i => rider.Samples[i + 1].Powertrain!.Wheel.InstantSlipRatio);
        yield return Pair(source, "exit-wheelspin", rider, spin);
    }

    private static object Pair(string source, string region, RiderRun144 rider, int index)
    {
        var sample = rider.Samples[index];
        var next = rider.Samples[index + 1];
        var before = rider.Samples[index - 1];
        var v = sample.Input.Rider with { Drive = next.Input.Active.Drive };
        var action = next.Input.Active;
        var spec = rider.Spec;
        var dt = (float)next.Powertrain!.Seconds;
        var release = RiderController.ReactionSeconds(spec);
        var previousDt = (float)(sample.Input.Time - before.Input.Time);
        var ax = Contact169.Acceleration(before.Input.Rider, sample.Input.Rider, previousDt);
        TwoContactForces Query(TrackFrame frame, TrackStateSnapshot surface, WorldParameters physics,
            FreeRacingAction command, out ClutchStep144 power)
        {
            var shaft = new CrankClutch144(omega: sample.CrankOmega);
            var wheel = new RearWheelState(sample.Input.Omega * Wheel.WheelRadiusMeters);
            var body = v with { Drive = command.Drive };
            var force = Contact144.Forces(body, spec, frame, surface, physics, sample.Input.Time, Mass, wheel,
                new(sample.Input.Steering), dt, steeringOverride: command.SteeringRadians, inertia: Inertia,
                powertrain: shaft, releaseAt: release);
            power = shaft.Prepared;
            return force;
        }

        var baseForce = Query(Base, Surface, Physics, action, out var basePower);
        var baseNative = next.Forces!.Value;
        var delta = Math.Max((baseForce.NetForce - baseNative.NetForce).Length(),
            Math.Abs(baseForce.NetTorque - baseNative.NetTorque));
        if (delta > .015 || Math.Abs(basePower.AfterOmega - next.CrankOmega) > .001)
            throw new InvalidDataException($"Archived contact mismatch: {source}/{region}: {delta}.");
        var results = new List<object> { Result("BASE", v, baseForce, basePower) };
        foreach (var (id, surface, mu) in new[] { ("M1", Damp, 1d), ("M2", Surface, 1.1), ("M3", Surface, .9) })
        {
            var f = Query(Base, surface, Physics with
            {
                LoadBasedTyres = Grip with { ReferenceFrictionCoefficient = Grip.ReferenceFrictionCoefficient * mu }
            }, action, out var p);
            results.Add(Result(id, v, f, p));
        }
        var wider = Query(Width, Surface, Physics, action, out var widePower);
        results.Add(Result("C-width-only", v, wider, widePower));
        var (allocation, allocatedPower) = Allocate(baseForce, v, sample, spec, dt, release);
        results.Add(Result("M4", v, allocation, allocatedPower));
        foreach (var h in new[] { .35, .55, .75 })
        {
            var shaft = new CrankClutch144(omega: sample.CrankOmega);
            var f = Contact169.Forces(v, spec, Base, Surface, Physics, sample.Input.Time, Mass,
                new(sample.Input.Omega * Wheel.WheelRadiusMeters), new(sample.Input.Steering), dt,
                action.SteeringRadians, shaft, release, new("load-only", h), ax);
            results.Add(Result($"F-load-{(int)(100 * h)}cm", v, f, shaft.Prepared));
        }
        foreach (float gas in new[] { 0f, .25f, .5f, .75f, .95f, 1f })
        {
            var f = Query(Base, Surface, Physics, action with { Drive = gas }, out var p);
            results.Add(Result($"gas-{(int)Math.Round(gas * 100)}", v, f, p));
        }
        var closestReview = rider.Decisions.First(d => d.Choice.Time >= sample.Input.Time - 1e-8);
        var reviewTime = closestReview.Choice.Time;
        var review = rider.Samples.MinBy(x => Math.Abs(x.Input.Time - reviewTime))!;
        var input = review.Input with
        {
            Time = reviewTime, NextReview = reviewTime,
            Pending = review.Input.Pending.Where(q => q.ObservedAt < reviewTime - 1e-8 &&
                q.ActivationAt >= reviewTime - 1e-8).ToArray(),
            Rider = review.Input.Rider with { Drive = review.Input.Active.Drive }
        };
        var historical = closestReview.Choice.Action;
        var forecasts = new List<object> { Forecast("A-recorded", input, review.CrankOmega, spec, historical) };
        foreach (var change in new[] { 0f, -.01f, .01f })
        {
            float steer = Math.Clamp(historical.SteeringRadians + change, -Bike.MaximumSteeringRadians, Bike.MaximumSteeringRadians);
            forecasts.Add(Forecast(change == 0 ? "B-100-fixed" : change < 0 ? "B-100-left" : "B-100-right",
                input, review.CrankOmega, spec, new(steer, 1)));
        }
        var widthGap = WidthClearance.Clearance(v);
        var kBase = Base.Shape!.At(v.TotalReferenceMeters).Curvature;
        var kRound = Round.Shape!.At(v.TotalReferenceMeters).Curvature;
        var r = v.Road.OffsetMeters;
        return new
        {
            Source = source, Region = region, Index = index, rider.Spec, Sample = sample, Next = next,
            StateSha256 = Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new { sample.Input, sample.CrankOmega, rider.Spec }, Json))),
            ArchivedForceError = delta, Ax = ax, WidthGap = new[] { widthGap.Inner, widthGap.Outer },
            KBase = kBase / (1 + r * kBase), KRound = kRound / (1 + r * kRound),
            ReviewInput = input, ReviewCrank = review.CrankOmega, Results = results, Forecasts = forecasts
        };
    }

    // Same pure-slip curves, total budget, front force, relaxation scale, tyre radius,
    // inertia, actuator and passive clutch. Only rear allocation at nonzero a AND b changes.
    private static (TwoContactForces Force, ClutchStep144 Power) Allocate(TwoContactForces original, RiderView v,
        StartSample144 sample, RiderSpec spec, float dt, double release)
    {
        var loads = original.TyreLoads!;
        float ur = Vector2.Dot(original.RearVelocity, original.BodyForward);
        var wr = Vector2.Dot(original.RearVelocity, original.BodyLeft);
        var y2 = Contact169.ScaleY2(ur, original.Budget / Mass);
        double b = wr / Math.Sqrt(y2);
        double share = 1 / Math.Sqrt(1 + b * b);
        float fy = (float)(-loads.RearCapacityNewtons * b * share);
        var external = Vector2.Dot(original.Resistance + original.FrontForce, original.BodyForward);
        var engine = Engine500Parameters.Default;
        double ratio = engine.ShortOverallRatio + (engine.LongOverallRatio - engine.ShortOverallRatio) * spec.Setup.Gearing;
        // RearWheelDynamics' constitutive relaxation is rearBudget * mobility * seconds.
        // Keep that product fixed while passing the remaining longitudinal capacity.
        var material = Wheel with { MinimumContactRelaxationSeconds = Wheel.MinimumContactRelaxationSeconds / share };
        var crank = new CrankClutch144(omega: sample.CrankOmega);
        var command = crank.Prepare(v.Drive * (1 - v.Impairment), ratio, engine.Efficiency, dt,
            sample.Input.Time >= release - 1e-10, torque =>
            {
                var step = RearWheelDynamics.Evaluate(ur, 0, sample.Input.Omega, original.Budget, Mass, y2,
                    torque, Math.Abs(torque), torque, external, dt, material,
                    rearBudgetNewtons: loads.RearCapacityNewtons * share);
                double utilization = Math.Sqrt(step.LongitudinalNewtons * (double)step.LongitudinalNewtons + fy * (double)fy)
                    / loads.RearCapacityNewtons;
                return step with { RearLateralNewtons = fy, RearUtilization = utilization };
            });
        var rear = crank.Prepared.Wheel;
        return (original with
        {
            RearLateral = fy, RearLongitudinal = rear.LongitudinalNewtons, Engine = command,
            RearForce = original.BodyForward * rear.LongitudinalNewtons + original.BodyLeft * fy
        }, crank.Prepared);
    }

    private static object Result(string id, RiderView v, TwoContactForces f, ClutchStep144 p)
    {
        var tangent = Vector2.Normalize(v.Velocity);
        var normal = new Vector2(-tangent.Y, tangent.X);
        var rear = f.TyreLoads!.RearCapacityNewtons;
        return new
        {
            Id = id, Force = f, Power = p,
            Along = Vector2.Dot(f.NetForce, tangent) / Mass, Normal = Vector2.Dot(f.NetForce, normal) / Mass,
            YawAcceleration = f.NetTorque / Inertia,
            FyRemainingAtFx = Math.Sqrt(Math.Max(0, rear * rear - f.RearLongitudinal * (double)f.RearLongitudinal))
        };
    }

    private static object Forecast(string id, FreeRacingInput input, double crank, RiderSpec spec, FreeRacingAction action)
    {
        var started = Stopwatch.GetTimestamp();
        var prediction = PredictorA153.Predict(input, spec, Base, Surface, Physics, Mass, Inertia, Horizon,
            candidate: action, reviewSeconds: .2, powertrain: new(omega: crank), boundaryCache: Cache,
            fastBoundary: Boundary, exactInitialWheel: true);
        double sweep = double.PositiveInfinity, pose = double.PositiveInfinity, beta = 0;
        object? firstFailure = null;
        for (int i = 0; i < prediction.Points.Length; i++)
        {
            var b = prediction.Points[i];
            pose = Math.Min(pose, Math.Min(b.Inner, b.Outer));
            beta = Math.Max(beta, Math.Abs(b.Rider.SlideRadians) * 180 / Math.PI);
            if (i > 0)
            {
                var a = prediction.Points[i - 1];
                sweep = Math.Min(sweep, Clearance153.SweepLowerBound(a.Rider, b.Rider, b.Time - a.Time,
                    (a.Inner, a.Outer), (b.Inner, b.Outer)));
            }
            if (firstFailure is null && (pose < PoseSweep || sweep < PoseSweep || beta > 45))
                firstFailure = new { b.Time, b.Rider.TotalReferenceMeters, Reason = beta > 45 ? "BETA45" : "POSE_OR_SWEPT" };
        }
        return new { Id = id, Action = action, Horizon, Pose = pose, Sweep = sweep, Beta = beta, FirstFailure = firstFailure,
            prediction.ForceEvaluations, WallMilliseconds = Stopwatch.GetElapsedTime(started).TotalMilliseconds,
            prediction.Points };
    }
}
