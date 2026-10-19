using System.Diagnostics;
using System.Globalization;
using System.Numerics;
using System.Text.Json;
using CoreSim;
using CoreSim.Interactions;
using CoreSim.PhysicalSpace;
using CoreSim.Setup;

namespace SharedWorldSpeedway;

/// <summary>Read-only #92 measurements. No alternate integration, tuning or runtime commands.</summary>
public static class FullHeatSoloAudit
{
    public const string Baseline = "a56a2b777ca28274c2ea227f51cbfb1ee087a60b";
    public const double StepSeconds = .01, MaximumSeconds = 90;
    public const int Laps = 4;
    public static readonly string[] Canonical = { "Reaction", "Start", "Technique", "TrackReading", "Attack", "Defense", "PairRiding", "Strength" };
    public static readonly string[] Legacy = { "Start", "Speed", "SlideControl" };
    public static WorldParameters Parameters(bool tactics = true) => new() {
        EnableTacticalRacing = tactics, EnableSideBySideContests = tactics, EnableTacticalQualityScoring = false, SolverSubsteps = 4 };

    public static RiderProfile Profile(int id, RiderAbilities? abilities = null, RiderSkills? legacy = null, float combativeness = .5f)
        => RiderProfile.CreateCanonical(id, $"R{id}", new(abilities ?? RiderAbilities.Balanced,
            new(70), new(combativeness, PreferredLine.Neutral, .5f)), legacy ?? RiderSkills.Balanced, RiderStyle.Balanced);

    // Frozen before the smoke run. Canonical order is the published eight-ability order.
    public static RiderProfile[] Profiles(bool varied) => varied ? new[] {
        Profile(1, new(80, 80, 50, 50, 50, 45, 50, 55), new(80, 50, 50, 50, 50, 50)),
        Profile(2, new(50, 50, 85, 70, 55, 55, 50, 60), new(50, 55, 80, 50, 50, 50)),
        Profile(3, new(55, 55, 60, 50, 85, 45, 50, 70), new(55, 65, 60, 50, 50, 50), .8f),
        Profile(4, new(60, 55, 70, 65, 45, 85, 50, 65), new(55, 50, 70, 50, 50, 50), .3f)
    } : Enumerable.Range(1, 4).Select(id => Profile(id)).ToArray();

    public static Scenario Heat(bool varied, int rotation)
    {
        if (rotation is < 0 or > 3) throw new ArgumentOutOfRangeException(nameof(rotation));
        var gates = Scenarios.A(4).Riders.Select(r => r.OffsetMeters).ToArray();
        return new($"A{(varied ? 2 : 1)}-rotation-{rotation}", Profiles(varied).Select((p, i) =>
            new RiderSpec(p, BikeSetup.Neutral, 0, gates[(i + rotation) % 4], 0, gates[(i + rotation) % 4])).ToArray(), MaximumSeconds, Laps);
    }

    public static Scenario Solo(string family, string attribute, int level)
    {
        if (level is not (20 or 50 or 80)) throw new ArgumentOutOfRangeException(nameof(level));
        var a = Enumerable.Repeat(50, 8).ToArray(); var l = Enumerable.Repeat(50f, 6).ToArray();
        var index = Array.IndexOf(family == "canonical" ? Canonical : Legacy, attribute);
        if (index < 0 || family is not ("canonical" or "legacy")) throw new ArgumentException("Unknown sensitivity input.");
        if (family == "canonical") a[index] = level; else l[index] = level;
        var profile = Profile(1, new(a[0], a[1], a[2], a[3], a[4], a[5], a[6], a[7]), new(l[0], l[1], l[2], l[3], l[4], l[5]));
        var rider = Heat(false, 0).Riders[0] with { Profile = profile, TrafficEnabled = false };
        return new($"{family}-{attribute}-{level}", new[] { rider }, MaximumSeconds, Laps);
    }

    public static string Status(RunResult result) => result.Riders.Any(v => v.Activity == RiderActivity.Crashed)
        ? "Interrupted-Crash" : result.Completed ? "Completed" : "TimeLimit";

    public static AuditRun Execute(Scenario scenario, bool tactics = true, string? trajectory = null, bool reverse = false)
    {
        using var process = Process.GetCurrentProcess();
        var allocation = GC.GetAllocatedBytesForCurrentThread(); var cpu = process.TotalProcessorTime;
        var start = Stopwatch.GetTimestamp();
        HeatMeasurements? measurements = null;
        var consequences = new List<ConsequenceObservation>();
        using var world = new WorldRunner(reverse ? scenario.Riders.Reverse() : scenario.Riders, parameters: Parameters(tactics),
            diagnostics: new() { Force = f => measurements!.Force(f), Consequence = consequences.Add });
        using var collector = new HeatMeasurements(world.Frame, world.Snapshot, trajectory);
        measurements = collector;
        var result = world.Run(scenario.Name, StepSeconds, scenario.DurationSeconds, scenario.Laps, collector.Accept, stopOnCrash: true);
        var elapsed = Stopwatch.GetElapsedTime(start).TotalMilliseconds;
        var allocated = GC.GetAllocatedBytesForCurrentThread() - allocation;
        var cpuMs = (process.TotalProcessorTime - cpu).TotalMilliseconds;
        start = Stopwatch.GetTimestamp();
        var metrics = collector.Finish(result);
        var reportMs = Stopwatch.GetElapsedTime(start).TotalMilliseconds;
        return new(scenario.Name, Status(result), Parameters(tactics), scenario.Riders.Select(s => new AuditInput(
            s.Profile.Id, Gate(s.OffsetMeters), s.Profile.Gameplay, s.Profile.Skills, s.Profile.Style, s.Setup,
            s.ReferenceMeters, s.OffsetMeters, s.TargetOffsetMeters, s.SpeedMetersPerSecond, s.TrafficEnabled)).ToArray(),
            result, metrics, collector.Passes.Events.ToArray(), collector.OrderChanges.ToArray(), collector.ControlSamples.ToArray(), collector.Anomalies.ToArray(),
            consequences.ToArray(), Status(result) == "Interrupted-Crash" ? world.Snapshot : null,
            new(elapsed, cpuMs, allocated, collector.ObserverMilliseconds, collector.ForceObserverMilliseconds, reportMs));
    }
    private static string Gate(float offset)
    {
        var gates = Scenarios.A(4).Riders;
        var index = Array.FindIndex(gates, r => r.OffsetMeters == offset);
        return index < 0 ? "non-gate" : ((StartingGate)index).ToString();
    }
    private static void Save(string path, object value) => File.WriteAllText(path, JsonSerializer.Serialize(value, Audit.Json) + "\n");
    public static void Run(string directory, string stage)
    {
        Directory.CreateDirectory(directory);
        if (stage == "smoke")
        {
            var frame = new TrackFrame(CoreSim.Analysis.MatchedVenueProfiles.CreateMotoarenaStandingStartTrack());
            Save(Path.Combine(directory, "configuration.json"), new { Baseline, StepSeconds, MaximumSeconds, Laps,
                StopOnCrash = true, Main = Parameters(), Solo = Parameters(false), frame.LapMeters, frame.Track.Geometry,
                Surface = TrackSurfaceState.Default, A1 = Heat(false, 0).Riders.Select(r => new {r.Profile.Id, r.Profile.Gameplay, Legacy = r.Profile.Skills, r.Setup}),
                A2 = Heat(true, 0).Riders.Select(r => new {r.Profile.Id, r.Profile.Gameplay, Legacy = r.Profile.Skills, r.Setup}),
                Canonical, Legacy, Levels = new[] {20, 50, 80}, Runtime = System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription,
                Build = System.Reflection.CustomAttributeExtensions.GetCustomAttribute<System.Reflection.AssemblyConfigurationAttribute>(typeof(WorldRunner).Assembly)?.Configuration,
                OS = System.Runtime.InteropServices.RuntimeInformation.OSDescription, CpuCount = Environment.ProcessorCount,
                Stopwatch.Frequency, TrajectoryIntervalSeconds = .1 });
            foreach (var s in new[] { Solo("canonical", "Reaction", 50), Heat(false, 0) })
            {
                var r = Execute(s, s.Riders.Length > 1); Save(Path.Combine(directory, "smoke-" + s.Name + ".json"), r);
                Console.WriteLine($"smoke {r.Name}: {r.Status} {r.Result.TimeSeconds:F3}s");
                if (r.Status != "Completed") throw new InvalidOperationException("Stage 1 did not complete; inspect retained smoke evidence before continuing.");
            }
            return;
        }
        if (stage == "heats")
        {
            foreach (var varied in new[] {false, true}) for (var rotation = 0; rotation < 4; rotation++)
            {
                var s = Heat(varied, rotation);
                var r = Execute(s, trajectory: rotation == 0 ? Path.Combine(directory, s.Name + "-trajectory.csv") : null);
                Save(Path.Combine(directory, s.Name + ".json"), r); Console.WriteLine($"{s.Name}: {r.Status}, passes={r.Passes.Length}, contacts={r.Result.Work.Episodes}");
            }
            // Exactly two OFF controls. Legacy reactive safety stays present, as in #91.
            foreach (var varied in new[] {false, true}) {
                var s = Heat(varied, 0) with {Name = $"A{(varied ? 2 : 1)}-control-OFF"};
                var r = Execute(s, false); Save(Path.Combine(directory, s.Name + ".json"), r);
                Console.WriteLine($"{s.Name}: {r.Status}");
            }
            return;
        }
        if (stage == "solo")
        {
            foreach (var family in new[] {"canonical", "legacy"}) foreach (var attribute in family == "canonical" ? Canonical : Legacy)
            foreach (var level in new[] {20, 50, 80}) {
                var s = Solo(family, attribute, level); var r = Execute(s, false);
                Save(Path.Combine(directory, s.Name + ".json"), r);
                Console.WriteLine($"{s.Name}: {r.Status}, first={r.Riders[0].LapSeconds.FirstOrDefault():F6}, best={r.Riders[0].BestFlyingSeconds:F6}");
            }
            return;
        }
        if (stage == "repeat")
        {
            var rows = new List<object>();
            foreach (var s in new[] { Heat(false, 0), Heat(true, 0), Heat(true, 1), Solo("canonical", "Technique", 80), Solo("legacy", "SlideControl", 80) })
            {
                var original = JsonSerializer.Deserialize<AuditRun>(File.ReadAllText(Path.Combine(directory, s.Name + ".json")), Audit.Json)!;
                var repeat = Execute(s, s.Riders.Length > 1, reverse: true);
                var hash = original.Result.Fingerprint == repeat.Result.Fingerprint;
                var laps = original.Riders.Zip(repeat.Riders).All(p => p.First.LapSeconds.SequenceEqual(p.Second.LapSeconds));
                var passes = original.Passes.SequenceEqual(repeat.Passes);
                var decisions = original.Result.Decisions.SequenceEqual(repeat.Result.Decisions);
                var contacts = original.Result.Contacts.SequenceEqual(repeat.Result.Contacts);
                rows.Add(new {s.Name, FreshProcess = true, Reverse = true, ExactHash = hash, ExactLaps = laps,
                    ExactPasses = passes, ExactDecisions = decisions, ExactContacts = contacts, repeat.Result.Fingerprint});
                if (!(hash && laps && passes && decisions && contacts)) throw new InvalidOperationException("Repeat mismatch: " + s.Name);
            }
            Save(Path.Combine(directory, "repeats.json"), rows); Console.WriteLine("Five independent reverse-input repeats exact."); return;
        }
        if (stage == "benchmark") { Benchmark(directory); return; }
        throw new ArgumentException("Use smoke, heats, solo, repeat or benchmark.");
    }

    private static void Benchmark(string directory)
    {
        var scenario = Heat(false, 0); var rows = new List<object>();
        foreach (var observed in new[] {false, true})
        {
            // One warm-up and three measured repeats of an existing configuration, no extra scenarios.
            for (var n = -1; n < 3; n++)
            {
                using var process = Process.GetCurrentProcess();
                var allocation = GC.GetAllocatedBytesForCurrentThread(); var cpu = process.TotalProcessorTime;
                var start = Stopwatch.GetTimestamp(); RunResult result; double callbackMs = 0;
                if (observed) {
                    var r = Execute(scenario); result = r.Result;
                    callbackMs = r.Performance.AcceptedObserverMilliseconds + r.Performance.ForceObserverMilliseconds;
                } else {
                    using var w = new WorldRunner(scenario.Riders, parameters: Parameters());
                    result = w.Run(scenario.Name, StepSeconds, MaximumSeconds, Laps, stopOnCrash: true);
                }
                var elapsed = Stopwatch.GetElapsedTime(start).TotalMilliseconds;
                if (n >= 0) rows.Add(new {Observed = observed, Sample = n, ElapsedMilliseconds = elapsed,
                    CpuMilliseconds = (process.TotalProcessorTime - cpu).TotalMilliseconds,
                    AllocatedBytes = GC.GetAllocatedBytesForCurrentThread() - allocation, CallbackMilliseconds = callbackMs,
                    result.TimeSeconds, result.Fingerprint, result.Work});
            }
        }
        Save(Path.Combine(directory, "performance.json"), rows); Console.WriteLine("A1 benchmark: bare and observed, 3 measured repeats each.");
    }
}

public sealed record AuditInput(int RiderId, string Gate, RiderGameplayProfile Canonical, RiderSkills Legacy,
    RiderStyle LegacyStyle, BikeSetup Setup, double ReferenceMeters, float OffsetMeters, float TargetOffsetMeters,
    float SpeedMetersPerSecond, bool TrafficEnabled);
public sealed record AuditPerformance(double ElapsedMilliseconds, double CpuMilliseconds, long AllocatedBytes,
    double AcceptedObserverMilliseconds, double ForceObserverMilliseconds, double SummaryMilliseconds);
public sealed record AuditRun(string Name, string Status, WorldParameters Parameters, AuditInput[] Inputs,
    RunResult Result, RiderMeasurement[] Riders, ConfirmedPass[] Passes, OrderChange[] OrderChanges,
    ControlSample[] ControlSamples, AuditAnomaly[] Anomalies, ConsequenceObservation[] Consequences, WorldSnapshot? CrashSnapshot, AuditPerformance Performance);
public sealed record OrderChange(double TimeSeconds, int[] ReferenceOrder);
public sealed record ControlSample(double ActivationBoundarySeconds, WorldSnapshot AfterAcceptedStep);
public sealed record AuditAnomaly(double TimeSeconds, int RiderId, string Kind, double Value, RiderView State);
public sealed record ConfirmedPass(int RiderId, int OpponentId, double ClearSinceSeconds, double ConfirmedAtSeconds,
    double HeldDistanceMeters, bool AfterFirstBend);
public sealed record RiderMeasurement(int RiderId, double[] CrossingSeconds, double[] LapSeconds, double? BestFlyingSeconds,
    double? MovementSeconds, double? FirstDriveForceSeconds, double MaximumLaunchTangentialForceNewtons, double? First35MetersSeconds, double? FirstBendExitSeconds,
    double AverageSpeedMetersPerSecond, double TypicalSpeedMetersPerSecond, double MaximumSpeedMetersPerSecond,
    double StraightAverageSpeedMetersPerSecond, double CornerAverageSpeedMetersPerSecond,
    double MinimumTrackClearanceMeters, double MinimumPeerGapMeters, double MinimumOffsetMeters, double MaximumOffsetMeters,
    double MaximumYawErrorRadians, double MaximumLateralDemandRatio, double MaximumAppliedGripRatio,
    double MinimumTangentialForceNewtons, double MaximumTangentialForceNewtons, double DriveOffSeconds,
    double TargetTravelMeters, int TargetReversals, double MaximumOneSecondSpeedLossMetersPerSecond,
    int Decisions, int Activated, int Attacks, int Defenses, int SideManeuvers, int ActualControlChanges, int Passes, int Contacts);

/// <summary>Linear interpolation between consecutive accepted states only; never feeds control.</summary>
public sealed class LapCrossings
{
    public List<double> Times { get; } = new();
    public void Accept(double beforeTime, double beforeMeters, double time, double meters, double lapMeters, int requiredLaps)
    {
        if (time <= beforeTime || meters <= beforeMeters) return;
        while (Times.Count < requiredLaps && meters >= (Times.Count + 1) * lapMeters) {
            var threshold = (Times.Count + 1) * lapMeters;
            if (beforeMeters > threshold) throw new InvalidOperationException("Missing accepted crossing interval.");
            Times.Add(beforeTime + (time - beforeTime) * (threshold - beforeMeters) / (meters - beforeMeters));
        }
    }
}

/// <summary>Pair history: established whole-bike leader, actual alongside, then sustained whole-bike clearance.</summary>
public sealed class FullHeatPassObserver
{
    private sealed class Pair { public int Leader, Candidate; public double Since, Distance; public bool Alongside; }
    private readonly Dictionary<(int, int), Pair> pairs = new();
    public List<ConfirmedPass> Events { get; } = new();
    public void Accept(WorldSnapshot snapshot)
    {
        for (var a = 0; a < snapshot.Riders.Count; a++) for (var b = a + 1; b < snapshot.Riders.Count; b++)
        {
            var x = snapshot.Riders[a]; var y = snapshot.Riders[b];
            if (x.Activity != RiderActivity.Racing || y.Activity != RiderActivity.Racing) continue;
            var key = (Math.Min(x.Id, y.Id), Math.Max(x.Id, y.Id));
            if (!pairs.TryGetValue(key, out var pair)) pairs[key] = pair = new();
            var lead = x.TotalReferenceMeters - y.TotalReferenceMeters;
            var nearby = Math.Abs(lead) < 20 && Vector2.Distance(x.Position, y.Position) < 20
                && Vector2.Dot(x.Road.Tangent, y.Road.Tangent) > .9;
            var leader = 0;
            if (nearby) {
                if (lead > 0 && TacticalAudit.ClearGap(x, y) >= .15) leader = x.Id;
                else if (lead < 0 && TacticalAudit.ClearGap(y, x) >= .15) leader = y.Id;
                else if (pair.Leader != 0) pair.Alongside = true;
            } else if (Math.Abs(lead) > 3) leader = lead > 0 ? x.Id : y.Id;
            if (leader == 0) { pair.Candidate = 0; continue; }
            if (leader == pair.Leader) { pair.Candidate = 0; pair.Alongside = false; continue; }
            var rider = leader == x.Id ? x : y;
            if (pair.Candidate != leader) { pair.Candidate = leader; pair.Since = snapshot.TimeSeconds; pair.Distance = rider.DistanceMeters; }
            if (snapshot.TimeSeconds - pair.Since < 1 || rider.DistanceMeters - pair.Distance < 10) continue;
            if (pair.Leader != 0 && pair.Alongside && nearby) Events.Add(new(leader, pair.Leader, pair.Since,
                snapshot.TimeSeconds, rider.DistanceMeters - pair.Distance,
                x.Context == RacingContext.NormalRacing && y.Context == RacingContext.NormalRacing));
            // Initial separation from gates establishes order, never counts as overtaking.
            pair.Leader = leader; pair.Candidate = 0; pair.Alongside = false;
        }
    }
}

public sealed class HeatMeasurements : IDisposable
{
    private sealed class Rider
    {
        public readonly LapCrossings Laps = new();
        public readonly double[] SpeedSeconds = new double[400];
        public readonly Queue<(double Time, double Speed)> History = new();
        public double? Movement, FirstDrive, First35, BendExit;
        public double MaxLaunchForce;
        public double Duration, SpeedIntegral, StraightDuration, StraightSpeed, CornerDuration, CornerSpeed, MaximumSpeed,
            MinClearance = double.PositiveInfinity, MinPeerGap = double.PositiveInfinity, MinOffset = double.PositiveInfinity,
            MaxOffset = double.NegativeInfinity, YawError, DemandRatio, AppliedRatio, MinTangential = double.PositiveInfinity,
            MaxTangential = double.NegativeInfinity, DriveOff, TargetTravel, SpeedLoss;
        public int Reversals, LastDirection, Changes;
    }
    private readonly TrackFrame frame;
    private WorldSnapshot previous;
    private readonly Dictionary<int, Rider> riders;
    private readonly StreamWriter? trajectory;
    private double nextTrajectory;
    private int[] lastOrder;
    public double ObserverMilliseconds { get; private set; }
    public double ForceObserverMilliseconds { get; private set; }
    public FullHeatPassObserver Passes { get; } = new();
    public List<OrderChange> OrderChanges { get; } = new();
    public List<ControlSample> ControlSamples { get; } = new();
    public List<AuditAnomaly> Anomalies { get; } = new();
    public HeatMeasurements(TrackFrame frame, WorldSnapshot initial, string? trajectoryPath = null)
    {
        this.frame = frame; previous = initial; riders = initial.Riders.ToDictionary(r => r.Id, _ => new Rider());
        lastOrder = initial.Riders.OrderByDescending(v => v.TotalReferenceMeters).ThenBy(v => v.Id).Select(v => v.Id).ToArray();
        if (trajectoryPath is not null) {
            trajectory = File.CreateText(trajectoryPath);
            trajectory.WriteLine("time_s,rider,x_m,y_m,vx_mps,vy_mps,speed_mps,reference_m,offset_m,target_m,drive,heading_rad,activity");
        }
    }
    public void Force(ForceObservation f)
    {
        var watch = Stopwatch.GetTimestamp();
        if (f.Rider.Activity != RiderActivity.Finished) {
            var r = riders[f.Rider.Id];
            if (f.Phase == "racing" && f.TangentialNewtons > 0) r.FirstDrive ??= f.TimeSeconds;
            if (f.Rider.Context == RacingContext.StartStraight) r.MaxLaunchForce = Math.Max(r.MaxLaunchForce, f.TangentialNewtons);
            r.YawError = Math.Max(r.YawError, Math.Abs(BikeAngles.Wrap(f.Rider.Road.Heading
                + (f.Rider.Road.IsTurn ? Math.PI / 15 : 0) - f.Rider.Heading)));
            if (f.GripBudgetNewtons is > 0) {
                r.DemandRatio = Math.Max(r.DemandRatio, Math.Abs(f.DesiredInwardNewtons) / f.GripBudgetNewtons.Value);
                r.AppliedRatio = Math.Max(r.AppliedRatio, f.ForceNewtons.Length() / f.GripBudgetNewtons.Value);
            }
            r.MinTangential = Math.Min(r.MinTangential, f.TangentialNewtons);
            r.MaxTangential = Math.Max(r.MaxTangential, f.TangentialNewtons);
        }
        ForceObserverMilliseconds += Stopwatch.GetElapsedTime(watch).TotalMilliseconds;
    }
    public void Accept(WorldSnapshot s)
    {
        var watch = Stopwatch.GetTimestamp(); var dt = s.TimeSeconds - previous.TimeSeconds; var controlsChanged = false;
        for (var i = 0; i < s.Riders.Count; i++)
        {
            var v = s.Riders[i]; var old = previous.Riders[i]; var r = riders[v.Id];
            if (old.Activity == RiderActivity.Finished) continue;
            r.Laps.Accept(previous.TimeSeconds, old.TotalReferenceMeters, s.TimeSeconds, v.TotalReferenceMeters, frame.LapMeters, FullHeatSoloAudit.Laps);
            var measuredDt = r.Laps.Times.Count == FullHeatSoloAudit.Laps ? Math.Max(0, r.Laps.Times[^1] - previous.TimeSeconds) : dt;
            var fraction = dt > 0 ? measuredDt / dt : 0;
            var endSpeed = old.Speed + (v.Speed - old.Speed) * fraction;
            var meanSpeed = (old.Speed + endSpeed) / 2;
            r.Duration += measuredDt; r.SpeedIntegral += measuredDt * meanSpeed;
            if (old.Road.IsTurn) { r.CornerDuration += measuredDt; r.CornerSpeed += meanSpeed * measuredDt; }
            else { r.StraightDuration += measuredDt; r.StraightSpeed += meanSpeed * measuredDt; }
            r.SpeedSeconds[Math.Clamp((int)(meanSpeed * 4), 0, 399)] += measuredDt;
            r.MaximumSpeed = Math.Max(r.MaximumSpeed, Math.Max(old.Speed, endSpeed));
            if (r.Movement is null && v.DistanceMeters >= .001) r.Movement = s.TimeSeconds;
            if (r.First35 is null && v.TotalReferenceMeters >= 35 && old.TotalReferenceMeters < 35)
                r.First35 = previous.TimeSeconds + dt * (35 - old.TotalReferenceMeters) / (v.TotalReferenceMeters - old.TotalReferenceMeters);
            if (r.BendExit is null && old.Context == RacingContext.FirstBend && v.Context == RacingContext.NormalRacing) r.BendExit = s.TimeSeconds;
            var clearance = Clearance(v, frame);
            r.MinClearance = Math.Min(r.MinClearance, clearance);
            if (clearance < 0 && !Anomalies.Any(a => a.RiderId == v.Id && a.Kind == "OutsideTrack"))
                Anomalies.Add(new(s.TimeSeconds, v.Id, "OutsideTrack", clearance, v));
            r.MinOffset = Math.Min(r.MinOffset, v.Road.OffsetMeters); r.MaxOffset = Math.Max(r.MaxOffset, v.Road.OffsetMeters);
            r.DriveOff += old.Drive == 0 ? measuredDt : 0;
            var targetDelta = v.TargetOffsetMeters - old.TargetOffsetMeters;
            if (targetDelta != 0 || v.Drive != old.Drive) { r.Changes++; controlsChanged = true; }
            r.TargetTravel += Math.Abs(targetDelta);
            if (Math.Abs(targetDelta) > .05) {
                var direction = Math.Sign(targetDelta); if (r.LastDirection != 0 && direction != r.LastDirection) r.Reversals++;
                r.LastDirection = direction;
            }
            foreach (var peer in s.Riders) if (peer.Id != v.Id) {
                r.MinPeerGap = Math.Min(r.MinPeerGap, MechanicalSeparation.Between(Pose(v, s.TimeSeconds), Pose(peer, s.TimeSeconds)).SignedMeters);
            }
            r.History.Enqueue((s.TimeSeconds, v.Speed));
            while (r.History.Count > 0 && r.History.Peek().Time < s.TimeSeconds - 1) r.History.Dequeue();
            if (r.History.Count > 0) r.SpeedLoss = Math.Max(r.SpeedLoss, r.History.Peek().Speed - v.Speed);
            if (trajectory is not null && s.TimeSeconds + 1e-9 >= nextTrajectory)
                trajectory.WriteLine(FormattableString.Invariant($"{s.TimeSeconds:R},{v.Id},{v.Position.X:R},{v.Position.Y:R},{v.Velocity.X:R},{v.Velocity.Y:R},{v.Speed:R},{v.TotalReferenceMeters:R},{v.Road.OffsetMeters:R},{v.TargetOffsetMeters:R},{v.Drive:R},{v.Heading:R},{v.Activity}"));
        }
        if (trajectory is not null && s.TimeSeconds + 1e-9 >= nextTrajectory) nextTrajectory = s.TimeSeconds + .1;
        if (controlsChanged) ControlSamples.Add(new(previous.TimeSeconds, s));
        // A finished body continues physically until the last rider finishes. Freeze its sporting order by crossing time.
        var order = s.Riders.OrderBy(v => riders[v.Id].Laps.Times.Count == FullHeatSoloAudit.Laps ? 0 : 1)
            .ThenBy(v => riders[v.Id].Laps.Times.Count == FullHeatSoloAudit.Laps ? riders[v.Id].Laps.Times[^1] : double.PositiveInfinity)
            .ThenByDescending(v => v.TotalReferenceMeters).ThenBy(v => v.Id).Select(v => v.Id).ToArray();
        if (!order.SequenceEqual(lastOrder)) { OrderChanges.Add(new(s.TimeSeconds, order)); lastOrder = order; }
        Passes.Accept(s); previous = s;
        ObserverMilliseconds += Stopwatch.GetElapsedTime(watch).TotalMilliseconds;
    }
    private static PhysicalBikePose Pose(RiderView v, double time) => new(v.Id, "shared-world",
        new(v.Position.X, v.Position.Y), new(time, v.Road.Heading, v.Heading), SpeedwayBikeDimensions.Reference);
    public static double Clearance(RiderView v, TrackFrame frame)
    {
        var extent = TacticalAudit.Extent(v, v.Road.Outward);
        var center = Vector2.Dot(v.Position, v.Road.Outward);
        var width = LaneModel.UsableRacingWidthMeters(frame.Track.Segments[v.Road.Segment].Type, frame.Track.Geometry);
        return Math.Min(v.Road.OffsetMeters + extent.Rear - center + 1, width + 1 - (v.Road.OffsetMeters + extent.Front - center));
    }
    public RiderMeasurement[] Finish(RunResult result) => result.Riders.Select(v => {
        var r = riders[v.Id]; var times = r.Laps.Times.ToArray();
        var laps = times.Select((t, i) => t - (i == 0 ? 0 : times[i - 1])).ToArray();
        if (result.FinishTimes.TryGetValue(v.Id, out var finish) && (times.Length != 4 || Math.Abs(times[^1] - finish) > 1e-8))
            throw new InvalidOperationException("Audit/WorldRunner finish mismatch.");
        double cumulative = 0, median = 0;
        for (var i = 0; i < r.SpeedSeconds.Length; i++) { cumulative += r.SpeedSeconds[i]; if (cumulative >= r.Duration / 2) { median = (i + .5) / 4; break; } }
        var decisions = result.Decisions.Where(d => d.RiderId == v.Id).ToArray();
        var active = decisions.Where(d => d.ActualActivation is not null).ToArray();
        return new RiderMeasurement(v.Id, times, laps, laps.Length > 1 ? laps.Skip(1).Min() : null, r.Movement, r.FirstDrive, r.MaxLaunchForce, r.First35, r.BendExit,
            r.SpeedIntegral / Math.Max(r.Duration, 1e-9), median, r.MaximumSpeed,
            r.StraightSpeed / Math.Max(r.StraightDuration, 1e-9), r.CornerSpeed / Math.Max(r.CornerDuration, 1e-9),
            r.MinClearance, r.MinPeerGap, r.MinOffset, r.MaxOffset, r.YawError, r.DemandRatio, r.AppliedRatio,
            r.MinTangential, r.MaxTangential, r.DriveOff, r.TargetTravel, r.Reversals, r.SpeedLoss,
            decisions.Length, active.Length, active.Count(d => d.Intent is RacingIntent.AttackInside or RacingIntent.AttackOutside),
            active.Count(d => d.Intent == RacingIntent.DefendLine), active.Count(d => d.Intent is RacingIntent.ContestInside or RacingIntent.ContestOutside or RacingIntent.ContestHold),
            r.Changes, Passes.Events.Count(p => p.RiderId == v.Id), result.Contacts.Count(c => c.Kind == "begin" && (c.RiderA == v.Id || c.RiderB == v.Id)));
    }).ToArray();
    public void Dispose() => trajectory?.Dispose();
}
