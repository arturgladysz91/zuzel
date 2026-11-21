using System.Numerics;
using System.Text.Json;
using CoreSim;
using SharedWorldSpeedway;

namespace SpeedwayHandling;

internal static partial class Study191
{
    private static void RequireLedger203(string scenario)
    {
        string? ledger = Environment.GetEnvironmentVariable(Box2DConstructionBudget.LedgerVariable);
        if (ledger is null || !Path.IsPathFullyQualified(ledger) || !File.Exists(ledger))
            throw new InvalidOperationException("Create the study ledger and set SPEEDWAY_BOX2D_LEDGER before native validation.");
        Environment.SetEnvironmentVariable("SPEEDWAY_BOX2D_SCENARIO", scenario);
    }

    private static void SameNative203(string repository, string output, string variant, string tape)
    {
        if (tape is not ("H25-same" or "H26-LE-learned-C"))
            throw new ArgumentException("Choose a frozen nominal command archive.", nameof(tape));
        string name = variant + "-commands-" + tape;
        RequireLedger203(name);
        SameNative202(repository, output, variant,
            Path.Combine(repository, "data/experiments/speedway202", tape + ".json.gz"), name);
    }

    private static void CheckPosture203(string repository, string output)
    {
        var model = new RiderPostureH27();
        int assertions = 0;
        void Check(bool condition)
        {
            if (!condition) throw new InvalidOperationException("H27-P posture invariant failed.");
            assertions++;
        }
        foreach (double u in new[] { 0, .25, .5, .75, 1 })
        foreach (double z in new[] { -.14, -.10, -.06, -.02, 0 })
        foreach (double dt in new[] { .0025, .005, .01, .02, .25 })
        {
            var full = model.Prepare(147, 70, .55, z, u, dt);
            var first = model.Prepare(147, 70, .55, z, u, dt / 2);
            var second = model.Prepare(147, 70, .55, first.RiderHeightOffsetAfterMeters, u, dt / 2);
            Check(full.BikeKg + full.RiderKg == 147);
            Check(full.RiderHeightOffsetAfterMeters is >= -.14 and <= 0);
            Check(Math.Abs(full.RiderHeightOffsetAfterMeters - z) <= .5 * dt + 1e-12);
            Check(Math.Abs(full.RiderHeightOffsetAfterMeters - second.RiderHeightOffsetAfterMeters) < 1e-12);
            Check(Math.Abs(full.RiderMidpointHeightMeters - first.RiderHeightAfterMeters) < 1e-12);
        }
        string simulatedLedger = Path.Combine(Path.GetFullPath(output), "reservation-test.jsonl");
        using (File.Open(simulatedLedger, FileMode.CreateNew)) { }
        int rejected = 0;
        Parallel.For(0, 12, _ =>
        {
            try { Box2DConstructionBudget.Reserve(simulatedLedger, "reservation-only self-test"); }
            catch (InvalidOperationException error) when (error.Message.StartsWith("Box2D construction budget exhausted"))
            {
                Interlocked.Increment(ref rejected);
            }
        });
        Check(rejected == 4);
        Check(File.ReadLines(simulatedLedger).Count() == Box2DConstructionBudget.MaximumConstructions);
        var rootArchive = Read199<Archive198>(Path.Combine(repository, "data/experiments/speedway202/H25-same.json.gz"));
        var rootDriver = rootArchive.Run.Riders.Single();
        var launch = Sample199(rootDriver.Samples[0], rootArchive.Lean[0][0]) with { Pending = [], Review = 0 };
        var tape = rootDriver.Decisions.Select(d => d.Choice.Action).ToArray();
        using var matched = JsonDocument.Parse(File.ReadAllText(Path.Combine(repository,
            "data/experiments/speedway202/matched-irrevocable-prefix.json")));
        var match = matched.RootElement.GetProperty("Start").Deserialize<Start197>(Json)!;
        var rows = new List<object>();
        foreach (string variant in new[] { "H25", "H26-LE", "H27-P" })
        {
            var settings = Settings202(variant);
            var predict = new Predictor198(Frame, Surface, Physics, settings, Timing, rootDriver.Spec, Mass, Inertia);
            var launchTrial = predict.Begin(launch, tape, 0, 3 * Frame.LapMeters, .50);
            launchTrial.Advance(200, .75);
            rows.Add(new { Case = "full launch roll-forward", Variant = variant, launchTrial.Outcome, End = launchTrial.Snapshot,
                Posture = launchTrial.Checkpoint().RiderHeightOffsetMeters });
            foreach (double offset in (variant == "H27-P" ? new[] { 0d, -.07, -.105, -.14 } : new[] { 0d }))
            {
                var root = match with { RiderHeightOffsetMeters = offset };
                var cursor = predict.Begin(root, [], 0, 3 * Frame.LapMeters, .5);
                cursor.Advance(20, match.Review);
                rows.Add(new { Case = "196 pre-review feasibility", Variant = variant, InitialRiderOffset = offset,
                    cursor.Outcome, cursor.Steps, End = cursor.Snapshot, Posture = cursor.Checkpoint().RiderHeightOffsetMeters });
            }
        }
        Save(Path.Combine(output, "posture-check-model.json"), new { Assertions = assertions, Settings = model,
            Cases = rows, SimulatedReservations = 8, ExcessReservationsRejected = rejected, NativeWorlds = 0 });
    }

    private sealed record AcceptedNative203(Run144 Run, HandlingSettings Handling, LeanSample[][] Lean,
        TimedPostureH27[] Posture);

    private static void CheckHoldoutSolver203(string path, string training, string output)
    {
        var archive = Read199<AcceptedNative203>(path);
        var driver = archive.Run.Riders.Single();
        var last = driver.Samples[^1];
        double time = last.Input.Time;
        var checkpoint = Sample199(last, archive.Lean[0][^1]) with
        {
            Review = time,
            RiderHeightOffsetMeters = archive.Posture[^1].Posture.RiderHeightOffsetAfterMeters
        };
        var winner = Read199<Training199>(training).Winner.Result;
        var corridor = new Corridor199(Map199, double.PositiveInfinity);
        var predictor = new Predictor198(Frame, Surface, Physics, archive.Handling, Timing, driver.Spec, Mass, Inertia);
        var cursor = predictor.Begin(checkpoint, Pad200(winner.Controls!), (int)Math.Round(time / .05),
            Frame.LapMeters * 3, .5, constraint: corridor.Check);
        cursor.Advance(10, time + .05);
        cursor.Advance(30, time + .25);
        var failure = cursor.ContactFailure ?? throw new InvalidOperationException("Could not reproduce the solver failure.");
        var h26 = archive.Handling.H26!;
        var height = archive.Handling.PostureH27!.Prepare(Mass, driver.Spec.Profile.Gameplay.Physical.MassKg,
            h26.ComHeightMeters, failure.RiderHeightOffsetMeters, failure.Action.Drive, failure.Dt).CombinedMidpointHeightMeters;
        var fixedHeight = archive.Handling with { H26 = h26 with { ComHeightMeters = height }, PostureH27 = null };
        var probe = new Probe203(fixedHeight, driver.Spec, failure.State, failure.Action, failure.Dt);
        var steps = new List<object>();
        foreach (float dt in new[] { .0025f, .005f, .01f, .02f })
        {
            var p = probe with { Dt = dt };
            try
            {
                var (step, power) = p.Evaluate();
                steps.Add(new { Dt = dt, step.LoadH26, Force = Authority203(p, step, power) });
            }
            catch (InvalidOperationException error)
            {
                steps.Add(new { Dt = dt, Failure = error.Message });
            }
        }
        Save(Path.Combine(output, "holdout-solver-model.json"), new { Checkpoint = checkpoint, Failure = failure,
            Height = height, Residual = Residual203(probe, 8192), FixedHeightDtSensitivity = steps, NativeWorlds = 0 });
    }

    private static void CheckNative203(string archives, string output)
    {
        var checks = new List<object>();
        foreach (string path in Directory.GetFiles(archives, "*.json.gz").Order(StringComparer.Ordinal))
        {
            var archive = Read199<AcceptedNative203>(path);
            var driver = archive.Run.Riders.Single();
            double riderOffset = 0;
            int exact = 0;
            var corridor = new Corridor199(Map199);
            var technique = new Technique200(corridor.Windows).Begin();
            object? corridorFailure = null;
            object? techniqueFailure = null;
            foreach (var sample in driver.Samples)
            {
                var x = sample.Input;
                if (corridorFailure is null && corridor.Check(x.Rider, sample.Outer) is { } strict)
                    corridorFailure = new { x.Time, x.Rider.TotalReferenceMeters, Reason = strict, x.Rider.AngularVelocity };
                if (techniqueFailure is null && technique.Observe(x.Rider, x.Time) is { } style)
                    techniqueFailure = new { x.Time, x.Rider.TotalReferenceMeters, Reason = style };
            }
            for (int i = 1; i < driver.Samples.Length; i++)
            {
                var previous = driver.Samples[i - 1];
                var sample = driver.Samples[i];
                var x = previous.Input;
                var road = x.Rider.Road;
                var width = LaneModel.UsableRacingWidthMeters(Frame.Track.Segments[road.Segment].Type, Frame.Track.Geometry);
                var grip = Surface.SampleSurface(road.Segment, Math.Clamp((float)(road.OffsetMeters / width) * 4, 0, 4));
                var wheel = Speed148.ForecastFork148.Wheel(x.Omega, Physics.RearWheel);
                var shaft = new CrankClutch144(omega: previous.CrankOmega, lossH26: archive.Handling.H26?.EngineLoss);
                var step = HandlingContact.Prepare(archive.Handling, grip, x.Rider with { Drive = sample.Input.Active.Drive },
                    driver.Spec, Physics, x.Time, Mass, Inertia, wheel, x.Steering, archive.Lean[0][i - 1].ActualRadians,
                    (float)sample.Powertrain!.Seconds, sample.Input.Active.SteeringRadians, shaft,
                    RiderController.ReactionSeconds(driver.Spec), riderOffset);
                var expected = sample.Forces!.Value;
                if (expected.FrontForce != step.Forces.FrontForce || expected.RearForce != step.Forces.RearForce ||
                    expected.SteeringAfter != step.Forces.SteeringAfter || sample.CrankOmega != shaft.Prepared.AfterOmega ||
                    sample.Input.Omega != shaft.Prepared.Wheel.AfterOmega || archive.Lean[0][i].ActualRadians != step.LeanAfter)
                    throw new InvalidOperationException($"Native force parity failed for {path}, step {i}.");
                if (step.PostureH27 is { } posture)
                {
                    if (posture != archive.Posture[i - 1].Posture)
                        throw new InvalidOperationException($"Native posture parity failed for {path}, step {i}.");
                    riderOffset = posture.RiderHeightOffsetAfterMeters;
                }
                exact++;
            }
            checks.Add(new { Name = Path.GetFileNameWithoutExtension(Path.GetFileNameWithoutExtension(path)),
                ExactSteps = exact, CorridorFailure = corridorFailure, TechniqueFailure = techniqueFailure });
        }
        Save(Path.Combine(output, "native-parity-and-style.json"), new { Checks = checks, NativeWorlds = 0 });
    }
}
