using System.Diagnostics;
using System.Numerics;
using System.Text.Json;
using CoreSim;
using CoreSim.PhysicalSpace;

namespace SharedWorldSpeedway;

public static class ThrottleAudit
{
    public static WorldParameters Parameters(bool enabled) => FullHeatSoloAudit.Parameters() with {EnableProportionalTrafficThrottle = enabled};
    public static IReadOnlyList<Scenario> Cases()
    {
        var source = TacticalScenarios.E(); var riders = source.Riders;
        var a = source with {Name = "A-blocked-follow", DurationSeconds = 8,
            Riders = riders.Select((r,i) => r with {SpeedMetersPerSecond = i == 0 ? 18 : 16, TrafficEnabled = i == 0}).ToArray()};
        var b = a with {Name = "B-steady-close", DurationSeconds = 3, Riders = new[] {
            a.Riders[0] with {SpeedMetersPerSecond = 18}, a.Riders[1] with {ReferenceMeters = a.Riders[0].ReferenceMeters + 6, SpeedMetersPerSecond = 18}}};
        var c = a with {Name = "C-follow-reattack", DurationSeconds = 10, Riders = a.Riders.Select((r,i) => {
            var skills = i == 1 ? new RiderSkills(50, 20, 50, 50, 50, 50) : new RiderSkills(50, 80, 60, 50, 50, 50);
            return r with {SpeedMetersPerSecond = i >= 2 ? 23 : r.SpeedMetersPerSecond,
                Profile = RiderProfile.CreateCanonical(r.Profile.Id, r.Profile.Name, r.Profile.Gameplay, skills, r.Profile.Style)};
        }).ToArray()};
        var d = TacticalScenarios.A() with {Name = "D-immediate-threat", DurationSeconds = 2};
        d = d with {Riders = new[] {d.Riders[0], d.Riders[1] with {ReferenceMeters = d.Riders[0].ReferenceMeters + 7, SpeedMetersPerSecond = 10, TrafficEnabled = false}}};
        var e = SideBySideScenarios.A() with {Name = "E-safe-parallel"};
        Scenario Capability(bool high) => a with {Name = high ? "F-high-capability" : "F-low-capability", DurationSeconds = 3,
            Riders = a.Riders.Take(2).Select((r,i) => i == 0 ? r with {
                Profile = RiderProfile.CreateCanonical(r.Profile.Id, r.Profile.Name, r.Profile.Gameplay,
                    new(50, high ? 80 : 20, high ? 80 : 20, 50, 50, 50), r.Profile.Style)} : r).ToArray()};
        var g = b with {Name = "G-straight-bend", DurationSeconds = 3, Riders = new[] {
            b.Riders[0] with {ReferenceMeters = 32, SpeedMetersPerSecond = 19},
            b.Riders[1] with {ReferenceMeters = 38, SpeedMetersPerSecond = 18}}};
        return new[] {a,b,c,d,e,Capability(false),Capability(true),g};
    }

    public static ThrottleRun Execute(Scenario scenario, bool enabled, bool reverse = false, bool detailed = true)
    {
        var observations = new List<ThrottleObservation>(); var samples = new List<ThrottleSample>();
        var responses = new List<ThrottleResponse>();
        var counters = scenario.Riders.ToDictionary(r => r.Profile.Id, r => new double[9]);
        var followingAt = new Dictionary<int,double>(); var close = new HashSet<int>();
        double nextSample = 0; HeatMeasurements? measurements = null; WorldSnapshot previous = null!;
        using var w = new WorldRunner(reverse ? scenario.Riders.Reverse() : scenario.Riders, parameters: Parameters(enabled),
            diagnostics: new() {Force = f => measurements!.Force(f), Throttle = o => {
                if (detailed && (o.TimeSeconds % .5 < 1e-8 || o.Emergency)) observations.Add(o);
            }});
        using var collector = new HeatMeasurements(w.Frame, w.Snapshot); measurements = collector; previous = w.Snapshot;
        var watch = Stopwatch.GetTimestamp(); var allocated = GC.GetAllocatedBytesForCurrentThread();
        var result = w.Run(scenario.Name, .01, scenario.DurationSeconds, scenario.Laps, state => {
            collector.Accept(state); var dt = state.TimeSeconds - previous.TimeSeconds;
            for (var i = 0; i < state.Riders.Count; i++) {
                var own = state.Riders[i]; var old = previous.Riders[i]; var values = counters[own.Id];
                if (old.Activity == RiderActivity.Finished) continue;
                if (old.Drive == 0) values[0] += dt; else if (old.Drive < 1) values[1] += dt;
                if (values[1] >= .3) followingAt.TryAdd(own.Id, state.TimeSeconds);
                if (own.Drive == 0 && old.Drive > 0) values[2]++;
                var change = own.Drive - old.Drive;
                if (detailed && change != 0 && dt > 0) responses.Add(new(previous.TimeSeconds,state.TimeSeconds,own.Id,
                    old.Drive,own.Drive,old.Speed,own.Speed,Vector2.Dot(own.Velocity-old.Velocity,old.Road.Tangent)/dt));
                if (Math.Abs(change) >= TrafficThrottle.DriveDeadband) {
                    var direction = Math.Sign(change); if (values[7] != 0 && values[7] != direction) values[8]++;
                    values[7] = direction;
                }
                if (own.Drive is > 0 and < 1 && (old.Drive == 0 || old.Drive == 1)) values[3]++;
                var front = TrafficThrottle.Front(state, own, own.TargetOffsetMeters,
                    RiderController.ReactionSeconds(scenario.Riders.Single(r => r.Profile.Id == own.Id)), w.Parameters);
                var dangerous = false;
                if (front is {ClosingMetersPerSecond: > .1} candidate) {
                    var peer = state.Riders.Single(v => v.Id == candidate.OpponentId);
                    PhysicalBikePose Pose(RiderView v) => new(v.Id,"world",new(v.Position.X,v.Position.Y),
                        new(state.TimeSeconds,v.Road.Heading,v.Heading),SpeedwayBikeDimensions.Reference);
                    dangerous = MechanicalSeparation.Between(Pose(own),Pose(peer)).SignedMeters < .3;
                }
                if (dangerous) { values[4] += dt; if (close.Add(own.Id)) values[5]++; } else close.Remove(own.Id);
                if (front is {GapMeters: >= 0 and < 5}) values[6] += dt;
                if (detailed && state.TimeSeconds + 1e-8 >= nextSample) {
                    var peer = front is { } f ? state.Riders.Single(v => v.Id == f.OpponentId) : state.Riders.Where(v => v.Id != own.Id)
                        .OrderBy(v => Vector2.DistanceSquared(own.Position,v.Position)).FirstOrDefault();
                    var gap = peer.Id == 0 ? (TrafficGap?)null : TrafficThrottle.Measure(own,peer,own.TargetOffsetMeters,
                        RiderController.ReactionSeconds(scenario.Riders.Single(r => r.Profile.Id == own.Id)), w.Parameters);
                    samples.Add(new(state.TimeSeconds, own.Id, peer.Id, own.Speed, peer.Speed, own.Drive, own.Road.OffsetMeters,
                        own.TargetOffsetMeters, gap));
                }
            }
            if (state.TimeSeconds + 1e-8 >= nextSample) nextSample = state.TimeSeconds + .5;
            previous = state;
        }, stopOnCrash:true);
        var elapsed = Stopwatch.GetElapsedTime(watch).TotalMilliseconds; var bytes = GC.GetAllocatedBytesForCurrentThread() - allocated;
        var metrics = collector.Finish(result);
        var riders = metrics.Select(v => new ThrottleRider(v, counters[v.RiderId][0], counters[v.RiderId][1],
            (int)counters[v.RiderId][2], (int)counters[v.RiderId][3], counters[v.RiderId][4], (int)counters[v.RiderId][5], counters[v.RiderId][6], (int)counters[v.RiderId][8],
            result.Decisions.Count(d => d.RiderId == v.RiderId && d.Channels == ControlChannels.Line
                && d.Action is TrafficAction.AttackInside or TrafficAction.AttackOutside
                && d.ActualActivation is { } t && followingAt.TryGetValue(v.RiderId,out var followed) && t > followed))).ToArray();
        return new(scenario.Name, enabled, FullHeatSoloAudit.Status(result), scenario.Riders.Select(r => new {
            r.Profile.Id, Canonical = r.Profile.Gameplay, Legacy = r.Profile.Skills, r.Setup, r.ReferenceMeters, r.OffsetMeters,
            r.TargetOffsetMeters, r.SpeedMetersPerSecond, r.InitialOutwardSpeed, r.TrafficEnabled}).ToArray(),
            result, riders, collector.Passes.Events.ToArray(), observations.ToArray(), samples.ToArray(), responses.ToArray(), elapsed, bytes);
    }

    public static void Run(string directory, string stage)
    {
        Directory.CreateDirectory(directory);
        if (stage == "repeat") {
            var rows = new List<object>();
            foreach (var s in new[] {Cases()[0],Cases()[2],FullHeatSoloAudit.Heat(false,0),FullHeatSoloAudit.Heat(true,0),FullHeatSoloAudit.Heat(true,1)}) {
                var original = JsonSerializer.Deserialize<ThrottleRun>(File.ReadAllText(Path.Combine(directory,s.Name+"-ON.json")),Audit.Json)!;
                var r=Execute(s,true,reverse:true,detailed:false);
                var hash=original.Result.Fingerprint==r.Result.Fingerprint;
                var decisions=original.Result.Decisions.SequenceEqual(r.Result.Decisions);var contacts=original.Result.Contacts.SequenceEqual(r.Result.Contacts);
                rows.Add(new {s.Name,Reverse=true,ExactHash=hash,ExactDecisions=decisions,ExactContacts=contacts,r.Result.Fingerprint});
                if(!(hash&&decisions&&contacts))throw new InvalidOperationException("Repeat failed: "+s.Name);
            }
            File.WriteAllText(Path.Combine(directory,"repeats.json"),JsonSerializer.Serialize(rows,Audit.Json));
            Console.WriteLine("Five ON reverse-input repeats exact in a fresh process.");return;
        }
        if (stage == "benchmark") {Benchmark(directory);return;}
        if (stage == "pairs") {
            var frame=new TrackFrame(CoreSim.Analysis.MatchedVenueProfiles.CreateMotoarenaStandingStartTrack());
            File.WriteAllText(Path.Combine(directory,"configuration.json"),JsonSerializer.Serialize(new {
                Baseline="8cd7dc67f66c798a1ba452f57dccad79fe6d5386",Off=Parameters(false),On=Parameters(true),StepSeconds=.01,Substeps=4,
                Surface=TrackSurfaceState.Default,frame.Track.Geometry,frame.LapMeters,StopOnCrash=true,SampleIntervalSeconds=.5,
                FullHeatMaximumSeconds=90,FullHeatRequiredLaps=4,TrafficThrottle.ResponseSeconds,TrafficThrottle.ClearanceMeters,
                TrafficThrottle.DriveDeadband,TrafficThrottle.MaximumOrdinaryDriveChange,DesiredGap="0.6 m + 0.1 s * ownSpeed",
                Runtime=System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription,OS=System.Runtime.InteropServices.RuntimeInformation.OSDescription,
                DiagnosticTrace="accepted samples at 0.5 s; no per-step files; existing #92 profiles unchanged"
            },Audit.Json));
        }
        var scenarios = stage == "pairs" ? Cases() : stage == "heats"
            ? (from varied in new[] {false,true} from rotation in Enumerable.Range(0,4) select FullHeatSoloAudit.Heat(varied,rotation)).ToArray()
            : throw new ArgumentException("Use pairs or heats.");
        foreach (var s in scenarios) foreach (var enabled in new[] {false,true}) {
            var r = Execute(s, enabled, detailed: stage == "pairs" || s.Name is "A1-rotation-0" or "A2-rotation-1");
            File.WriteAllText(Path.Combine(directory, s.Name + (enabled ? "-ON.json" : "-OFF.json")), JsonSerializer.Serialize(r,Audit.Json));
            Console.WriteLine($"{s.Name} {(enabled ? "ON" : "OFF")}: {r.Status}; zero={r.Riders.Sum(v=>v.ZeroSeconds):F3}, partial={r.Riders.Sum(v=>v.PartialSeconds):F3}, contacts={r.Result.Work.Episodes}, reattacks={r.Riders.Sum(v=>v.Reattacks)}");
        }
    }

    private static void Benchmark(string directory)
    {
        var rows=new List<object>();
        foreach(var s in new[]{Cases()[0],FullHeatSoloAudit.Heat(false,0),FullHeatSoloAudit.Heat(true,1)})
        foreach(var enabled in new[]{false,true}) for(var n=-1;n<3;n++) {
            using var process=Process.GetCurrentProcess();var cpu=process.TotalProcessorTime;
            var bytes=GC.GetAllocatedBytesForCurrentThread();var start=Stopwatch.GetTimestamp();RunResult result;
            using(var w=new WorldRunner(s.Riders,parameters:Parameters(enabled)))
                result=w.Run(s.Name,.01,s.DurationSeconds,s.Laps,stopOnCrash:true);
            var elapsed=Stopwatch.GetElapsedTime(start).TotalMilliseconds;
            if(n>=0)rows.Add(new{s.Name,Enabled=enabled,Sample=n,ElapsedMilliseconds=elapsed,
                CpuMilliseconds=(process.TotalProcessorTime-cpu).TotalMilliseconds,AllocatedBytes=GC.GetAllocatedBytesForCurrentThread()-bytes,
                TieredCompilation=Environment.GetEnvironmentVariable("DOTNET_TieredCompilation"),result.TimeSeconds,result.Fingerprint,result.Work});
        }
        File.WriteAllText(Path.Combine(directory,"performance.json"),JsonSerializer.Serialize(rows,Audit.Json));
        Console.WriteLine("Three configurations, OFF/ON, one warmup and three measured bare samples each.");
    }
    public static void Diagnose(string directory)
    {
        Directory.CreateDirectory(directory); var evidence = new List<object>();
        foreach (var s in new[] {FullHeatSoloAudit.Heat(false, 0), FullHeatSoloAudit.Heat(true, 1)})
        {
            var original = JsonSerializer.Deserialize<AuditRun>(File.ReadAllText(Path.Combine(
                "docs/experiments/full-heat-and-solo-validation", s.Name + ".json")), Audit.Json)!;
            var times = original.Result.Decisions.Where(d => d.Reason is "NoPassingCorridor" or "ImmediateThreat" or "ResumeContest")
                .Select(d => d.ObservedAt).ToArray();
            var samples = new List<WorldSnapshot>();
            using var w = new WorldRunner(s.Riders, parameters: FullHeatSoloAudit.Parameters());
            var result = w.Run(s.Name, .01, 90, 4, state => {
                if (times.Any(t => Math.Abs(state.TimeSeconds - t) < 1e-8)) samples.Add(state);
            }, stopOnCrash: true);
            if (original.Result.Fingerprint != result.Fingerprint) throw new InvalidOperationException("#92 baseline mismatch.");
            evidence.Add(new {s.Name, result.Fingerprint, Exact92 = true, result.Decisions,
                Episodes = original.Result.Decisions.Where(d => times.Contains(d.ObservedAt)).Select(d => {
                    var state = samples.Single(x => Math.Abs(x.TimeSeconds - d.ObservedAt) < 1e-8);
                    var own = state.Riders.Single(v => v.Id == d.RiderId);
                    var other = state.Riders.FirstOrDefault(v => v.Id == d.OpponentId);
                    var spec = s.Riders.Single(v => v.Profile.Id == own.Id);
                    return new {Decision = d, Own = own, Other = other, Gap = other.Id == 0 ? (TrafficGap?)null
                        : TrafficThrottle.Measure(own, other, own.TargetOffsetMeters, RiderController.ReactionSeconds(spec), w.Parameters)};
                }).ToArray()});
        }
        File.WriteAllText(Path.Combine(directory, "diagnosis-92.json"), JsonSerializer.Serialize(evidence, Audit.Json));
        Console.WriteLine("Two #92 heats reproduced exactly; measured baseline episodes saved.");
    }
}
public sealed record ThrottleSample(double TimeSeconds, int RiderId, int OpponentId, float OwnSpeed, float OpponentSpeed,
    float Drive, double OffsetMeters, float TargetMeters, TrafficGap? Gap);
public sealed record ThrottleResponse(double ActivationBoundarySeconds, double AcceptedAtSeconds, int RiderId,
    float OldDrive, float NewDrive, float BeforeSpeed, float AfterSpeed, double ActualAlongAcceleration);
public sealed record ThrottleRider(RiderMeasurement Measurement, double ZeroSeconds, double PartialSeconds, int FullClosures,
    int FollowingEpisodes, double DangerousSeconds, int DangerousEpisodes, double CloseFollowingSeconds, int ThrottleReversals, int Reattacks);
public sealed record ThrottleRun(string Name, bool Enabled, string Status, object[] Inputs, RunResult Result, ThrottleRider[] Riders,
    ConfirmedPass[] Passes, ThrottleObservation[] Observations, ThrottleSample[] Samples, ThrottleResponse[] Responses, double ElapsedMilliseconds, long AllocatedBytes);
