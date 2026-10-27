using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
namespace SharedWorldSpeedway;

/// <summary>Opt-in, resumable experiment. Reservations survive failures; no retries or policy fitting.</summary>
public static class Experiment141
{
    public const string BaseHead="932fc33ba20a8c96c2cd1a102957c9b297d412ea";
    public static readonly JsonSerializerOptions Json=FreeLearningExperiment.Json;
    static JsonNode Node(object x)=>JsonSerializer.SerializeToNode(x,Json)!;
    static string Hash(string s)=>Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(s.Replace("\r\n","\n"))));
    static Dictionary<string,string> Sources()=>Directory.GetFiles("tools/shared-world-speedway","*.cs",SearchOption.AllDirectories)
        .Concat(Directory.GetFiles("tools/autonomous-acceleration").Where(x=>x.EndsWith(".cs",StringComparison.Ordinal)||x.EndsWith(".py",StringComparison.Ordinal)||x.EndsWith(".csproj",StringComparison.Ordinal)))
        .Where(x=>!x.Replace('\\','/').Contains("/obj/")&&!x.Replace('\\','/').Contains("/bin/")).Order().ToDictionary(x=>x.Replace('\\','/'),x=>Hash(File.ReadAllText(x)));
    static void Save(string path,JsonObject root){File.WriteAllText(path+".tmp",root.ToJsonString(Json));File.Move(path+".tmp",path,true);}
    static FreeRacingSample[] HistoricalPrefix(double end){
        var old=RobustLearningExperiment.ReadEpisode("docs/experiments/motorcycle-physics-validation/native","off-D0");
        var saved=old.Replay.Samples.Where(s=>s.Input.Time<=end+1e-8).ToArray();
        // The long capture annotates each sample before its NEXT accepted interval.
        // A short capture's final sample has no next interval: exclude only a request
        // at the endpoint, which has not been observed by the short world yet.
        var d=old.Replay.Decisions.Last(x=>x.Time<end-1e-8);var last=saved[^1];
        saved[^1]=last with{Input=last.Input with{Pending=last.Input.Pending.Where(x=>x.ObservedAt<end-1e-8).ToArray(),NextReview=d.Time+.2},RequestedSteering=d.Action.SteeringRadians,RequestedDrive=d.Action.Drive};
        return saved;
    }
    static JsonArray Attempts(JsonObject r)=>(JsonArray)r["Reservations"]!;
    static double Cpu(JsonObject r)=>r["Audit"]!["CpuSeconds"]!.GetValue<double>()+Attempts(r).Sum(x=>x!["CpuSeconds"]!.GetValue<double>());
    static int Reserve(JsonObject r,string path,string name){
        if(Attempts(r).Any(x=>x!["Name"]!.GetValue<string>()==name||!x["Complete"]!.GetValue<bool>()))throw new IOException("Duplicate or incomplete reservation: never rerun.");
        if(Attempts(r).Count>=20||Cpu(r)>320)throw new IOException("20 worlds / 400 CPU-s, retaining 80 CPU-s safety.");
        Attempts(r).Add(Node(new{Name=name,NativeWorlds=1,Complete=false,CpuSeconds=0d,ContactEvaluations=0L,Forecasts=0L}));Save(path,r);return Attempts(r).Count-1;
    }
    static void Cost(JsonObject r){r["Cost"]=Node(new{NewNativeWorlds=Attempts(r).Count,CpuSeconds=Cpu(r),
        AuditCpuSeconds=r["Audit"]!["CpuSeconds"]!.GetValue<double>(),
        EpisodeAndForecastCpuSeconds=Attempts(r).Sum(x=>x!["CpuSeconds"]!.GetValue<double>()),
        Contacts=Attempts(r).Sum(x=>x!["ContactEvaluations"]!.GetValue<long>()),Forecasts=Attempts(r).Sum(x=>x!["Forecasts"]!.GetValue<long>()),
        Meaning="Includes chooser forecasts, native world, compact summaries and historical prefix reads; audit reads counted conservatively too. Build/tests excluded: no new forecasts/worlds there.",Limits=new{Worlds=20,CpuSeconds=400}});}
    public static void Run(string path,string stage){
        Directory.SetCurrentDirectory(Experiment140.RepoRoot());path=Path.GetFullPath(path);Directory.CreateDirectory("work");
        using var lease=new FileStream("work/acceleration141.lock",FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None);
        var r=JsonNode.Parse(File.ReadAllText(path))!.AsObject();
        if(stage=="prefix-audit"){
            // Read-only correction of the export comparison; never reruns a world.
            if(Attempts(r).Count!=1||r["Stopped"]?.GetValue<string>()!="Native guard, clock, ledger or original-prefix gate: preflight-A")throw new IOException("Only the single stopped original preflight may be audited.");
            var cpu=Process.GetCurrentProcess().TotalProcessorTime;var row=r["Native"]![0]!;
            var normalizedHash=Hash(JsonSerializer.Serialize(HistoricalPrefix(3),Json));
            if(normalizedHash!=row["NewPrefixSha256"]!.GetValue<string>())throw new IOException("Difference exceeds endpoint annotation; stop remains.");
            r["CollectorBoundaryCorrection"]=Node(new{NativeWorlds=0,NewForecasts=0,
                Meaning="Original long capture had already annotated decision3.00s; short capture ends before observing it. Removing only endpoint command annotation from saved historical prefix makes its entire serialized SHA256 equal the new capture. No physical state, earlier command, score, gate threshold or controller changed.",
                PreviousHistoricalSha256=row["HistoricalPrefixSha256"]!.GetValue<string>(),NormalizedHistoricalSha256=normalizedHash,
                PreviousHostSourceSha256=r["Protocol"]!["Sources"]!["tools/shared-world-speedway/AutonomousAcceleration141/Experiment141.cs"]!.GetValue<string>()});
            row["HistoricalPrefixSha256"]=normalizedHash;row["ExactHistoricalPrefix"]=true;row["NumericalAndClockGatesPassed"]=true;
            r["Protocol"]!["Sources"]!["tools/shared-world-speedway/AutonomousAcceleration141/Experiment141.cs"]=Hash(File.ReadAllText("tools/shared-world-speedway/AutonomousAcceleration141/Experiment141.cs"));
            Attempts(r)[0]!["CpuSeconds"]=Attempts(r)[0]!["CpuSeconds"]!.GetValue<double>()+(Process.GetCurrentProcess().TotalProcessorTime-cpu).TotalSeconds;
            r.Remove("Stopped");r.Remove("Verdict");Cost(r);Save(path,r);Console.WriteLine("Exact prefix confirmed after endpoint annotation normalization, zero new worlds/forecasts.");return;
        }
        if(stage=="freeze"){
            if(r["Protocol"] is not null||Attempts(r).Count>0)throw new IOException("Cannot overwrite a frozen experiment.");
            r["Protocol"]=Node(new{BaseHead,DefaultEnabled=false,ProductionChanged=false,Policy=PhysicsExperiment139.Policy,
                Controller="One prototype: remove throttle prior, replace terminal yaw with existing mean yaw; best per original gas plus original134A fallback. No weights fitted.",
                Preparation="For <=4 roots reuse H09 endpoint, one additional H09 coast/inverse-steering recovery forecast each. Original point forces, queue and actuator. Geometry estimates beyond H1.8 use attained offset and measured coast deceleration; reaction, actual steering travel, lateral+slip-yaw response consume available time. Relative residual-speed gate vs original134A. Uncertified estimate; no native future input.",
                Actions="Original 0 / 0.318... / 1 and original steering set; only first request issued, .24s irrevocable pending, .2s reviews, +/-35deg,90deg/s, beta45deg. No native intervention, reset or suffix commitment.",
                HorizonSeconds=.9,RecoverySeconds=.9,PreparationToleranceMetersPerSecond=.001,
                Cases=PhysicsExperiment139.Cases(),Scope="One continuous original WorldRunner through studied exit, next62m, entire NEXT bend and another62m. D1/H1 include bend3. Historical two-bend+1s gate separate.",
                Gates=new{AllFourLegalWhenBaselineLegal=true,NewDnf=0,PhysicallyDistinctFamilies=2,MinimumSpeedGain40Kph=3,MinimumSameFamilyFullTimeGainSeconds=.10,MaximumMeanDecisionCpuRatio=1.5,MaximumBMeanDecisionCpuSeconds=.20},
                Allocation="2 preflight worlds D0 A/B3s; then4 diagnostic D0/D1 A/B; commit held lock;4 confirmation H0/H1 A/B. Stop on preflight guard or CPU failure. No retuning.",
                Limits=new{NewWorlds=20,CpuSeconds=400,PerEpisodeCpuSeconds=40},
                Confirmation="H families withheld from new native141 until lock; previously used137-140, not a new empirical dataset.",Sources=Sources(),FrozenBeforeAnyNewWorld=true});
            Cost(r);Save(path,r);return;
        }
        foreach(var s in r["Protocol"]!["Sources"]!.AsObject())if(Hash(File.ReadAllText(s.Key))!=s.Value!.GetValue<string>())throw new IOException("Frozen source changed: "+s.Key);
        if(r["Stopped"] is not null)throw new IOException("Experiment stopped: "+r["Stopped"]);
        if(Attempts(r).Any(x=>!x!["Complete"]!.GetValue<bool>()))throw new IOException("Incomplete reservation retained; no retry.");
        if(stage=="preflight"){
            foreach(var mode in new[]{"A","B"})if(!Native(r,path,PhysicsExperiment139.Cases()[0],mode,"preflight-"+mode,3))return;
            var rows=(JsonArray)r["Native"]!;var a=rows.First(x=>x!["Name"]!.GetValue<string>()=="preflight-A")!;var b=rows.First(x=>x!["Name"]!.GetValue<string>()=="preflight-B")!;
            var ratio=b["DecisionCpu"]!["MeanSeconds"]!.GetValue<double>()/a["DecisionCpu"]!["MeanSeconds"]!.GetValue<double>();
            var ok=ratio<=1.5&&b["DecisionCpu"]!["MeanSeconds"]!.GetValue<double>()<=.20;
            r["Preflight"]=Node(new{Passed=ok,CpuRatio=ratio,Meaning="Original prefix parity and physical guards plus frozen CPU gate;3s does not certify later bends."});
            if(!ok){r["Stopped"]="Preflight CPU gate";r["Verdict"]="INCONCLUSIVE";}Save(path,r);return;
        }
        if(r["Preflight"]?["Passed"]?.GetValue<bool>()!=true)throw new IOException("Passed preflight required.");
        var cases=PhysicsExperiment139.Cases();
        if(stage=="diagnostic"){foreach(var c in cases.Take(2))foreach(var m in new[]{"A","B"})if(!Native(r,path,c,m,c.Name+"-"+m))return;}
        else if(stage=="confirmation"){
            if(r["ConfirmationLock"] is null)throw new IOException("Commit confirmation lock first.");
            foreach(var c in cases.Skip(2))foreach(var m in new[]{"A","B"})if(!Native(r,path,c,m,c.Name+"-"+m))return;
        }else throw new ArgumentException("freeze|preflight|diagnostic|confirmation");
    }
    static bool Native(JsonObject r,string path,NativeCase139 c,string mode,string name,double? shortSeconds=null){
        var rows=(JsonArray)r["Native"]!;if(rows.Any(x=>x!["Name"]!.GetValue<string>()==name))return true;
        var reservation=Reserve(r,path,name);var cpu=Process.GetCurrentProcess().TotalProcessorTime;
        var frame=TrackAwarenessExperiment134.Frame(c.Case.Geometry);var surface=MultiActionExperiment135.Surface(frame);var spec=FreeLearningExperiment.Rider(c.Case.Scenario);var p=FreeLearningExperiment.Parameters();
        var decisions=new List<object>();var timings=new List<double>();
        FreeLearningDecision Choose(FreeRacingInput input){
            var start=Process.GetCurrentProcess().TotalProcessorTime;AccelerationChoice141? choice=null;
            var d=mode=="A"?MultiActionPlanner135.Baseline(input,PhysicsExperiment139.Policy,spec,frame,surface,p)
                :(choice=Controller141.Choose(input,PhysicsExperiment139.Policy,spec,frame,surface,p)).Decision;
            var elapsed=(Process.GetCurrentProcess().TotalProcessorTime-start).TotalSeconds;timings.Add(elapsed);
            decisions.Add(new{input.Time,ReferenceMeters=input.Rider.TotalReferenceMeters,SpeedMetersPerSecond=input.Rider.Speed,input.Active,input.Pending,
                Request=d.Action,d.Safety,CpuSeconds=elapsed,OriginalAction=choice?.OriginalChoice.Action,choice?.Changed,choice?.Reason,
                Preparation=choice?.Preparation,OriginalPreparation=choice?.OriginalPreparation,
                GasRoots=choice is not null&&(decisions.Count%5==0||choice.Changed)?choice.GasRoots:null});return d;
        }
        NativeCaptureResult141 capture;
        try{capture=NativeCapture141.Execute(name,16015,PhysicsExperiment139.Policy,c.Case.Scenario,shortSeconds:shortSeconds,twoBends:true,horizonSeconds:.9,
            normalizeProgress:true,fullThrottleCandidate:true,trackShape:TrackAwarenessExperiment134.Variant(c.Case.Geometry),chooser:Choose,extendAfterSecondStraight:shortSeconds is null,studiedCorner:c.Corner);}
        catch(Exception ex){var a=Attempts(r)[reservation]!;a["CpuSeconds"]=(Process.GetCurrentProcess().TotalProcessorTime-cpu).TotalSeconds;
            a["Error"]=ex.Message;r["Stopped"]="Incomplete world retained: "+ex.Message;r["Verdict"]="INCONCLUSIVE";Cost(r);Save(path,r);throw;}
        var e=capture.Episode;var samples=e.Replay.Samples;var metrics=e.Replay.Metrics;
        var summary=Node(NativeSummary140.Build(new(e,capture.HistoricalCompleted,capture.HistoricalGateSeconds,capture.NativeContacts,capture.BoundaryContacts),c,mode,false,null,null,false));
        summary["Controller"]=mode=="A"?"frozen134A #16015":"autonomous141 prototype";
        var sameCaseA=rows.FirstOrDefault(x=>x!["Case"]!["Name"]!.GetValue<string>()==c.Name&&x["Mode"]!.GetValue<string>()=="A");
        var initial=summary["Initial"]!;
        summary["ExactInitialPhysicalState"]=sameCaseA is null?null:Node(new[]{"Rider","Omega","Steering","Active"}.All(k=>initial[k]!.ToJsonString(Json)==sameCaseA["Initial"]![k]!.ToJsonString(Json)));
        // Keep normal report compact; one peak and last-two failure observations retain the diagnostic forces.
        foreach(var point in new[]{"Exit","Straight20Meters","Straight40Meters","Straight62Meters"})if(summary[point] is JsonObject o){o.Remove("PreviousIntervalForces");o.Remove("PreviousIntervalWheel");}
        summary["NextCornerEndReferenceMeters"]=capture.NextCornerEndReference;summary["StopReferenceMeters"]=capture.StopReference;
        summary["FullScopeCertified"]=metrics.Completed;summary["DNF"]=metrics.Outcome is not "completed" and not "probe";
        summary["DecisionCpu"]=Node(new{Count=timings.Count,TotalSeconds=timings.Sum(),MeanSeconds=timings.Average(),MaximumSeconds=timings.Max(),Timer="Process.TotalProcessorTime; quantized native timer. Includes every original/prototype forecast and inverse query."});
        summary["Decisions"]=Node(decisions);
        var exit=SpeedThrottleAudit136.Exit(frame,c.Corner);var nextStart=exit+62;
        var straight=samples.Zip(samples.Skip(1),(a,b)=>new{a,b}).Where(x=>x.a.Input.Rider.TotalReferenceMeters>=exit&&x.a.Input.Rider.TotalReferenceMeters<nextStart).ToArray();
        var straightTime=straight.Sum(x=>x.b.Input.Time-x.a.Input.Time);
        summary["StudiedStraight"]=Node(new{ObservedSeconds=straightTime,FullGasTimeFraction=straightTime>0?straight.Where(x=>x.a.Input.Active.Drive>=.99).Sum(x=>x.b.Input.Time-x.a.Input.Time)/straightTime:(double?)null,
            CertifiedTo62Meters=samples[^1].Input.Rider.TotalReferenceMeters>=nextStart});
        var next=samples.Where(x=>x.Input.Rider.TotalReferenceMeters>=nextStart&&x.Input.Rider.TotalReferenceMeters<=capture.NextCornerEndReference).ToArray();
        summary["NextCorner"]=Node(new{Entered=next.Length>0,Completed=samples[^1].Input.Rider.TotalReferenceMeters>=capture.NextCornerEndReference,
            MinimumSpeedMetersPerSecond=next.Length>0?next.Min(x=>x.Input.Rider.Speed):(double?)null,ObservedSeconds=next.Length>1?next[^1].Input.Time-next[0].Input.Time:0});
        var previous=new FreeRacingAction(0,1);FreeLearningDecision? reduction=null;
        foreach(var d in e.Replay.Decisions){if(d.Time>=samples.FirstOrDefault(x=>x.Input.Rider.TotalReferenceMeters>=exit)?.Input.Time&&d.Action.Drive<previous.Drive-1e-6){reduction=d;break;}previous=d.Action;}
        if(reduction is not null){var request=samples.First(x=>x.Input.Time>=reduction.Time-1e-8);var activation=samples.FirstOrDefault(x=>x.Input.Time>=reduction.Time+.24-1e-8);
            var travel=samples.Zip(samples.Skip(1),(a,b)=>new{a,b}).Where(x=>x.a.Input.Time>=reduction.Time-1e-8&&x.b.Input.Time<=reduction.Time+.24+1e-8)
                .Sum(x=>System.Numerics.Vector2.Distance(x.a.Input.Rider.Position,x.b.Input.Rider.Position));
            summary["FirstStraightReduction"]=Node(new{RequestTimeSeconds=reduction.Time,ActivationTimeSeconds=activation?.Input.Time,reduction.Action,
                ReferenceMeters=request.Input.Rider.TotalReferenceMeters,DistanceBeforeNextBendMeters=nextStart-request.Input.Rider.TotalReferenceMeters,ReactionTravelMeters=travel});}
        var pass=summary["EnergyLedgerPassed"]!.GetValue<bool>()&&summary["MaximumYawUpdateErrorRadiansPerSecond"]!.GetValue<double>()<=2e-5
            &&summary["MaximumReactionErrorSeconds"]!.GetValue<double>()<1e-8&&summary["MaximumReviewIntervalErrorSeconds"]!.GetValue<double>()<1e-8;
        if(summary["ExactInitialPhysicalState"] is not null)pass&=summary["ExactInitialPhysicalState"]!.GetValue<bool>();
        if(shortSeconds is not null){pass&=metrics.Outcome=="probe";
            if(mode=="A"){
                var saved=HistoricalPrefix(shortSeconds.Value);
                var actual=JsonSerializer.Serialize(samples,Json);var historical=JsonSerializer.Serialize(saved,Json);var same=actual==historical;
                summary["ExactHistoricalPrefix"]=same;summary["HistoricalPrefixSha256"]=Hash(historical);summary["NewPrefixSha256"]=Hash(actual);pass&=same;
            }
        }
        summary["NumericalAndClockGatesPassed"]=pass;rows.Add(summary);
        var attempt=Attempts(r)[reservation]!;attempt["Complete"]=true;attempt["CpuSeconds"]=(Process.GetCurrentProcess().TotalProcessorTime-cpu).TotalSeconds;
        attempt["ContactEvaluations"]=metrics.PhysicsEvaluations;attempt["Forecasts"]=metrics.Forecasts;
        Cost(r);if(!pass){r["Stopped"]="Native guard, clock, ledger or original-prefix gate: "+name;r["Verdict"]=summary["DNF"]!.GetValue<bool>()?"FAIL":"INCONCLUSIVE";}
        Save(path,r);Console.WriteLine($"{name}: {metrics.Outcome}, duration={metrics.DurationSeconds:F3}, beta={metrics.MaximumBetaDegrees:F3}, decisionCPU={timings.Average():F4}, CPU={attempt["CpuSeconds"]}");return pass;
    }
}
