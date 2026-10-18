using System.Globalization;
using System.Diagnostics;
using System.Numerics;
using System.Text.Json;
using CoreSim.Interactions;
using CoreSim.PhysicalSpace;

namespace SharedWorldSpeedway;

public sealed record SideComparison(double? FirstControlAt, string FirstControlReason, int FirstControlRider,
    bool PrefixExact, double? FirstPositionDifferenceAt, double? FirstLateralDifferenceAt,
    double MaximumPositionDifferenceMeters, double MaximumReferenceDifferenceMeters, double FinalReferenceLeadDifferenceMeters,
    double MaximumFirstBendPositionDifferenceMeters, double MaximumFirstBendReferenceDifferenceMeters,
    int FirstPositionDifferenceRider, int FirstLateralDifferenceRider);
public sealed record ParallelResolution(int RiderA, int RiderB, int Winner, double ClearAt, double HeldAt, double HeldDistanceMeters);

public static class SideBySideAudit
{
    private static readonly SpeedwayBikeDimensions Bike = SpeedwayBikeDimensions.Reference;
    public static SideComparison Compare(Scenario scenario, TacticalEvidence baseline, TacticalEvidence changed)
    {
        DecisionEvent? first = null;
        var targets = scenario.Riders.ToDictionary(r => r.Profile.Id, r => r.TargetOffsetMeters);
        var drives = scenario.Riders.ToDictionary(r => r.Profile.Id, _ => 1f);
        foreach (var d in changed.Result.Decisions.Where(d => d.ActualActivation is not null).OrderBy(d => d.ActualActivation).ThenBy(d => d.RiderId))
        {
            if (targets[d.RiderId] != d.TargetOffsetMeters || drives[d.RiderId] != d.Drive) {first = d; break;}
            targets[d.RiderId] = d.TargetOffsetMeters; drives[d.RiderId] = d.Drive;
        }
        var byTime = baseline.Samples.ToDictionary(s => Math.Round(s.TimeSeconds, 9));
        double maxPosition = 0, maxReference = 0, bendPosition = 0, bendReference = 0;
        double? positionAt = null, lateralAt = null; var prefix = true;
        var positionRider = 0; var lateralRider = 0;
        foreach (var s in changed.Samples)
        {
            if (!byTime.TryGetValue(Math.Round(s.TimeSeconds, 9), out var b)) continue;
            if ((first is null || s.TimeSeconds <= first.ActualActivation) && !s.Riders.SequenceEqual(b.Riders)) prefix = false;
            for (var i = 0; i < s.Riders.Count; i++)
            {
                var a = s.Riders[i]; var peer = b.Riders[i]; var distance = Vector2.Distance(a.Position, peer.Position);
                maxPosition = Math.Max(maxPosition, distance);
                maxReference = Math.Max(maxReference, Math.Abs(a.TotalReferenceMeters - peer.TotalReferenceMeters));
                if (a.Context == RacingContext.FirstBend || peer.Context == RacingContext.FirstBend) {
                    bendPosition = Math.Max(bendPosition, distance);
                    bendReference = Math.Max(bendReference, Math.Abs(a.TotalReferenceMeters - peer.TotalReferenceMeters));
                }
                if (positionAt is null && distance >= .02) {positionAt = s.TimeSeconds; positionRider = a.Id;}
                if (lateralAt is null && Math.Abs(a.Road.OffsetMeters - peer.Road.OffsetMeters) >= .05) {lateralAt = s.TimeSeconds; lateralRider = a.Id;}
            }
        }
        var ar = changed.Result.Riders; var br = baseline.Result.Riders;
        return new(first?.ActualActivation, first?.Reason ?? "", first?.RiderId ?? 0, prefix, positionAt, lateralAt,
            maxPosition, maxReference, (ar[0].TotalReferenceMeters - ar[1].TotalReferenceMeters) - (br[0].TotalReferenceMeters - br[1].TotalReferenceMeters),
            bendPosition, bendReference, positionRider, lateralRider);
    }
    public static IReadOnlyList<ParallelResolution> Resolutions(TacticalEvidence evidence)
    {
        var results = new List<ParallelResolution>();
        for (var a = 0; a < evidence.Result.Riders.Count; a++) for (var b = a + 1; b < evidence.Result.Riders.Count; b++)
        {
            var seen = false; int winner = 0; double since = 0, distance = 0;
            foreach (var s in evidence.Samples)
            {
                var x = s.Riders[a]; var y = s.Riders[b];
                seen |= Vector2.DistanceSquared(x.Position, y.Position) <= 64
                    && Math.Abs(x.TotalReferenceMeters - y.TotalReferenceMeters) <= Bike.OverallMechanicalLengthMeters
                    && TacticalAudit.ClearGap(x, y) < .15 && TacticalAudit.ClearGap(y, x) < .15;
                if (!seen) continue;
                var leader = TacticalAudit.ClearGap(x, y) >= .15 ? x : TacticalAudit.ClearGap(y, x) >= .15 ? y : default;
                if (leader.Id != winner) {winner = leader.Id; since = s.TimeSeconds; distance = leader.DistanceMeters;}
                if (winner != 0 && s.TimeSeconds - since >= 1 && leader.DistanceMeters - distance >= 10)
                {results.Add(new(x.Id, y.Id, winner, since, s.TimeSeconds, leader.DistanceMeters - distance)); break;}
            }
        }
        return results;
    }
    public static void Run(string directory, IReadOnlyList<Scenario> cases)
    {
        Directory.CreateDirectory(directory); var rows = new List<object>();
        using var candidates = File.CreateText(Path.Combine(directory, "candidates.csv"));
        using var events = File.CreateText(Path.Combine(directory, "events.csv"));
        events.WriteLine("scenario,side,time_s,rider,peer,situation,decision,target_m,drive,requested_s,activated_s,offset_m,speed_mps,reference_m,reference_gap_m,own_reference_mps,peer_reference_mps,physical_gap_m,delta_reference_vs_off_m");
        candidates.WriteLine("scenario,time_s,rider,peer,target_m,available,baseline_reference_mps,candidate_reference_mps,own_reference_mps,peer_reference_mps,required_gain_mps");
        foreach (var scenario in cases)
        {
          var off = TacticalAudit.Execute(scenario, .01, true);
          var on = TacticalAudit.Execute(scenario, .01, true, side: true, contestObserver: c =>
                Row(candidates, scenario.Name, c.TimeSeconds, c.RiderId, c.OpponentId, c.TargetMeters, c.Available,
                    c.BaselineReferenceSpeed, c.CandidateReferenceSpeed, c.OwnReferenceSpeed, c.OpponentReferenceSpeed, c.RequiredGain));
          var comparison = Compare(scenario, off, on);
          foreach (var (e, enabled) in new[] {(off, false), (on, true)}) {
            var r = e.Result;
            rows.Add(new {r.Name, Side = enabled, r.TimeSeconds, r.Riders, r.Decisions, r.Contacts, r.Work, r.Fingerprint,
                e.MaximumGripRatio, ReferenceLead = r.Riders[0].TotalReferenceMeters - r.Riders[1].TotalReferenceMeters,
                Comparison = enabled ? comparison : null, Resolutions = Resolutions(e)});
            Events(events, e, off, enabled, enabled ? comparison : null);
          }
        }
        File.WriteAllText(Path.Combine(directory, "runs.json"), JsonSerializer.Serialize(rows, Audit.Json));
        Console.WriteLine("Saved actual side-by-side ON/OFF runs to " + Path.GetFullPath(directory));
    }
    private static void Events(TextWriter writer, TacticalEvidence e, TacticalEvidence baseline, bool enabled, SideComparison? comparison)
    {
        var frame = new TrackFrame(CoreSim.Analysis.MatchedVenueProfiles.CreateMotoarenaStandingStartTrack());
        var rows = new List<object?[]>(); var baselineTimes = baseline.Samples.ToDictionary(s => Math.Round(s.TimeSeconds, 9));
        WorldSnapshot At(double time) => e.Samples.Last(s => s.TimeSeconds <= time + 1e-9);
        void Event(double time, int rider, int peer, string situation, DecisionEvent? command = null)
        {
            var s = At(time); var own = s.Riders.Single(v => v.Id == rider); var other = s.Riders.FirstOrDefault(v => v.Id == peer);
            double? gap = other.Id == 0 ? null : MechanicalSeparation.Between(Pose(own), Pose(other)).SignedMeters;
            double? delta = baselineTimes.TryGetValue(Math.Round(time, 9), out var b)
                ? own.TotalReferenceMeters - b.Riders.Single(v => v.Id == rider).TotalReferenceMeters : null;
            rows.Add(new object?[] {e.Result.Name, enabled, time, rider, peer, situation, command?.Action.ToString() ?? "Measured",
                command?.TargetOffsetMeters ?? own.TargetOffsetMeters, command?.Drive ?? own.Drive, command?.RequestedActivation,
                command?.ActualActivation, own.Road.OffsetMeters, own.Speed, own.TotalReferenceMeters,
                other.Id == 0 ? null : other.TotalReferenceMeters - own.TotalReferenceMeters, frame.ReferenceSpeed(own),
                other.Id == 0 ? null : frame.ReferenceSpeed(other), gap, delta});
        }
        foreach (var v in At(.1).Riders) {
            var peer = At(.1).Riders.Where(p => p.Id != v.Id).OrderBy(p => Vector2.DistanceSquared(v.Position, p.Position)).First();
            Event(.1, v.Id, peer.Id, "SharedObservation");
        }
        foreach (var d in e.Result.Decisions) {
            Event(d.ObservedAt, d.RiderId, d.OpponentId, d.Reason, d);
            if (d.ActualActivation is { } t) Event(t, d.RiderId, d.OpponentId, "ControlActivated", d);
            if (d.CancelledAt is { } c) Event(c, d.RiderId, d.OpponentId, "CancelledBeforeActivation", d);
        }
        if (comparison is not null) {
            if (comparison.FirstPositionDifferenceAt is { } p) Event(p, comparison.FirstPositionDifferenceRider, 0, "ActualDifferenceAtLeast2cm");
            if (comparison.FirstLateralDifferenceAt is { } p2) Event(p2, comparison.FirstLateralDifferenceRider, 0, "LateralDifferenceAtLeast5cm");
        }
        foreach (var outcome in Resolutions(e)) {
            var peer = outcome.Winner == outcome.RiderA ? outcome.RiderB : outcome.RiderA;
            Event(outcome.ClearAt, outcome.Winner, peer, "ParallelContestEntireBikeAhead");
            Event(outcome.HeldAt, outcome.Winner, peer, "ParallelLeadHeldOneSecondTenMeters");
        }
        foreach (var c in e.Result.Contacts.Where(c => c.Kind == "begin"))
            Event(c.TimeSeconds, c.RiderA, c.RiderB, "ContactSeverity=" + PhysicalContactAnalyzer.Classify(c.SeverityRatioA));
        foreach (var t in new[] {2d, 4d, 6d, e.Result.TimeSeconds}.Distinct().Where(t => t <= e.Result.TimeSeconds))
            Event(t, e.Result.Riders[0].Id, e.Result.Riders[1].Id, "AcceptedProgressCheckpoint");
        foreach (var row in rows.OrderBy(r => (double)r[2]!).ThenBy(r => (int)r[3]!)) Row(writer, row);
    }
    private static PhysicalBikePose Pose(RiderView v) => new(v.Id, "shared-world", new(v.Position.X, v.Position.Y),
        new(0, v.Speed > .01f ? Math.Atan2(v.Velocity.Y, v.Velocity.X) : v.Heading, v.Heading), Bike);
    public static void Benchmark(string directory)
    {
        Directory.CreateDirectory(directory); var rows = new List<object>();
        var heat = Scenarios.A(4);
        var solo = heat with {Name = "solo-heat", Riders = new[] {heat.Riders[0] with {TrafficEnabled = true}}};
        var distant = heat with {Name = "four-distant", DurationSeconds = .8, Laps = 0,
            Riders = heat.Riders.Select((r, i) => r with {TrafficEnabled = true, ReferenceMeters = i * 70,
                SpeedMetersPerSecond = 18, OffsetMeters = 4, TargetOffsetMeters = 4}).ToArray()};
        foreach (var original in new[] {solo, distant, SideBySideScenarios.A(), SideBySideScenarios.E(), SideBySideScenarios.F(), Scenarios.DenseStart()})
        {
            RunResult Execute(Scenario s, bool side) {
                using var world = new WorldRunner(s.Riders, parameters: new() {EnableTacticalRacing = true, EnableSideBySideContests = side});
                return world.Run(s.Name, .01, s.DurationSeconds, s.Laps, stopOnCrash: true);
            }
            var off = Execute(original, false); var on = Execute(original, true);
            var horizon = Math.Min(off.TimeSeconds, on.TimeSeconds);
            var clipped = horizon < original.DurationSeconds - 1e-9 && original.Laps == 0;
            var scenario = clipped ? original with {DurationSeconds = horizon} : original;
            foreach (var side in new[] {false, true}) {Execute(scenario, side); Execute(scenario, side);}
            var elapsed = new[] {new List<double>(), new List<double>()};
            var cpu = new double[2]; var allocated = new long[2]; var tactical = new double[2]; var results = new RunResult[2];
            using var process = Process.GetCurrentProcess(); const int samples = 5, batch = 2;
            for (var sample = 0; sample < samples; sample++) foreach (var index in sample % 2 == 0 ? new[] {0, 1} : new[] {1, 0})
            {
                var allocation = GC.GetAllocatedBytesForCurrentThread(); var startCpu = process.TotalProcessorTime;
                var start = Stopwatch.GetTimestamp();
                for (var n = 0; n < batch; n++) {results[index] = Execute(scenario, index == 1); tactical[index] += results[index].Work.TacticalElapsedMilliseconds;}
                elapsed[index].Add(Stopwatch.GetElapsedTime(start).TotalMilliseconds / batch);
                cpu[index] += (process.TotalProcessorTime - startCpu).TotalMilliseconds;
                allocated[index] += GC.GetAllocatedBytesForCurrentThread() - allocation;
            }
            for (var index = 0; index < 2; index++) {
                var r = results[index]; long changes = 0;
                foreach (var spec in scenario.Riders) {
                    var target = spec.TargetOffsetMeters; var drive = 1f;
                    foreach (var d in r.Decisions.Where(d => d.RiderId == spec.Profile.Id && d.ActualActivation is not null)) {
                        if (target != d.TargetOffsetMeters || drive != d.Drive) changes++;
                        target = d.TargetOffsetMeters; drive = d.Drive;
                    }
                }
                elapsed[index].Sort(); rows.Add(new {scenario.Name, Side = index == 1, r.TimeSeconds, ClippedToCommonHorizon = clipped,
                    ElapsedMilliseconds = elapsed[index][samples / 2], CpuMilliseconds = cpu[index] / (samples * batch),
                    AllocatedBytes = allocated[index] / (samples * batch), TacticalMilliseconds = tactical[index] / (samples * batch),
                    Samples = samples, Batch = batch, r.Work, Decisions = r.Decisions.Count, ActualControlChanges = changes, r.Fingerprint});
            }
        }
        File.WriteAllText(Path.Combine(directory, "performance.json"), JsonSerializer.Serialize(rows, Audit.Json));
    }
    public static void Diagnose(string directory)
    {
        Directory.CreateDirectory(directory);
        var evidence = TacticalAudit.Execute(TacticalScenarios.F(), .01, true);
        var frame = new TrackFrame(CoreSim.Analysis.MatchedVenueProfiles.CreateMotoarenaStandingStartTrack());
        using var csv = File.CreateText(Path.Combine(directory, "legacy-pairs.csv"));
        csv.WriteLine("time_s,own,peer,own_turn,peer_turn,reference_gap_m,projected_gap_m,lateral_m,own_mps,peer_mps,own_reference_mps,peer_reference_mps,legacy_front,legacy_closing,legacy_encounter,zero_radius_arc_comparison");
        int close = 0, alongside = 0, front = 0, closing = 0, encounter = 0, zero = 0, mixed = 0;
        foreach (var s in evidence.Samples.Where(s => s.TimeSeconds >= .1 && s.TimeSeconds < 12
            && Math.Abs(s.TimeSeconds / .1 - Math.Round(s.TimeSeconds / .1)) < 1e-8))
        foreach (var own in s.Riders) foreach (var peer in s.Riders.Where(p => p.Id != own.Id))
        {
            var delta = peer.Position - own.Position;
            if (delta.LengthSquared() > 64) continue;
            close++;
            var along = Vector2.Dot(delta, own.Road.Tangent); var across = Vector2.Dot(delta, own.Road.Outward);
            var a = Math.Abs(peer.TotalReferenceMeters - own.TotalReferenceMeters) <= Bike.OverallMechanicalLengthMeters;
            var selected = s.Riders.Where(p => p.Id != own.Id && Vector2.DistanceSquared(p.Position, own.Position) <= 900
                && Vector2.Dot(p.Position - own.Position, own.Road.Tangent) > 0)
                .OrderBy(p => Vector2.Dot(p.Position - own.Position, own.Road.Tangent)).FirstOrDefault();
            var f = selected.Id == peer.Id && along > Bike.OverallMechanicalLengthMeters;
            var c = Vector2.Dot(own.Velocity, own.Road.Tangent) - Vector2.Dot(peer.Velocity, peer.Road.Tangent);
            var t = c > .25 ? along / c : double.PositiveInfinity;
            var horizon = .8 + 2.2 * (Scenarios.G().Riders.Single(r => r.Profile.Id == own.Id).Profile.Gameplay.Abilities.Attack - 1) / 98;
            if (a) alongside++; if (f) front++; if (f && c > .25) closing++; if (f && c > .25 && t <= horizon) encounter++;
            var z = own.Road.IsTurn && !peer.Road.IsTurn;
            if (z) zero++; if (own.Road.IsTurn != peer.Road.IsTurn) mixed++;
            Row(csv, s.TimeSeconds, own.Id, peer.Id, own.Road.IsTurn, peer.Road.IsTurn,
                peer.TotalReferenceMeters - own.TotalReferenceMeters, along, across, own.Speed, peer.Speed,
                frame.ReferenceSpeed(own), frame.ReferenceSpeed(peer), f, c, double.IsFinite(t) ? t : null, z);
        }
        File.WriteAllText(Path.Combine(directory, "legacy-diagnosis.json"), JsonSerializer.Serialize(new {
            CloseDirectedObservations = close, Alongside = alongside, LegacyFront = front,
            FrontAndClosing = closing, AttackHorizon = encounter, MixedBoundary = mixed,
            ZeroRadiusArcComparison = zero, Decisions = evidence.Result.Decisions.Count,
            evidence.Result.Fingerprint}, Audit.Json));
        Console.WriteLine(File.ReadAllText(Path.Combine(directory, "legacy-diagnosis.json")));
    }
    private static void Row(TextWriter w, params object?[] values) => w.WriteLine(string.Join(",",
        values.Select(v => v is IFormattable f ? f.ToString(null, CultureInfo.InvariantCulture) : v?.ToString() ?? "")));
}
