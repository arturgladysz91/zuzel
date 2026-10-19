using System.Diagnostics;
using System.Numerics;
using System.Security.Cryptography;
using System.Text.Json;
using CoreSim;
using CoreSim.PhysicalSpace;

namespace SharedWorldSpeedway;

/// <summary>Frozen #94 observations of the accepted world. Never supplies runtime commands.</summary>
public static class CloseRacingAudit
{
    public const string Baseline = "cb980e0e59b5ad6a1c3b28ac35db778d1a1fae94";
    public static RiderProfile[] CloseProfiles() => new[] {
        FullHeatSoloAudit.Profile(1, new(65,60,65,60,80,45,50,60), new(55,55,65,50,50,50), .7f),
        FullHeatSoloAudit.Profile(2, new(55,65,70,65,55,75,50,65), new(55,55,65,50,50,50), .4f),
        FullHeatSoloAudit.Profile(3, new(60,55,60,55,75,55,50,60), new(55,55,65,50,50,50), .6f),
        FullHeatSoloAudit.Profile(4, new(70,60,65,70,60,65,50,55), new(55,55,65,50,50,50), .5f)
    };
    public static Scenario Rolling(bool bend)
    {
        var a = TacticalScenarios.E(); var start = bend ? 45 : a.Riders[0].ReferenceMeters;
        var profiles = CloseProfiles();
        var references = new[] {start, start + 8, start + 7, start + 7};
        var offsets = new[] {4f,4f,2.6f,5.4f}; var speeds = new[] {19f,18f,19.5f,18.5f};
        return new(bend ? "rolling-bend" : "rolling-straight", profiles.Select((p,i) =>
            new RiderSpec(p, CoreSim.Setup.BikeSetup.Neutral, references[i], offsets[i], speeds[i], offsets[i])).ToArray(),90,4);
    }
    public static Scenario[] Heats() => new[] {
        FullHeatSoloAudit.Heat(false,0), FullHeatSoloAudit.Heat(true,1),
        FullHeatSoloAudit.Heat(false,0) with {Name="close-rotation-0",Riders=FullHeatSoloAudit.Heat(false,0).Riders.Select((r,i)=>r with {Profile=CloseProfiles()[i]}).ToArray()},
        FullHeatSoloAudit.Heat(false,1) with {Name="close-rotation-1",Riders=FullHeatSoloAudit.Heat(false,1).Riders.Select((r,i)=>r with {Profile=CloseProfiles()[i]}).ToArray()},
        Rolling(false), Rolling(true)
    };
    public static Scenario[] ShortCases()
    {
        var s=ThrottleAudit.Cases(); var pair=s[1]; var origin=pair.Riders[0].ReferenceMeters;
        return new[] {
            pair with {Name="equal-close",DurationSeconds=5,Riders=pair.Riders.Select((r,i)=>r with {ReferenceMeters=origin+i*3.2}).ToArray()},
            s[0] with {Name="catch-slower",DurationSeconds=6},
            s[7] with {Name="leader-bend-slowdown",DurationSeconds=5},
            pair with {Name="closing-sign-change",DurationSeconds=6,Riders=pair.Riders.Select((r,i)=>r with {
                SpeedMetersPerSecond=i==0?18:19,Profile=FullHeatSoloAudit.Profile(i+1,legacy:new(50,i==0?70:35,60,50,50,50))}).ToArray()},
            s[2] with {Name="follow-attack"}, s[4] with {Name="parallel"},
            s[0] with {Name="three-shared-space",DurationSeconds=6,Riders=s[0].Riders.Take(3).Select((r,i)=>r with {
                ReferenceMeters=origin+i*5,OffsetMeters=4,TargetOffsetMeters=4,SpeedMetersPerSecond=19-i,TrafficEnabled=true}).ToArray()}
        };
    }
    public static bool IsRolling(Scenario s) => s.Name.StartsWith("rolling-",StringComparison.Ordinal);
    public static WorldParameters Variant(int variant) => variant switch {
        1=>ThrottleAudit.Parameters(true),
        2=>ThrottleAudit.Parameters(true) with {DiagnosticFollowingGap=(.4,.04)},
        3=>ThrottleAudit.Parameters(true) with {EnableClosureAdaptiveFollowingGap=true},
        _=>throw new ArgumentOutOfRangeException(nameof(variant))
    };
    public static PhysicalBikePose Pose(RiderView v,double t) => new(v.Id,"shared-world",new(v.Position.X,v.Position.Y),
        new(t,v.Road.Heading,v.Heading),SpeedwayBikeDimensions.Reference);
    public static double Separation(RiderView a,RiderView b,double time) => MechanicalSeparation.Between(Pose(a,time),Pose(b,time)).SignedMeters;
    public static void ValidateInitial(WorldRunner w)
    {
        var state=w.Snapshot;
        foreach(var own in state.Riders) {
            if(HeatMeasurements.Clearance(own,w.Frame)<0)throw new InvalidOperationException("Initial bike outside track.");
            // Existing Box2D rotation approximation differs by ~.0014 rad in this bend.
            if(Math.Abs(BikeAngles.Wrap(own.Heading-own.Road.Heading))>.005)throw new InvalidOperationException($"Invalid initial heading R{own.Id}.");
            foreach(var peer in state.Riders.Where(p=>p.Id>own.Id))
                if(Separation(own,peer,0)<=0)throw new InvalidOperationException("Initial capsules overlap.");
        }
    }
    private static void Save(string directory,string name,object value) => File.WriteAllText(Path.Combine(directory,name+".json"),JsonSerializer.Serialize(value,Audit.Json)+"\n");
    public static GapRun Execute(Scenario s, WorldParameters? parameters=null,bool reverse=false,bool trace=true,
        WorldDiagnostics? additionalDiagnostics=null,Action<WorldSnapshot>? acceptedObserver=null,string? trajectoryPath=null)
    {
        var p=parameters??ThrottleAudit.Parameters(true);
        HeatMeasurements? measurement=null; var observations=new List<ThrottleObservation>();
        var consequences=new List<ConsequenceObservation>();
        var zeroReasons=new Dictionary<string,int>();
        using var w=new WorldRunner(reverse?s.Riders.Reverse():s.Riders,parameters:p,diagnostics:new() {
            AutonomousLine=additionalDiagnostics?.AutonomousLine,
            Force=f=>{measurement!.Force(f);additionalDiagnostics?.Force?.Invoke(f);},
            Consequence=c=>{consequences.Add(c);additionalDiagnostics?.Consequence?.Invoke(c);},
            Throttle=o=>{if(o.DesiredDrive==0)zeroReasons[o.Reason]=zeroReasons.GetValueOrDefault(o.Reason)+1;
                if(trace && (o.Emergency || Math.Abs(o.TimeSeconds*2-Math.Round(o.TimeSeconds*2))<1e-8))observations.Add(o);
                additionalDiagnostics?.Throttle?.Invoke(o);}});
        ValidateInitial(w);
        var initial=w.Snapshot;
        using var collector=new HeatMeasurements(w.Frame,initial,trajectoryPath,lapsFromInitialPosition:IsRolling(s)); measurement=collector;
        var data=s.Riders.ToDictionary(r=>r.Profile.Id,_=>new GapAccumulator());
        var samples=new List<GapSample>(); var previous=initial; var nextSample=0d;
        var watch=Stopwatch.GetTimestamp(); var bytes=GC.GetAllocatedBytesForCurrentThread();
        var r=w.Run(s.Name,.01,s.DurationSeconds,s.Laps,state=>{
            collector.Accept(state); var dt=state.TimeSeconds-previous.TimeSeconds;
            foreach(var own in previous.Riders) {
                if(own.Activity is RiderActivity.Finished or RiderActivity.Crashed)continue;
                var current=state.Riders.Single(v=>v.Id==own.Id); var a=data[own.Id]; var measuredDt=dt;
                if(current.Activity==RiderActivity.Finished) {
                    var finish=(IsRolling(s)?s.Riders.Single(x=>x.Profile.Id==own.Id).ReferenceMeters:0)+4*w.Frame.LapMeters;
                    measuredDt=dt*Math.Clamp((finish-own.TotalReferenceMeters)/(current.TotalReferenceMeters-own.TotalReferenceMeters),0,1);
                }
                if(own.Drive==0)a.Zero+=measuredDt;else if(own.Drive==1)a.Full+=measuredDt;else a.Partial+=measuredDt;
                if(current.Drive==0 && own.Drive>0)a.FullClosures++;
                if(Math.Abs(current.Drive-own.Drive)>=TrafficThrottle.DriveDeadband) {
                    var direction=Math.Sign(current.Drive-own.Drive);if(a.LastDirection!=0 && direction!=a.LastDirection)a.Reversals++;
                    a.LastDirection=direction;
                }
                var spec=s.Riders.Single(x=>x.Profile.Id==own.Id);
                var gap=TrafficThrottle.Front(previous,own,own.TargetOffsetMeters,RiderController.ReactionSeconds(spec),p);
                var minimum=previous.Riders.Where(v=>v.Id!=own.Id).Select(v=>Separation(own,v,previous.TimeSeconds)).DefaultIfEmpty(double.PositiveInfinity).Min();
                a.MinimumSeparation=Math.Min(a.MinimumSeparation,minimum);
                var dangerous=minimum<.3 && previous.Riders.Any(peer=>peer.Id!=own.Id &&
                    Vector2.Dot(own.Velocity-peer.Velocity,peer.Position-own.Position)>0);
                if(dangerous){a.NearSeconds+=measuredDt;if(!a.Near)a.NearEpisodes++;}a.Near=dangerous;
                var following=gap is {} g && g.GapMeters is >=0 and <=10 && g.LateralGapMeters<=TrafficThrottle.ClearanceMeters;
                if(gap is not null)a.PredictedCorridor+=measuredDt;
                if(following && gap is {} f) {
                    var peer=previous.Riders.Single(v=>v.Id==f.OpponentId);
                    a.Following+=measuredDt;
                    if(f.GapMeters<=3)a.CloseSeconds+=measuredDt;
                    if(own.Drive is >0 and <1)a.PartialFollowing+=measuredDt;
                    if(Math.Abs(f.ClosingMetersPerSecond)<=.5)a.StableFollowing+=measuredDt;
                    a.Gaps.Add(new(f.GapMeters,f.LateralGapMeters,Separation(own,peer,previous.TimeSeconds),peer.TotalReferenceMeters-own.TotalReferenceMeters,measuredDt));
                    var sign=Math.Abs(f.ClosingMetersPerSecond)>.1?Math.Sign(f.ClosingMetersPerSecond):0;
                    if(sign!=0){if(a.ClosingSign!=0&&sign!=a.ClosingSign)a.ClosingSignChanges++;a.ClosingSign=sign;}
                }
                if(trace && (state.TimeSeconds+1e-8>=nextSample || current.Drive!=own.Drive || current.TargetOffsetMeters!=own.TargetOffsetMeters)) {
                    var peer=gap is {} selected?previous.Riders.Single(v=>v.Id==selected.OpponentId):default;
                    samples.Add(new(previous.TimeSeconds,own.Id,peer.Id,own.Drive,own.Speed,peer.Speed,own.Road.OffsetMeters,
                        own.TargetOffsetMeters,gap,peer.Id==0?null:Separation(own,peer,previous.TimeSeconds),
                        peer.Id==0?null:peer.TotalReferenceMeters-own.TotalReferenceMeters,following));
                }
            }
            if(state.TimeSeconds+1e-8>=nextSample)nextSample=state.TimeSeconds+.5;
            previous=state; acceptedObserver?.Invoke(state);
        },stopOnCrash:true,lapsFromInitialPosition:IsRolling(s));
        var elapsed=Stopwatch.GetElapsedTime(watch).TotalMilliseconds;var allocated=GC.GetAllocatedBytesForCurrentThread()-bytes;
        var metrics=collector.Finish(r);
        return new(s.Name,IsRolling(s),FullHeatSoloAudit.Status(r),p,
            s.Riders.Select(x=>new {x.Profile.Id,Canonical=x.Profile.Gameplay,Legacy=x.Profile.Skills,x.Setup,x.ReferenceMeters,x.OffsetMeters,x.TargetOffsetMeters,x.SpeedMetersPerSecond,x.TrafficEnabled}).ToArray(),
            initial, r,metrics.Select(m=>data[m.RiderId].Finish(m)).ToArray(),collector.Passes.Events.ToArray(),
            consequences.ToArray(),collector.Anomalies.ToArray(),zeroReasons,observations.ToArray(),samples.ToArray(),elapsed,allocated);
    }
    public static void Run(string directory,string stage)
    {
        Directory.CreateDirectory(directory);
        if(stage=="freeze") {
            var path=Path.Combine(directory,"frozen-configuration.json");if(File.Exists(path))throw new InvalidOperationException("Already frozen.");
            Save(directory,"frozen-configuration",new {Baseline,CreatedUtc=DateTime.UtcNow,Parameters=ThrottleAudit.Parameters(true),
                StepSeconds=.01,Substeps=4,Surface=TrackSurfaceState.Default,Heats=Heats(),ShortCases=ShortCases(),
                Profiles=CloseProfiles().Select(p=>new{p.Id,Canonical=p.Gameplay,Legacy=p.Skills}),
                Variants=new[]{"0.6+0.1*v","0.4+0.04*v","0.6+max(0,closing)*reaction+max(0,closing)^2/(2*max(0.1,-aZero))"},
                Following="accepted interval start: Front corridor AND current lateral gap <= .15m AND longitudinal gap 0..10m; close <=3m; stable |closing|<=.5m/s; weighted by interval duration",
                RollingTiming="four complete reference circuits from EACH initial progress; separate from gate starts",SamplesSeconds=.5});
            return;
        }
        if(stage=="rolling-first") {SaveRun(Rolling(false),"baseline");return;}
        if(stage=="variants") {
            VerifySeal(directory);
            foreach(var s in ShortCases())foreach(var variant in new[]{2,3}) {
                var r=Execute(s,Variant(variant));Save(directory,s.Name+"-variant-"+variant,r);
                Console.WriteLine($"{s.Name} v{variant}: following={r.Riders.Sum(x=>x.FollowingSeconds):F3}, partial={r.Riders.Sum(x=>x.PartialSeconds):F3}, zero={r.Riders.Sum(x=>x.ZeroSeconds):F3}, near={r.Riders.Sum(x=>x.NearCollisionSeconds):F3}, contacts={r.Result.Work.Episodes}");
            }return;
        }
        if(stage=="candidate") {
            VerifySeal(directory);
            if(!File.Exists(Path.Combine(directory,"selection.md")))throw new InvalidOperationException("Record selection before full trials.");
            foreach(var s in Heats()) {
                var r=Execute(s,Variant(3));Save(directory,s.Name+"-candidate",r);
                Console.WriteLine($"{s.Name} candidate: {r.Status}; following={r.Riders.Sum(x=>x.FollowingSeconds):F3}, partial={r.Riders.Sum(x=>x.PartialSeconds):F3}, near={r.Riders.Sum(x=>x.NearCollisionSeconds):F3}, contacts={r.Result.Work.Episodes}, passes={r.Passes.Length}");
            }return;
        }
        if(stage=="benchmark") { Benchmark(directory);return; }
        if(stage=="repeat") {
            VerifySeal(directory);var rows=new List<object>();
            foreach(var s in new[]{ShortCases()[0],ShortCases()[4],Rolling(false) with {DurationSeconds=8,Laps=0}}) {
                var a=Execute(s,Variant(3),trace:false);var b=Execute(s,Variant(3),reverse:true,trace:false);
                var exactHash=a.Result.Fingerprint==b.Result.Fingerprint;
                var exactDecisions=a.Result.Decisions.SequenceEqual(b.Result.Decisions);
                var exactContacts=a.Result.Contacts.SequenceEqual(b.Result.Contacts);
                rows.Add(new{s.Name,Reverse=true,ExactHash=exactHash,ExactDecisions=exactDecisions,ExactContacts=exactContacts,a.Result.Fingerprint});
                if(!(exactHash&&exactDecisions&&exactContacts))throw new InvalidOperationException("Repeat mismatch.");
            }
            Save(directory,"repeats",rows);return;
        }
        if(stage=="baseline") {
            foreach(var s in Heats().Where(s=>!File.Exists(Path.Combine(directory,s.Name+"-baseline.json"))))SaveRun(s,"baseline");
            foreach(var s in ShortCases())SaveRun(s,"baseline");
            var files=Directory.GetFiles(directory,"*.json").OrderBy(x=>x,StringComparer.Ordinal).Select(path=>new {
                File=Path.GetFileName(path),Sha256=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)))}).ToArray();
            Save(directory,"baseline-seal",new{Baseline,SealedUtc=DateTime.UtcNow,Files=files});return;
        }
        throw new ArgumentException("Use freeze, rolling-first, baseline, variants, candidate, repeat or benchmark.");
        void SaveRun(Scenario s,string suffix) {
            var name=s.Name+"-"+suffix;if(File.Exists(Path.Combine(directory,name+".json")))throw new InvalidOperationException("Do not overwrite frozen evidence.");
            var r=Execute(s);Save(directory,name,r);
            Console.WriteLine($"{name}: {r.Status}; following={r.Riders.Sum(v=>v.FollowingSeconds):F3}, partial={r.Riders.Sum(v=>v.PartialSeconds):F3}, contacts={r.Result.Work.Episodes}");
        }
    }
    public static void VerifySeal(string directory) {
        using var seal=JsonDocument.Parse(File.ReadAllText(Path.Combine(directory,"baseline-seal.json")));
        foreach(var file in seal.RootElement.GetProperty("Files").EnumerateArray()) {
            var path=Path.Combine(directory,file.GetProperty("File").GetString()!);
            if(Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)))!=file.GetProperty("Sha256").GetString())
                throw new InvalidOperationException("Frozen baseline changed: "+path);
        }
    }
    private static void Benchmark(string directory) {
        var rows=new List<object>();
        foreach(var s in new[]{ThrottleAudit.Cases()[0],Rolling(false) with {DurationSeconds=8,Laps=0}})
        foreach(var mode in new[]{0,1,3})for(var n=-1;n<3;n++) {
            var bytes=GC.GetAllocatedBytesForCurrentThread();var watch=Stopwatch.GetTimestamp();RunResult result;
            using(var w=new WorldRunner(s.Riders,parameters:mode==0?ThrottleAudit.Parameters(false):Variant(mode)))
                result=w.Run(s.Name,.01,8,stopOnCrash:true);
            var elapsed=Stopwatch.GetElapsedTime(watch).TotalMilliseconds;
            if(n>=0)rows.Add(new{s.Name,Mode=mode,Sample=n,ElapsedMilliseconds=elapsed,AllocatedBytes=GC.GetAllocatedBytesForCurrentThread()-bytes,
                result.TimeSeconds,result.Fingerprint,result.Work});
        }
        Save(directory,"performance",rows);Console.WriteLine("Two 8s windows: OFF / #93 / candidate, warmup + 3 samples; no observers.");
    }
}

public sealed record WeightedGap(double Longitudinal,double Lateral,double Mechanical,double Progress,double Seconds);
public sealed record GapDistribution(double Seconds,double? Minimum,double? P10,double? Median,double? P90,double? Maximum)
{
    public static GapDistribution From(IEnumerable<WeightedGap> source,Func<WeightedGap,double> selector) {
        var a=source.OrderBy(selector).ToArray();var seconds=a.Sum(x=>x.Seconds);
        double? Quantile(double fraction) {double cumulative=0;foreach(var x in a){cumulative+=x.Seconds;if(cumulative>=seconds*fraction)return selector(x);}return null;}
        return new(seconds,a.Length>0?selector(a[0]):null,Quantile(.1),Quantile(.5),Quantile(.9),a.Length>0?selector(a[^1]):null);
    }
}
internal sealed class GapAccumulator
{
    public double Zero,Full,Partial,Following,CloseSeconds,PartialFollowing,StableFollowing,PredictedCorridor,NearSeconds,MinimumSeparation=double.PositiveInfinity;
    public int FullClosures,Reversals,LastDirection,ClosingSign,ClosingSignChanges,NearEpisodes;public bool Near;
    public List<WeightedGap> Gaps {get;}=new();
    public GapRider Finish(RiderMeasurement m)=>new(m,Zero,Partial,Full,Following,CloseSeconds,PartialFollowing,StableFollowing,PredictedCorridor,
        FullClosures,Reversals,ClosingSignChanges,NearSeconds,NearEpisodes,MinimumSeparation,
        GapDistribution.From(Gaps,x=>x.Longitudinal),GapDistribution.From(Gaps,x=>x.Lateral),GapDistribution.From(Gaps,x=>x.Mechanical),GapDistribution.From(Gaps,x=>x.Progress));
}
public sealed record GapRider(RiderMeasurement Measurement,double ZeroSeconds,double PartialSeconds,double FullSeconds,
    double FollowingSeconds,double CloseSeconds,double PartialFollowingSeconds,double StableFollowingSeconds,double PredictedCorridorSeconds,
    int FullClosures,int ThrottleReversals,int ClosingSignChanges,double NearCollisionSeconds,int NearCollisionEpisodes,double MinimumSeparationMeters,
    GapDistribution Longitudinal,GapDistribution Lateral,GapDistribution Mechanical,GapDistribution ReferenceProgress);
public sealed record GapSample(double TimeSeconds,int RiderId,int OpponentId,float Drive,float OwnSpeed,float OpponentSpeed,
    double OffsetMeters,float TargetMeters,TrafficGap? Gap,double? MechanicalMeters,double? ReferenceProgressMeters,bool Following);
public sealed record GapRun(string Name,bool Rolling,string Status,WorldParameters Parameters,object[] Inputs,WorldSnapshot Initial,RunResult Result,
    GapRider[] Riders,ConfirmedPass[] Passes,ConsequenceObservation[] Consequences,AuditAnomaly[] Anomalies,Dictionary<string,int> ZeroReasons,
    ThrottleObservation[] Observations,GapSample[] Samples,double ElapsedMilliseconds,long AllocatedBytes);
