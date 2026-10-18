using System.Globalization;
using System.Diagnostics;
using System.Numerics;
using System.Text.Json;
using CoreSim.PhysicalSpace;

namespace SharedWorldSpeedway;

public sealed record TacticalEvidence(RunResult Result, double? MovementAt, double? AlongsideAt,
    double? ClearAt, double? HeldAt, double? HeldSince, double? HeldDistanceMeters,
    double FinalLeadMeters, bool LeaderKeptDrive, double MaximumGripRatio, IReadOnlyList<WorldSnapshot> Samples);

public static class TacticalAudit
{
    // Independent projection of actual chassis and handlebar capsules, not intent/order.
    public static (double Rear, double Front) Extent(RiderView v, Vector2 axis)
    {
        var bike = BikeFootprint.Create(new(v.Position.X, v.Position.Y), v.Heading, SpeedwayBikeDimensions.Reference);
        double rear = double.PositiveInfinity, front = double.NegativeInfinity;
        void Include(MechanicalCapsule c)
        {
            var direction = new MeterPoint(axis.X, axis.Y);
            var a = MeterPoint.Dot(c.Start, direction); var b = MeterPoint.Dot(c.End, direction);
            rear = Math.Min(rear, Math.Min(a, b) - c.RadiusMeters);
            front = Math.Max(front, Math.Max(a, b) + c.RadiusMeters);
        }
        Include(bike.Chassis); Include(bike.Handlebar); return (rear, front);
    }
    public static double ClearGap(RiderView own, RiderView other)
        => Extent(own, own.Road.Tangent).Rear - Extent(other, own.Road.Tangent).Front;
    public static TacticalEvidence Execute(Scenario scenario, double step, bool enabled, bool reverse = false)
    {
        double grip = 0;
        using var world = new WorldRunner(reverse ? scenario.Riders.Reverse() : scenario.Riders,
            parameters: new() {EnableTacticalRacing = enabled}, diagnostics: new() {Force = f => {
                if (f.GripBudgetNewtons is > 0) grip = Math.Max(grip, f.ForceNewtons.Length() / f.GripBudgetNewtons.Value);
            }});
        var samples = new List<WorldSnapshot> {world.Snapshot};
        var result = world.Run(scenario.Name, step, scenario.DurationSeconds, scenario.Laps, samples.Add, stopOnCrash: true);
        double? movement = null, alongside = null, clear = null, held = null, heldSince = null, heldDistance = null;
        double? candidate = null, distanceAtCandidate = null;
        var origin = samples[0].Riders[0].Road.OffsetMeters;
        // Gate starts and parallel riding are not passes from an established rear position.
        var startedBehind = samples[0].Riders.Count >= 2 && Extent(samples[0].Riders[0], samples[0].Riders[0].Road.Tangent).Front
            < Extent(samples[0].Riders[1], samples[0].Riders[0].Road.Tangent).Rear;
        foreach (var s in samples)
        {
            if (s.Riders.Count < 2) continue;
            var own = s.Riders[0]; var other = s.Riders[1];
            if (movement is null && Math.Abs(own.Road.OffsetMeters - origin) >= .05) movement = s.TimeSeconds;
            if (alongside is null && Extent(own, own.Road.Tangent).Front >= Extent(other, own.Road.Tangent).Rear)
                alongside = s.TimeSeconds;
            if (startedBehind && ClearGap(own, other) >= .15)
            {
                clear ??= s.TimeSeconds;
                if (candidate is null) { candidate = s.TimeSeconds; distanceAtCandidate = own.DistanceMeters; }
                if (held is null && s.TimeSeconds - candidate >= 1 && own.DistanceMeters - distanceAtCandidate >= 10)
                { held = s.TimeSeconds; heldSince = candidate; heldDistance = own.DistanceMeters - distanceAtCandidate; }
            }
            else { candidate = null; distanceAtCandidate = null; }
        }
        var final = result.Riders;
        return new(result, movement, alongside, clear, held, heldSince, heldDistance,
            final.Count < 2 ? 0 : Vector2.Dot(final[0].Position - final[1].Position, final[0].Road.Tangent),
            samples.All(s => s.Riders.Count < 2 || s.Riders[1].Drive == 1), grip, samples);
    }
    public static void Run(string directory, IReadOnlyList<Scenario> scenarios)
    {
        Directory.CreateDirectory(directory); var summaries = new List<object>();
        using var events = File.CreateText(Path.Combine(directory, "events.csv"));
        events.WriteLine("scenario,tactics,step_s,time_s,rider,opponent,situation,decision,target_m,drive,activation_s,actual_offset_m,progress_m,speed_mps,physical_clear_gap_m");
        foreach (var scenario in scenarios) foreach (var enabled in new[] {false, true})
        {
            var evidence = Execute(scenario, .01, enabled); var r = evidence.Result;
            summaries.Add(new {r.Name, Enabled = enabled, r.StepSeconds, r.TimeSeconds, r.Work, r.Riders,
                r.Decisions, r.Contacts, r.Fingerprint, evidence.MovementAt, evidence.AlongsideAt,
                evidence.ClearAt, evidence.HeldAt, evidence.HeldSince, evidence.HeldDistanceMeters,
                evidence.FinalLeadMeters, evidence.LeaderKeptDrive, evidence.MaximumGripRatio});
            WorldSnapshot At(double time) => evidence.Samples.Last(s => s.TimeSeconds <= time + 1e-9);
            var rows = new List<object?[]>();
            void Event(double time, int rider, int opponent, string situation, string decision,
                float target, float drive, double? activation)
            {
                var s = At(time); var own = s.Riders.Single(v => v.Id == rider);
                var peer = s.Riders.FirstOrDefault(v => v.Id == opponent);
                rows.Add(new object?[] {r.Name, enabled, .01, time, rider, opponent, situation, decision, target, drive,
                    activation, own.Road.OffsetMeters, own.TotalReferenceMeters, own.Speed,
                    peer.Id == 0 ? null : ClearGap(own, peer)});
            }
            foreach (var v in At(.1).Riders)
            {
                var peer = At(.1).Riders.Where(p => p.Id != v.Id).OrderBy(p => Vector2.DistanceSquared(p.Position, v.Position)).FirstOrDefault();
                Event(.1, v.Id, peer.Id, "SharedObservation", "Observe", v.TargetOffsetMeters, v.Drive, null);
                var initial = evidence.Samples[0].Riders.Single(p => p.Id == v.Id);
                var movement = evidence.Samples.FirstOrDefault(s => Math.Abs(s.Riders.Single(p => p.Id == v.Id).Road.OffsetMeters - initial.Road.OffsetMeters) >= .05);
                if (movement is not null) Event(movement.TimeSeconds, v.Id, 0, "LateralMotion", "Measured",
                    movement.Riders.Single(p => p.Id == v.Id).TargetOffsetMeters, movement.Riders.Single(p => p.Id == v.Id).Drive, null);
                var bend = evidence.Samples.FirstOrDefault(s => s.Riders.Single(p => p.Id == v.Id).Road.IsTurn);
                if (bend is not null && !initial.Road.IsTurn) {
                    var actual = bend.Riders.Single(p => p.Id == v.Id);
                    Event(bend.TimeSeconds, v.Id, 0, "FirstBendEntry", "Measured", actual.TargetOffsetMeters, actual.Drive, null);
                }
            }
            foreach (var d in r.Decisions)
            {
                Event(d.ObservedAt, d.RiderId, d.OpponentId, d.Reason, d.Action.ToString(), d.TargetOffsetMeters, d.Drive, d.RequestedActivation);
                if (d.CancelledAt is { } cancelled)
                    Event(cancelled, d.RiderId, d.OpponentId, "CancelledBeforeActivation", d.Action.ToString(), d.TargetOffsetMeters, d.Drive, null);
                if (d.ActualActivation is { } activated)
                    Event(activated, d.RiderId, d.OpponentId, "ControlActivated", d.Action.ToString(), d.TargetOffsetMeters, d.Drive, activated);
            }
            if (r.Riders.Count >= 2)
                foreach (var milestone in new[] {(evidence.AlongsideAt,"ActualAlongside"),
                    (evidence.ClearAt,"EntireBikeAhead"),(evidence.HeldAt,"LeadHeldOneSecondAndTenMeters")})
                    if (milestone.Item1 is { } time)
                    {
                        var own = At(time).Riders[0];
                        Event(time, own.Id, r.Riders[1].Id, milestone.Item2, "Measured", own.TargetOffsetMeters, own.Drive, null);
                    }
            foreach (var c in r.Contacts.Where(c => c.Kind == "begin"))
            {
                var own = At(c.TimeSeconds).Riders.Single(v => v.Id == c.RiderA);
                Event(c.TimeSeconds, c.RiderA, c.RiderB, "PhysicalContactRatio=" + c.SeverityRatioA.ToString("G6", CultureInfo.InvariantCulture),
                    own.Activity.ToString(), own.TargetOffsetMeters, own.Drive, null);
            }
            foreach (var row in rows.OrderBy(row => (double)row[3]!).ThenBy(row => (int)row[4]!)) Row(events, row);
        }
        File.WriteAllText(Path.Combine(directory, "runs.json"), JsonSerializer.Serialize(summaries, Audit.Json));
        Console.WriteLine("Saved bounded actual tactical events to " + Path.GetFullPath(directory));
    }
    public static void Benchmark(string directory)
    {
        Directory.CreateDirectory(directory); var rows = new List<object>();
        var heat = Scenarios.A(4);
        // Real traffic-enabled lone rider: observation path runs but there are no peers.
        var solo = heat with {Name = "solo-full-heat", Riders = new[] {heat.Riders[0] with {TrafficEnabled = true}}};
        var distant = heat with {Name = "four-distant-straight", DurationSeconds = .8, Laps = 0,
            Riders = heat.Riders.Select((r, i) => r with {TrafficEnabled = true, ReferenceMeters = i * 70,
                SpeedMetersPerSecond = 18, OffsetMeters = 4, TargetOffsetMeters = 4}).ToArray()};
        var cases = new[] {(heat, false), (solo, false), (solo, true), (distant, false), (distant, true),
            (TacticalScenarios.A(), true), (TacticalScenarios.E(), false), (TacticalScenarios.E(), true),
            (Scenarios.DenseStart(), false), (Scenarios.DenseStart(), true)};
        foreach (var (scenario, enabled) in cases)
        {
            RunResult Execute() {
                using var world = new WorldRunner(scenario.Riders, parameters: new() {EnableTacticalRacing = enabled});
                return world.Run(scenario.Name, .01, scenario.DurationSeconds, scenario.Laps, stopOnCrash: true);
            }
            Execute(); Execute();
            var elapsed = new List<double>(); double cpu = 0; long allocated = 0; RunResult result = null!;
            using var process = Process.GetCurrentProcess();
            const int samples = 5, batch = 2;
            for (var sample = 0; sample < samples; sample++)
            {
                var allocation = GC.GetAllocatedBytesForCurrentThread(); var startCpu = process.TotalProcessorTime;
                var start = Stopwatch.GetTimestamp();
                for (var n = 0; n < batch; n++) result = Execute();
                elapsed.Add(Stopwatch.GetElapsedTime(start).TotalMilliseconds / batch);
                cpu += (process.TotalProcessorTime - startCpu).TotalMilliseconds;
                allocated += GC.GetAllocatedBytesForCurrentThread() - allocation;
            }
            long actualChanges = 0;
            foreach (var spec in scenario.Riders)
            {
                var target = spec.TargetOffsetMeters; var drive = 1f;
                foreach (var d in result.Decisions.Where(d => d.RiderId == spec.Profile.Id && d.ActualActivation is not null))
                { if (target != d.TargetOffsetMeters || drive != d.Drive) actualChanges++; target = d.TargetOffsetMeters; drive = d.Drive; }
            }
            elapsed.Sort(); rows.Add(new {scenario.Name, Enabled = enabled, StepSeconds = .01,
                ElapsedMilliseconds = elapsed[samples / 2], CpuMilliseconds = cpu / (samples * batch),
                AllocatedBytes = allocated / (samples * batch), Samples = samples, Batch = batch,
                result.TimeSeconds, result.Work, Decisions = result.Decisions.Count, ActualControlChanges = actualChanges, result.Fingerprint});
        }
        File.WriteAllText(Path.Combine(directory, "performance.json"), JsonSerializer.Serialize(rows, Audit.Json));
    }
    private static void Row(TextWriter writer, params object?[] values) => writer.WriteLine(string.Join(",",
        values.Select(v => v is IFormattable f ? f.ToString(null, CultureInfo.InvariantCulture) : v?.ToString() ?? "")));
}
