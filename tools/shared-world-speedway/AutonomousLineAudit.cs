using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using System.Numerics;
using CoreSim;

namespace SharedWorldSpeedway;

/// <summary>Read-only #95 measurements and a bounded frozen scenario set.</summary>
public static class AutonomousLineAudit
{
    public const string Baseline="f996cd1e2c1424659dd9d106f63c007e5aa1ea0f";
    public static WorldParameters Parameters(bool enabled=false)=>ThrottleAudit.Parameters(true) with {EnableAutonomousRacingLine=enabled};
    public static Scenario[] Solos() {
        var a=FullHeatSoloAudit.Heat(false,0).Riders[0];
        Scenario Case(string name,RiderSpec r)=>new(name,new[]{r},90,4);
        return new[] {
            Case("S1-inner",a),Case("S2-outer",a with {OffsetMeters=9.5f,TargetOffsetMeters=9.5f}),
            Case("rolling-S3-entry",a with {ReferenceMeters=20,OffsetMeters=7,TargetOffsetMeters=7,SpeedMetersPerSecond=22}),
            Case("rolling-S4-exit",a with {ReferenceMeters=110,OffsetMeters=7,TargetOffsetMeters=7,SpeedMetersPerSecond=22}),
            Case("S5-low",a with {OffsetMeters=9.5f,TargetOffsetMeters=9.5f,Profile=FullHeatSoloAudit.Profile(1,legacy:new(50,20,20,50,50,50))}),
            Case("S5-high",a with {OffsetMeters=9.5f,TargetOffsetMeters=9.5f,Profile=FullHeatSoloAudit.Profile(1,legacy:new(50,80,80,50,50,50))})
        };
    }
    public static Scenario[] Heats()=>CloseRacingAudit.Heats().Where(s=>s.Name!="close-rotation-1").ToArray();
    public static Scenario Conflict() {
        var own=Solos()[2].Riders[0];
        return new("rolling-neighbor-conflict",new[]{own,own with {Profile=FullHeatSoloAudit.Profile(2),
            OffsetMeters=5.8f,TargetOffsetMeters=5.8f,SpeedMetersPerSecond=24,TrafficEnabled=false}},8);
    }
    public static LineRun Execute(Scenario s,bool enabled=false,bool reverse=false,string? trajectory=null) {
        var frame=new TrackFrame(CoreSim.Analysis.MatchedVenueProfiles.CreateMotoarenaStandingStartTrack());
        var observer=new PhaseObserver(frame,s.Riders);
        var evaluations=new List<AutonomousLineObservation>();
        var r=CloseRacingAudit.Execute(s,Parameters(enabled),reverse,trace:false,
            additionalDiagnostics:new(){Force=observer.Force,AutonomousLine=evaluations.Add},acceptedObserver:observer.Accept,trajectoryPath:trajectory);
        return new(r,observer.Events.ToArray(),observer.Summaries(),evaluations.ToArray());
    }
    private static void Save(string directory,string name,object value)=>File.WriteAllText(Path.Combine(directory,name+".json"),JsonSerializer.Serialize(value,Audit.Json)+"\n");
    public static void Run(string directory,string stage) {
        Directory.CreateDirectory(directory);
        if(stage=="baseline") {
            if(File.Exists(Path.Combine(directory,"baseline-seal.json")))throw new InvalidOperationException("Baseline already sealed.");
            Save(directory,"configuration",new{Baseline,Parameters=Parameters(),StepSeconds=.01,Substeps=4,Solos=Solos(),Heats=Heats(),
                Inputs=Solos().Concat(Heats()).Select(s=>new{s.Name,Riders=s.Riders.Select(r=>new{r.Profile.Id,Canonical=r.Profile.Gameplay,Legacy=r.Profile.Skills,r.ReferenceMeters,r.OffsetMeters,r.SpeedMetersPerSecond,r.TargetOffsetMeters,r.TrafficEnabled})}),
                Surface=TrackSurfaceState.Default,Replay="existing HeatMeasurements CSV accepted states at .1s, decisions and consequences in JSON"});
            foreach(var s in Solos())SaveRun(s,false);
            // Exactly two additional fixed inner-line controls; no search for a winning profile.
            foreach(var s in Solos().Where(s=>s.Name.StartsWith("S5-",StringComparison.Ordinal)))
                SaveRun(s with{Name=s.Name+"-inner-control",Riders=s.Riders.Select(r=>r with{OffsetMeters=.5f,TargetOffsetMeters=.5f}).ToArray()},false);
            foreach(var s in Heats()) {
                var r=SaveRun(s,false);
                var old=JsonSerializer.Deserialize<GapRun>(File.ReadAllText(Path.Combine("docs/experiments/close-racing-following-gap",s.Name+"-baseline.json")),Audit.Json)!;
                if(r.Run.Result.Fingerprint!=old.Result.Fingerprint||!r.Run.Result.Decisions.SequenceEqual(old.Result.Decisions))throw new InvalidOperationException("#94 mismatch.");
            }
            Seal(directory);return;
        }
        if(stage=="focus") {VerifySeal(directory);SaveRun(Solos()[2],true);return;}
        if(stage=="solo") {VerifySeal(directory);foreach(var s in Solos().Where(s=>s.Name!=Solos()[2].Name))SaveRun(s,true);return;}
        if(stage=="heats") {VerifySeal(directory);foreach(var s in Heats())SaveRun(s,true);return;}
        if(stage=="conflicts") {Save(directory,"conflicts",Conflicts());return;}
        if(stage=="benchmark") {Benchmark(directory);return;}
        if(stage=="pack-windows") {
            var rows=new List<object>();
            foreach(var s in Heats().Where(CloseRacingAudit.IsRolling))foreach(var enabled in new[]{false,true}) {
                using var w=new WorldRunner(s.Riders,parameters:Parameters(enabled));var next=0d;
                var result=w.Run(s.Name,.01,45,s.Laps,state=>{
                    if(state.TimeSeconds+1e-8<next)return;next=state.TimeSeconds+5;
                    rows.Add(new{s.Name,Enabled=enabled,State=state,
                        ReferenceSpreadMeters=state.Riders.Max(v=>v.TotalReferenceMeters)-state.Riders.Min(v=>v.TotalReferenceMeters)});
                },stopOnCrash:true,lapsFromInitialPosition:true);
                if(result.Contacts.Count>0)throw new InvalidOperationException("Unexpected prefix contact.");
            }
            Save(directory,"pack-windows",rows);return;
        }
        if(stage=="replay") {
            VerifySeal(directory);var rows=new List<object>();
            foreach(var s in new[]{Solos()[2],Heats().Single(s=>s.Name=="rolling-straight")}) {
                var path=Path.Combine(directory,s.Name+"-accepted.csv");
                var r=Execute(s,true,reverse:s.Riders.Length>1,trajectory:path);
                var old=JsonSerializer.Deserialize<LineRun>(File.ReadAllText(Path.Combine(directory,s.Name+"-ON.json")),Audit.Json)!;
                var hash=r.Run.Result.Fingerprint==old.Run.Result.Fingerprint;
                var decisions=r.Run.Result.Decisions.SequenceEqual(old.Run.Result.Decisions);
                var contacts=r.Run.Result.Contacts.SequenceEqual(old.Run.Result.Contacts);
                if(!(hash&&decisions&&contacts))throw new InvalidOperationException("Replay mismatch.");
                rows.Add(new{s.Name,File=Path.GetFileName(path),Reverse=s.Riders.Length>1,ExactHash=hash,ExactDecisions=decisions,ExactContacts=contacts,r.Run.Result.Fingerprint});
            }
            Save(directory,"replays",rows);return;
        }
        throw new ArgumentException("Use baseline, focus, solo, heats, conflicts, benchmark or replay.");
        LineRun SaveRun(Scenario s,bool enabled) {
            var name=s.Name+(enabled?"-ON":"-OFF");if(File.Exists(Path.Combine(directory,name+".json")))throw new InvalidOperationException("Do not overwrite evidence.");
            var r=Execute(s,enabled);Save(directory,name,r);
            Console.WriteLine($"{name}: {r.Run.Status}; time={r.Run.Result.TimeSeconds:F3}, contacts={r.Run.Result.Work.Episodes}");return r;
        }
    }
    private static void Seal(string directory)=>Save(directory,"baseline-seal",new{Baseline,CreatedUtc=DateTime.UtcNow,
        Files=Directory.GetFiles(directory,"*.json").OrderBy(x=>x,StringComparer.Ordinal).Select(path=>new{File=Path.GetFileName(path),Sha256=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)))})});
    public static void VerifySeal(string directory) {
        using var doc=JsonDocument.Parse(File.ReadAllText(Path.Combine(directory,"baseline-seal.json")));
        foreach(var file in doc.RootElement.GetProperty("Files").EnumerateArray()) {
            var path=Path.Combine(directory,file.GetProperty("File").GetString()!);
            if(Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)))!=file.GetProperty("Sha256").GetString())
                throw new InvalidOperationException("Changed frozen baseline.");
        }
    }
    public static object Conflicts() {
        var blocked=Execute(Conflict(),true);
        var own=Solos()[2].Riders[0];var attacking=own with{TargetOffsetMeters=8.5f};
        using var w=new WorldRunner(new[]{attacking,own with{Profile=FullHeatSoloAudit.Profile(2),ReferenceMeters=40,OffsetMeters=8.5f,TargetOffsetMeters=8.5f,SpeedMetersPerSecond=20}},parameters:Parameters(true));
        var state=w.Snapshot with{TimeSeconds=1};var view=state.Riders[0];var work=new WorldWork();
        var memory=new TacticalMemory{HomeOffsetMeters=7,AutonomousOffsetMeters=7,Intent=RacingIntent.AttackOutside,OpponentId=2,ActivatedAt=.7,NextTacticAt=1.5};
        var surface=TrackState.CreateDefault(w.Frame.Track).Snapshot();
        var actual=TrafficDecision.Observe(state,view,attacking,w.Parameters,work,memory,TrafficDecision.ObservePairs(state,work),w.Frame,surface);
        var free=AutonomousRacingLine.Observe(state with{Riders=new[]{view}},view,attacking,w.Parameters,new(),
            new(){AutonomousOffsetMeters=7},w.Frame,surface,(_,_,_)=>true);
        if(actual is not null||memory.Intent!=RacingIntent.AttackOutside||free is null||free.TargetOffsetMeters>=view.TargetOffsetMeters)
            throw new InvalidOperationException("Autonomy displaced an active attack or counterfactual not opposite.");
        return new{BlockedNeighbor=blocked,ActiveAttackSnapshot=new{state,CurrentIntent=memory.Intent,AutonomousIfFree=free,
            Actual=actual,PreservedActiveAttack=true,Meaning="read-only snapshot comparison, not an authored world command"}};
    }
    private static void Benchmark(string directory) {
        var rows=new List<object>();
        foreach(var s in new[]{Solos()[1],Heats()[1],Heats().Single(s=>s.Name=="rolling-straight")})
        foreach(var enabled in new[]{false,true})for(var n=-1;n<3;n++) {
            var bytes=GC.GetAllocatedBytesForCurrentThread();var watch=Stopwatch.GetTimestamp();RunResult result;
            using(var w=new WorldRunner(s.Riders,parameters:Parameters(enabled)))
                result=w.Run(s.Name,.01,s.DurationSeconds,s.Laps,stopOnCrash:true,lapsFromInitialPosition:CloseRacingAudit.IsRolling(s));
            var elapsed=Stopwatch.GetElapsedTime(watch).TotalMilliseconds;
            if(n>=0)rows.Add(new{s.Name,Enabled=enabled,Sample=n,ElapsedMilliseconds=elapsed,AllocatedBytes=GC.GetAllocatedBytesForCurrentThread()-bytes,
                result.TimeSeconds,result.Fingerprint,result.Work});
        }
        Save(directory,"performance",rows);
    }
}
public sealed record PhaseEvent(int RiderId,int Lap,string Phase,double TimeSeconds,float Speed,double OffsetMeters,float TargetMeters,float Drive);
public sealed record ForceSummary(int RiderId,double PositiveTangentialDeltaV,double NegativeTangentialDeltaV,double AppliedWorkJoules,
    double MinimumCornerSpeed,double MaximumGripRatio,double CorrectionSeconds);
public sealed record LineRun(GapRun Run,PhaseEvent[] Phases,ForceSummary[] Force,AutonomousLineObservation[]? Evaluations=null);
internal sealed class PhaseObserver
{
    private readonly TrackFrame frame;private readonly RiderSpec[] specs;
    private readonly Dictionary<int,ForceObservation> forces=new();
    private readonly Dictionary<int,double[]> totals;
    private readonly Dictionary<int,double> exitProgress=new();
    public List<PhaseEvent> Events {get;}=new();
    public PhaseObserver(TrackFrame frame,RiderSpec[] specs) {
        this.frame=frame;this.specs=specs;totals=specs.ToDictionary(s=>s.Profile.Id,_=>new[]{0d,0d,0d,double.PositiveInfinity,0d,0d});
    }
    public void Force(ForceObservation force)=>forces[force.Rider.Id]=force;
    public void Accept(WorldSnapshot state) {
        foreach(var v in state.Riders) {
            var force=forces[v.Id];var old=force.Rider;if(old.Activity is RiderActivity.Finished or RiderActivity.Crashed)continue;
            var dt=state.TimeSeconds-force.TimeSeconds;var a=totals[v.Id];var spec=specs.Single(s=>s.Profile.Id==v.Id);
            var mass=spec.Profile.Gameplay.Physical.MassKg+77;
            a[force.TangentialNewtons>=0?0:1]+=force.TangentialNewtons/mass*dt;
            a[2]+=Vector2.Dot(force.ForceNewtons,old.Velocity)*dt;
            if(v.Road.IsTurn)a[3]=Math.Min(a[3],v.Speed);
            if(force.GripBudgetNewtons is >0)a[4]=Math.Max(a[4],force.ForceNewtons.Length()/force.GripBudgetNewtons.Value);
            if(force.TangentialNewtons<0)a[5]+=dt;
            string? phase=null;
            if(!old.Road.IsTurn&&v.Road.IsTurn)phase="Entry";
            if(old.Road.IsTurn&&v.Road.IsTurn&&old.Road.CornerProgress<.5&&v.Road.CornerProgress>=.5)phase="Apex";
            if(old.Road.IsTurn&&!v.Road.IsTurn){phase="Exit";exitProgress[v.Id]=v.TotalReferenceMeters;}
            if(exitProgress.TryGetValue(v.Id,out var exit)&&v.TotalReferenceMeters>=exit+20){phase="Exit+20m";exitProgress.Remove(v.Id);}
            if(phase is null)continue;
            // Geometry events use the fixed track-reference circuit index; rolling
            // timing itself retains its separate per-rider initial-progress origin.
            var lap=(int)Math.Floor(v.TotalReferenceMeters/frame.LapMeters)+1;
            Events.Add(new(v.Id,lap,phase,state.TimeSeconds,v.Speed,v.Road.OffsetMeters,v.TargetOffsetMeters,v.Drive));
        }
    }
    public ForceSummary[] Summaries()=>totals.Select(p=>new ForceSummary(p.Key,p.Value[0],p.Value[1],p.Value[2],p.Value[3],p.Value[4],p.Value[5])).ToArray();
}
