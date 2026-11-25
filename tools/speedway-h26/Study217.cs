
using System.Numerics;
using SharedWorldSpeedway;
using WholeCorner171;

namespace SpeedwayHandling;

internal static partial class Study191
{
    private const double AuditHorizon217 = 9;
    private sealed record SearchArchive217(BendVisit Visit, CornerOutcome216[] Attempts);
    private sealed record Seed217(string Name, double? Kph, int? ArchiveId, State215 State, double Reference, BendVisit Bend,
        CornerTuning216 Tuning, Activation215[] Pending, Forces215? Previous, string Provenance);
    private sealed record Decision217(double Time, double Reference, double SpeedKph, Command215 Prior,
        LegacyDecision217 Legacy, LegacyDecision217? Ablation, PreviewDecision217? Preview, Command215? Chosen);
    private sealed record Safety217(double MaxSteer, double MaxSteerRate, double MaxActiveForce, double MaxActivePower,
        double MaxTyreUtilization, double MaxAirborneContactForce, double MinimumVerifiedForecastClearance);
    private sealed record Outcome217(string Seed, string Policy, CornerOutcome216 Rollout, int Commands,
        int MatchedDecisionChanges, int LegacyCutOpportunities, int RequestsAtFullDriveInCutStates,
        double ActiveZeroSecondsInLegacyCutRegion, double ActiveFullSecondsInLegacyCutRegion,
        double FirstFullDriveSeconds, Decision217? First, Decision217? FirstEntryCut,
        int ForecastProposals, int ForecastVerifications, string? Abstention, Safety217 Mechanical);
    private sealed record Run217(Outcome217 Summary, Decision217[] Decisions, Step215[] Trace);
    private sealed record Batch217(Seed217[] Seeds, Outcome217[] Outcomes);

    private static void ControllerAudit217(string root, string output)
    {
        var (_, spec) = CourseFixture208(root, "C");
        var fixture = new Fixture216(spec);
        var course = new CornerCourse216(new RacingLineGeometry(Frame), RootFor210(root, "C").State.State.Rider);
        string parent = Path.Combine(root, "data/experiments/speedway216");
        var archive = ReadPlain214<SearchArchive217>(Path.Combine(parent, "corner-search.json"));
        var candidates = archive.Attempts.Where(x => x.RequestedKph.HasValue && x.Settings is
            { Lane: 3.6, BendDrive: 1, InitialBank: -.24, RollGain: 15 }).OrderBy(x => x.RequestedKph)
            .ThenBy(x => x.Settings.Leg).ToArray();
        if (candidates.Length != 10) throw new InvalidOperationException("expected ten preregistered H35 full-Drive roots");
        var ablations = Enum.GetValues<Ablation217>();
        Save(Path.Combine(output, "protocol.json"), new
        {
            Base = "f7888f6fe00b35d6b8134309401d18f0428bba8f",
            Scope = "MODEL diagnostics. No official, sporting or native qualification.",
            AuditHorizon217, PredictionSeconds217, ProposalStep217, ExecutionStep = StepSeconds216,
            ControlPeriod216, ReactionSeconds216, CornerCourse216.RequiredClearance, FootprintRadius216,
            ModelSteer = Steering217, Muscle = Actuator215.Limited, Geometry = fixture.Base, Gravity216,
            GeometryDomain = new { Geometry215.MaxRoll, Geometry215.MaxPitch, Geometry215.MaxCompression },
            LegacyAblations = ablations.Select(a => a.ToString()).ToArray(),
            RetainedForecastCandidatesForRefinement = 2, MaxExecutionRechecks = 3,
            AllRoots = candidates.Select(c => new { c.Id, c.RequestedKph, c.Settings })
        });
        var roots = candidates.Select(archived =>
        {
            string name = $"{archived.RequestedKph:F0}-{(archived.Settings.Leg ? "leg" : "wheels")}";
            return new Seed217(name, archived.RequestedKph, archived.Id, archived.Initial, archived.InitialReference,
                archive.Visit, archived.Settings, [], null, "Exact H35 #216 start including active command");
        }).Append(ExitSeed()).ToArray();
        var batches = new Batch217[roots.Length];
        // Each root uses its own mechanical solver and command history. Archive
        // order is the fixture order regardless of the thread that finishes first.
        Parallel.For(0, roots.Length, new ParallelOptions { MaxDegreeOfParallelism = Math.Min(8, Environment.ProcessorCount) },
            index => batches[index] = Batch(roots[index]));
        var seeds = batches.SelectMany(b => b.Seeds).ToArray();
        var results = batches.SelectMany(b => b.Outcomes).ToArray();
        Save(Path.Combine(output, "seeds.json"), seeds);
        Save(Path.Combine(output, "outcomes.json"), results);
        Console.WriteLine($"H35 controller audit recorded {seeds.Length} matched states and {results.Length} rollouts.");

        Batch217 Batch(Seed217 rootSeed)
        {
            var states = new List<Seed217>();
            var observations = new List<Outcome217>();
            var fork = Study(rootSeed, rootSeed.Kph.HasValue);
            if (fork != null) Study(fork, captureFork: false);
            return new(states.ToArray(), observations.ToArray());

            Seed217? Study(Seed217 seed, bool captureFork)
            {
                Seed217? match = null;
                states.Add(seed);
                var geometry = fixture.Geometry(seed.Tuning.Leg ? new Leg216() : null);
                var model = fixture.Engine(geometry, NominalSupport216, Actuator215.Limited, true);
                int firstResult = observations.Count;
                foreach (var ablation in ablations)
                    Store(Run(seed, geometry, model, ablation, captureFork && ablation == Ablation217.Baseline ? Observe : null),
                        observations);
                Store(Run(seed, geometry, model, null, null), observations);
                var baseResult = observations[firstResult];
                var noCut = observations[firstResult + 1];
                var predictive = observations[^1];
                Console.WriteLine($"{seed.Name}: base {baseResult.Rollout.Stop} ({baseResult.Rollout.BendFullDrive:F3}s full Drive), " +
                    $"no-cut {noCut.Rollout.Stop} ({noCut.Rollout.BendFullDrive:F3}s), " +
                    $"predictive {predictive.Rollout.Stop} ({predictive.Rollout.BendFullDrive:F3}s)");
                return match;

                void Observe(CornerObservation217 o, LegacyDecision217 action)
                {
                    if (match != null || !action.EntryCutApplied || o.State.Next == o.Queue.Length) return;
                    match = seed with
                    {
                        Name = seed.Name + "-gate", State = o.State, Reference = o.Reference, Pending = o.Queue,
                        Previous = o.Previous,
                        Provenance = "First legacy zero-Drive decision with a queued future activation in the accepted prefix"
                    };
                }
            }
        }

        void Store(Run217 run, List<Outcome217> results)
        {
            results.Add(run.Summary);
            string seed = run.Summary.Seed;
            string policy = run.Summary.Policy;
            bool example = seed is "90-leg-gate" or "120-wheels-gate" or "exit-witness" or "80-leg";
            if (example && policy is "Baseline" or "NoEntryDriveCut" or "NoExtraSteeringClamp" or "Predictive")
                Save(Path.Combine(output, $"trace-{seed}-{policy}.json.gz"), run, true);
        }

        Run217 Run(Seed217 seed, Geometry215 g, UnifiedModel215 model, Ablation217? ablation,
            Action<CornerObservation217, LegacyDecision217>? capture)
        {
            var decisions = new List<Decision217>();
            var trial = RunCorner216(seed.ArchiveId ?? 0, seed.Name, seed.Kph, g, model, seed.State, seed.Reference,
                seed.Bend, seed.Tuning, AuditHorizon217, course, Decide, seed.Pending, seed.Previous);
            var comparison = trial.Trace;
            double reference = seed.Reference;
            double zero = 0, full = 0, firstFull = -1, verified = double.PositiveInfinity;
            double frontAirForce = 0, maxSteer = Math.Abs(seed.State.Steer), maxSteerRate = 0;
            double peakForce = 0, peakPower = 0, utilization = 0;
            var beforeMark = course.Mark(seed.State, g);
            foreach (var frame in comparison)
            {
                var after = course.Mark(frame.After, g);
                double delta = Frame.UnwrapDelta(beforeMark.Road.ReferenceMeters, after.Road.ReferenceMeters);
                double seconds = Duration215(frame);
                double midReference = reference + delta / 2;
                var m = g.Motion(frame.Midpoint);
                var midRoad = Frame.RoadAt(midReference, (beforeMark.Road.OffsetMeters + after.Road.OffsetMeters) / 2);
                var legacy = LegacyControl217(g, frame.Midpoint, m, midRoad, midReference, seed.Bend,
                    seed.Tuning, frame.Forces);
                float active = frame.Before.Command.Bike.Drive;
                if (legacy.EntryGate)
                {
                    if (active == 0) zero += seconds;
                    if (active >= .9999) full += seconds;
                }
                if (firstFull < 0 && active >= .9999)
                    firstFull = frame.Before.Time - seed.State.Time;
                reference += delta;
                beforeMark = after;
                var forces = frame.Forces;
                maxSteer = Math.Max(maxSteer, Math.Abs(frame.After.Steer));
                maxSteerRate = Math.Max(maxSteerRate, Math.Abs(forces.SteerRate));
                peakForce = Math.Max(peakForce, forces.Rider.Norm + Math.Abs(forces.LeftFoot?.MuscleForce ?? 0));
                peakPower = Math.Max(peakPower, forces.LeftFoot?.AbsoluteMusclePower ?? Math.Abs(frame.ActuatorPower));
                utilization = Math.Max(utilization, Math.Max(forces.Front.Utilization, forces.Rear.Utilization));
                if (forces.Front.Patch.Gap >= 0) frontAirForce = Math.Max(frontAirForce, forces.Front.Force.Norm);
                if (forces.Rear.Patch.Gap >= 0) frontAirForce = Math.Max(frontAirForce, forces.Rear.Force.Norm);
                if (forces.LeftFoot is { Contact.Gap: >= 0 } foot)
                    frontAirForce = Math.Max(frontAirForce, foot.Force.Norm);
            }
            foreach (var record in decisions)
                if (record.Preview?.PredictedMinimumSwept is { } margin) verified = Math.Min(verified, margin);
            var limits = new Safety217(maxSteer, maxSteerRate, peakForce, peakPower, utilization, frontAirForce,
                double.IsFinite(verified) ? verified : -1);
            if (maxSteer > Steering217.MaximumSteeringRadians + 1e-7 ||
                maxSteerRate > Steering217.SteeringRateRadiansPerSecond + 1e-7 ||
                peakForce > 700.00001 || peakPower > 350.00001 || utilization > 1.000001 ||
                frontAirForce != 0 || verified < CornerCourse216.RequiredClearance)
                throw new InvalidOperationException("a diagnostic exceeded an unchanged mechanical limit");
            var summary = new Outcome217(seed.Name, ablation?.ToString() ?? "Predictive", trial.Summary, decisions.Count,
                decisions.Count(r => r.Chosen.HasValue && r.Chosen.Value != r.Legacy.Command),
                decisions.Count(r => r.Legacy.EntryCutApplied),
                decisions.Count(r => r.Legacy.EntryCutApplied && r.Chosen?.Bike.Drive >= .9999),
                zero, full, firstFull, decisions.FirstOrDefault(), decisions.FirstOrDefault(r => r.Legacy.EntryCutApplied),
                decisions.Sum(r => r.Preview?.Proposals ?? 0), decisions.Sum(r => r.Preview?.Verified ?? 0),
                decisions.LastOrDefault()?.Preview?.Reason, limits);
            return new(summary, decisions.ToArray(), comparison);

            Command215? Decide(CornerObservation217 o)
            {
                var motion = g.Motion(o.State);
                var baseCommand = LegacyControl217(g, o.State, motion, o.Bike.Road, o.Reference, o.Bend, o.Tuning, o.Previous);
                capture?.Invoke(o, baseCommand);
                LegacyDecision217? changed = ablation is { } kind
                    ? LegacyControl217(g, o.State, motion, o.Bike.Road, o.Reference, o.Bend, o.Tuning, o.Previous,
                        kind, seed.State.Command.Target)
                    : null;
                var preview = ablation.HasValue ? null : PredictiveControl217(o, model, course);
                var chosen = changed?.Command ?? preview?.Command;
                decisions.Add(new(o.State.Time, o.Reference, 3.6 * o.Bike.Speed, o.State.Command, baseCommand,
                    changed, preview, chosen));
                return chosen;
            }
        }

        Seed217 ExitSeed()
        {
            var standingCircle = ReadPlain214<Balance215>(Path.Combine(parent, "circle.json")).State;
            double reference = archive.Visit.Exit - 6;
            const double lane = 5;
            var (position, road) = Frame.At(reference, lane);
            var rotation = Attitude215.Pose(road.Heading, 0, 0);
            var aligned = standingCircle with
            {
                Center = new(position.X, position.Y, standingCircle.Center.Z),
                Pose = standingCircle.Pose.Advance(Vec215.Up, road.Heading),
                Momentum = rotation.Apply(standingCircle.Momentum), Angular = rotation.Apply(standingCircle.Angular)
            };
            return new("exit-witness", null, null, aligned, reference, archive.Visit,
                new(lane, .66f, aligned.Pose.Roll, 15, false), [], null,
                "H35 mechanically balanced rolling circle rigidly rotated and translated to six metres before exit");
        }
    }
}
