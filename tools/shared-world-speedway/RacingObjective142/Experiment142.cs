using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;
namespace SharedWorldSpeedway;

public static class Experiment142
{
    public const string BaseHead="266df048536c256ced410a1045a5924899153237";
    static readonly JsonSerializerOptions Json=FreeLearningExperiment.Json;
    static JsonNode Node(object o)=>JsonSerializer.SerializeToNode(o,Json)!;
    static string Hash(string s)=>Offline142.Hash(s);
    static void Save(string path,JsonObject r){File.WriteAllText(path+".tmp",r.ToJsonString(Json));File.Move(path+".tmp",path,true);}
    static Dictionary<string,string> Sources()=>Directory.GetFiles("tools/shared-world-speedway","*.cs",SearchOption.AllDirectories)
        .Concat(Directory.GetFiles("tools/racing-objective-ablation").Where(x=>x.EndsWith(".cs",StringComparison.Ordinal)||x.EndsWith(".py",StringComparison.Ordinal)||x.EndsWith(".csproj",StringComparison.Ordinal)))
        .Where(x=>!x.Replace('\\','/').Contains("/obj/")&&!x.Replace('\\','/').Contains("/bin/")).Order().ToDictionary(x=>x.Replace('\\','/'),x=>Hash(File.ReadAllText(x)));
    static JsonArray Attempts(JsonObject r)=>(JsonArray)r["Reservations"]!;
    static double Used(JsonObject r)=>r["Audit"]!["CpuSeconds"]!.GetValue<double>()+Attempts(r).Sum(x=>x!["CpuSeconds"]!.GetValue<double>());
    static void Cost(JsonObject r)=>r["Cost"]=Node(new{NewNativeWorlds=Attempts(r).Count,CpuSeconds=Used(r),
        EpisodeAndForecastCpuSeconds=Attempts(r).Sum(x=>x!["CpuSeconds"]!.GetValue<double>()),OfflineReadCpuSeconds=r["Audit"]!["CpuSeconds"]!.GetValue<double>(),
        Forecasts=Attempts(r).Sum(x=>x!["Forecasts"]!.GetValue<long>()),Contacts=Attempts(r).Sum(x=>x!["Contacts"]!.GetValue<long>()),
        Includes="Every native episode including prefixes, every H09 forecast, summary and historical reads. Offline reads conservatively counted. Builds/regression tests excluded; new unit tests use no forecasts/worlds.",Limits=new{Worlds=24,CpuSeconds=450}});
    static int Reserve(JsonObject r,string path,string name){
        if(Attempts(r).Any(x=>x!["Name"]!.GetValue<string>()==name||!x["Complete"]!.GetValue<bool>()))throw new IOException("Duplicate or incomplete reservation; never retry.");
        if(Attempts(r).Count>=24||Used(r)>440)throw new IOException("Total budget exhausted; retain10s for finalization.");
        Attempts(r).Add(Node(new{Name=name,Complete=false,CpuSeconds=0d,Forecasts=0L,Contacts=0L}));Save(path,r);return Attempts(r).Count-1;
    }
    static IEnumerable<JsonNode> Branches(JsonObject r)=>((JsonArray)r["Audit"]!["BranchStates"]!).Select(x=>x!);
    static JsonNode? Row(JsonObject r,string name)=>((JsonArray)r["Native"]!).FirstOrDefault(x=>x!["Name"]!.GetValue<string>()==name);
    public static void Run(string path,string stage){
        Directory.SetCurrentDirectory(Experiment140.RepoRoot());path=Path.GetFullPath(path);Directory.CreateDirectory(Path.GetDirectoryName(path)!);Directory.CreateDirectory("work");
        using var lease=new FileStream("work/objective142.lock",FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None);
        if(stage=="offline"){
            if(File.Exists(path))throw new IOException("Offline audit already exists; no repeat.");Save(path,Offline142.Run());return;
        }
        var r=JsonNode.Parse(File.ReadAllText(path))!.AsObject();
        if(stage=="freeze-boundary-audit"){
            if(r["BoundaryDiagnosticExtension"] is not null||Attempts(r).Count!=22)throw new IOException("One bounded two-world boundary diagnostic extension only.");
            const string host="tools/shared-world-speedway/RacingObjective142/Experiment142.cs";
            foreach(var s in r["Protocol"]!["Sources"]!.AsObject())if(s.Key!=host&&Hash(File.ReadAllText(s.Key))!=s.Value!.GetValue<string>())throw new IOException("Policy/physics/source changed outside diagnostic dispatcher: "+s.Key);
            r["BoundaryDiagnosticExtension"]=Node(new{FrozenBeforeTwoAdditionalWorlds=true,PreviousHostSha256=r["Protocol"]!["Sources"]![host]!.GetValue<string>(),
                Cases=new[]{"D0-B","H1-B"},Purpose="Resolve the two ambiguous conservative native swept-certificate stops using existing RobustSweepCertificate.Refine. Same original B requests, worlds and point forces; only observation certificate refined. Never promote main results or count repetitions as independent held confirmations.",
                MaximumAdditionalWorlds=2,MaximumTotalWorlds=24,NoObjectiveOrPhysicsChange=true,OriginalMainResultsRetained=true});
            r["Protocol"]!["Sources"]=Node(Sources());Save(path,r);return;
        }
        if(stage=="clock-audit"){
            var failed=Attempts(r).Last();if(failed?["Error"]?.GetValue<string>()!="Historical branch state/pending mismatch."||failed["Complete"]!.GetValue<bool>())throw new IOException("Only the retained comparison abort can be audited.");
            var cpu=Process.GetCurrentProcess().TotalProcessorTime;var proofs=new List<object>();long forecasts=0;
            foreach(var b in Branches(r)){
                var target=b["State"]!.Deserialize<YawState137>(Json)!;var c=PhysicsExperiment139.Cases().Single(x=>x.Case.Name==target.Case.Name);
                var original=Row(r,c.Name+"-A")!;var obs=((JsonArray)original["BranchObservations"]!).Single(x=>x!["Name"]!.GetValue<string>()==target.Name)!;
                var old=RobustLearningExperiment.ReadEpisode("docs/experiments/motorcycle-physics-validation/native","off-"+c.Name);
                var sample=old.Replay.Samples.Single(x=>Math.Abs(x.Input.Time-target.Time)<1e-8);
                var archived=RiderDecisionAudit133.BeforeDecision(sample);var callerTime=obs["Time"]!.GetValue<double>();
                var caller=archived with{Time=callerTime,NextReview=callerTime};
                var oldRaw=Hash(JsonSerializer.Serialize(caller,Json));var expected=obs["ObservedInputSha256"]!.GetValue<string>();
                if(oldRaw!=expected)throw new IOException("Reconstructed caller differs beyond clock; no continuation authorized by this audit.");
                var canonical=Clock142.StateHash(caller);if(canonical!=Clock142.StateHash(target.Input))throw new IOException("Nonclock physical/pending mismatch retained; no continuation.");
                proofs.Add(new{target.Name,AcceptedSampleTimeSeconds=archived.Time,ChooserTimeSeconds=callerTime,
                    ClockDifferenceSeconds=callerTime-archived.Time,ExactOriginalCallerHash=oldRaw,CanonicalPhysicalAndPendingHash=canonical,
                    Meaning="Entire reconstructed actual chooser input has the recorded old hash. Only time/NextReview/pending timestamps canonicalized to1e-10s for equality; no physics/state rounding or controller input change."});
                if(c.Name=="D0")forecasts=old.Replay.Decisions.Where(x=>x.Time<=target.Time+1e-8).Sum(x=>(long)x.Forecasts);
            }
            var previous= r["Protocol"]!["Sources"]!.DeepClone();var current=Sources();
            failed!["Complete"]=true;failed["AbortedBeforeIntervention"]=true;failed["Forecasts"]=forecasts;failed["ContactsUnknown"]=true;
            failed["ForecastCountMethod"]="Reconstructed from identical original A decision prefix, including failing chooser call. World was aborted before issuing the changed branch request. Native/forecast contacts not fully captured: aggregate contacts is a lower bound.";
            r["ClockComparisonCorrection"]=Node(new{NativeWorlds=0,NewForecasts=0,Proofs=proofs,PreviousFrozenSources=previous,
                AbortedWorldRetained=true,RetryDisposition="One new, separately named clock-corrected branch allowed after exact read-only proof; no completed scientific result repeated or policy tuned.",
                Changes="Comparison canonicalization and abort accounting only. Objective142, original physics, actions, safety and native scope unchanged. Original all-source manifest retained."});
            r["Protocol"]!["Sources"]=Node(current);r["Protocol"]!["DevelopmentComparisonAbortException"]=true;
            r["Audit"]!["CpuSeconds"]=r["Audit"]!["CpuSeconds"]!.GetValue<double>()+(Process.GetCurrentProcess().TotalProcessorTime-cpu).TotalSeconds;
            r.Remove("Stopped");Cost(r);Save(path,r);Console.WriteLine("Clock-only caller/sample difference proven without worlds or forecasts; aborted world's cost retained.");return;
        }
        if(stage=="freeze"){
            if(r["Protocol"] is not null||Attempts(r).Count!=0)throw new IOException("Already frozen.");
            r["Protocol"]=Node(new{BaseHead,DefaultEnabled=false,ProductionPhysicsChanged=false,Policy=PhysicsExperiment139.Policy,
                Modes=new{A="Original134A #16015 literal score and safety ranking",B="Max H09 progress, original safety class priority; no score penalties",C="Max H09 progress over pointwise nominal valid, forward, within footprint boundaries and beta45. No Uncertain priority. No admissible candidate -> controller forecast-infeasibility stop, not physical DNF."},
                Predictor="Original FreeRacingModel H09, one held forecast for each original legal action; no extra recovery forecasts or141 gate. First request only, actual .24s reaction, .2s reviews, original +/-35deg and90deg/s.",
                Safety="Predicted crossing/invalid state vs caution margins(.2m minimum,.1m closing estimate) vs provisional beta45 distinguished. Native footprint sweep, collision, reverse, energy and actuator detectors preserved. Nominal C admissibility is not a safety certificate or proof of recoverability.",
                Scope="Studied bend, next62m, entire next bend and another62m in one continuous WorldRunner; D1/H1 include third bend. No state injection, resets or authored line.",Cases=PhysicsExperiment139.Cases(),
                Allocation="3 small A/B/C preflights;12 full D0,D1,H0,H1 A/B/C worlds (terminal preflight reused if same trajectory already ended);2 changed single-penalty branch worlds with full original A main counterpart reused; up to4 diagnostic late objective switches if needed to locate loss of recovery. <=21 planned worlds, never >24.",
                Branches="Original A until selected cached observation; issue one isolated no-component request at that observation; subsequent fresh A decisions. Compare exact predecision state/pending and whole accepted prefix. Original main A is the exact unmodified branch counterpart, not another world.",
                Confirmation="D0/D1 diagnostics; commit confirmation lock before new H0/H1. No policy tuning; historical families already known, not an unseen dataset.",
                Cpu="No per-decision stopping threshold and no warm/cold feasibility claim. Only total24world/450CPU-s guard; near-budget worlds halt and report incomplete scope.",
                DecisionCriteria="Report all legal completion/time pairs, same-point exit/+20/+40/+62 speeds, drive, beta, clearance and DNF. Faster offline or faster-before-DNF is not success; four physically distinct initial families, aliases/prefixes not independent. No target speed or weight fitting.",
                Sources=Sources(),FrozenBeforeAnyNewWorld=true});Cost(r);Save(path,r);return;
        }
        foreach(var s in r["Protocol"]!["Sources"]!.AsObject())if(Hash(File.ReadAllText(s.Key))!=s.Value!.GetValue<string>())throw new IOException("Frozen source changed: "+s.Key);
        if(Attempts(r).Any(x=>!x!["Complete"]!.GetValue<bool>()))throw new IOException("Incomplete reservation retained; no retry.");
        var cases=PhysicsExperiment139.Cases();
        if(stage=="preflight"){
            foreach(var mode in new[]{"A","B","C"})Native(r,path,cases[0],mode,"preflight-"+mode,mode=="A"?3:.6);
            var a=Row(r,"preflight-A")!;
            r["Preflight"]=Node(new{Passed=a["Exact141OriginalPrefix"]!.GetValue<bool>()&&((JsonArray)r["Native"]!).All(x=>x!["NumericalClockGatesPassed"]!.GetValue<bool>()),
                Meaning="Original3s entire sample SHA matches141; B/C small probes; no decision-CPU gate and behavior DNF retained as evidence."});Save(path,r);return;
        }
        if(r["Preflight"]?["Passed"]?.GetValue<bool>()!=true)throw new IOException("Original parity and numerical preflight required.");
        if(stage=="diagnostic"||stage=="confirmation"){
            if(stage=="confirmation"&&r["ConfirmationLock"] is null)throw new IOException("Commit confirmation lock before H cases.");
            foreach(var c in stage=="diagnostic"?cases.Take(2):cases.Skip(2))foreach(var mode in new[]{"A","B","C"}){
                var pre=Row(r,"preflight-"+mode);
                if(c.Name=="D0"&&pre is not null&&pre["Outcome"]!.GetValue<string>()!="probe"){
                    if(Row(r,c.Name+"-"+mode) is null){var alias=pre.DeepClone();alias["Name"]=c.Name+"-"+mode;alias["ReusedTerminalPreflight"]=pre["Name"]!.GetValue<string>();((JsonArray)r["Native"]!).Add(alias);Save(path,r);}continue;
                }
                Native(r,path,c,mode,c.Name+"-"+mode);
            }return;
        }
        if(stage=="branches"){
            foreach(var b in Branches(r)){
                var state=b["State"]!.Deserialize<YawState137>(Json)!;var c=cases.Single(x=>x.Case.Name==state.Case.Name);
                var corrected=Attempts(r).Any(x=>x!["Name"]!.GetValue<string>()=="branch-"+c.Name&&x["AbortedBeforeIntervention"]?.GetValue<bool>()==true);
                Native(r,path,c,"A","branch-"+c.Name+(corrected?"-clock-corrected":""),null,b);
            }return;
        }
        if(stage=="recovery"){
            // Two physical families; finite late switches, no new cost or action tuning.
            foreach(var c in cases.Take(2)){
                var failed=Row(r,c.Name+"-B");if(failed is null||!failed["PhysicalDnf"]!.GetValue<bool>())continue;
                var ds=(JsonArray)failed["Decisions"]!;var first=ds.FirstOrDefault(x=>x!["HardFeasibleCount"]!.GetValue<int>()==0);
                if(first is null)continue;
                var late=first["Time"]!.GetValue<double>();
                foreach(var (label,time) in new[]{("early",Math.Max(0,late-.4)),("late",late)}){
                    if(time<1e-8){continue;} // main A is already the exact switch-at-zero counterpart
                    Native(r,path,c,"B","recovery-"+c.Name+"-"+label,null,null,time);
                }
            }return;
        }
        if(stage=="boundary-audit"){
            if(r["BoundaryDiagnosticExtension"] is null)throw new IOException("Freeze boundary diagnostic extension first.");
            foreach(var c in cases.Where(x=>x.Name is "D0" or "H1"))Native(r,path,c,"B","boundary-refined-"+c.Name,null,null,null,true);
            return;
        }
        if(stage=="analyze"){r["Analysis"]=Summary142.Compare(r);Cost(r);Save(path,r);return;}
        throw new ArgumentException("offline|freeze|preflight|diagnostic|branches|confirmation|recovery|analyze");
    }
    static void Native(JsonObject r,string path,NativeCase139 c,string mode,string name,double? shortSeconds=null,JsonNode? branch=null,double? switchAt=null,bool refinedBoundary=false){
        if(Row(r,name) is not null)return;var reservation=Reserve(r,path,name);var cpu=Process.GetCurrentProcess().TotalProcessorTime;
        var frame=TrackAwarenessExperiment134.Frame(c.Case.Geometry);var surface=MultiActionExperiment135.Surface(frame);var spec=FreeLearningExperiment.Rider(c.Case.Scenario);var p=FreeLearningExperiment.Parameters();
        var branchState=branch?["State"]!.Deserialize<YawState137>(Json);var branchMode=branch?["Mode"]!.GetValue<string>();
        var observations=new List<object>();var decisions=new List<object>();var timings=new List<double>();string? halt=null;bool branchObserved=branch is null;
        FreeLearningDecision Choose(FreeRacingInput input){
            var start=Process.GetCurrentProcess().TotalProcessorTime;
            var actualMode=switchAt is{} change&&input.Time>=change-1e-8?"A":mode;
            if(branchState is not null&&Math.Abs(input.Time-branchState.Time)<1e-8)actualMode=branchMode!;
            var choice=Objective142.Choose(input,actualMode,spec,frame,surface,p);var elapsed=(Process.GetCurrentProcess().TotalProcessorTime-start).TotalSeconds;timings.Add(elapsed);
            if(choice.Abstained)halt="controller forecast infeasible; no admissible H09 candidate";
            foreach(var b in Branches(r)){
                var target=b["State"]!.Deserialize<YawState137>(Json)!;
                if(target.Case.Name!=c.Case.Name||Math.Abs(input.Time-target.Time)>1e-8)continue;
                var observed=Clock142.StateHash(input);var cached=Clock142.StateHash(target.Input);
                observations.Add(new{target.Name,input.Time,ObservedInputSha256=observed,CachedInputSha256=cached,ExactCachedPredecision=observed==cached,
                    PendingCount=input.Pending.Length,OriginalAction=choice.Original.Candidate.Action,ChosenAction=choice.Selected?.Candidate.Action});
                if(branchState is not null){branchObserved=true;if(observed!=cached)throw new InvalidOperationException("Historical branch state/pending mismatch.");}
            }
            decisions.Add(new{input.Time,ReferenceMeters=input.Rider.TotalReferenceMeters,SpeedKph=input.Rider.Speed*3.6,BetaDegrees=input.Rider.SlideRadians*180/Math.PI,
                YawRate=input.Rider.AngularVelocity,input.Omega,input.Steering,input.Active,input.Pending,Mode=actualMode,
                Request=choice.Selected?.Candidate.Action,DiagnosticPlaceholder=choice.Abstained?input.Active:(FreeRacingAction?)null,
                Original=choice.Original.Candidate.Action,Chosen=Offline142.View(choice.Selected),choice.HardFeasibleCount,choice.Abstained,CpuSeconds=elapsed,
                OriginalScore=choice.Original.Candidate.Score,ComponentRemovals=actualMode.StartsWith("no-",StringComparison.Ordinal)?actualMode:null,
                SafeCount=choice.Leaves.Count(x=>x.Candidate.Safety==FreeActionSafety.SafeInHorizon),UncertainCount=choice.Leaves.Count(x=>x.Candidate.Safety==FreeActionSafety.Uncertain)});
            return choice.Decision;
        }
        NativeCaptureResult142 capture;
        try{capture=NativeCapture142.Execute(name,16015,PhysicsExperiment139.Policy,c.Case.Scenario,shortSeconds:shortSeconds,refinedDiagnostic:refinedBoundary,twoBends:true,horizonSeconds:.9,
            normalizeProgress:true,fullThrottleCandidate:true,trackShape:TrackAwarenessExperiment134.Variant(c.Case.Geometry),chooser:Choose,extendAfterSecondStraight:shortSeconds is null,
            studiedCorner:c.Corner,controllerHalt:()=>halt,cpuCeilingSeconds:Math.Max(1,450-Used(r)-3));}
        catch(Exception ex){Attempts(r)[reservation]!["CpuSeconds"]=(Process.GetCurrentProcess().TotalProcessorTime-cpu).TotalSeconds;
            Attempts(r)[reservation]!["Error"]=ex.Message;r["Stopped"]=ex.Message;Cost(r);Save(path,r);throw;}
        var summary=Summary142.Native(capture,c,mode);summary["Name"]=name;summary["Decisions"]=Node(decisions);summary["BranchObservations"]=Node(observations);
        summary["RefinedBoundaryDiagnostic"]=refinedBoundary;
        summary["DecisionCpu"]=Node(new{Count=timings.Count,TotalSeconds=timings.Sum(),MeanSeconds=timings.Average(),MaxSeconds=timings.Max(),
            ColdFirstSeconds=timings[0],WarmMeanSeconds=timings.Count>1?timings.Skip(1).Average():timings[0],Meaning="Process CPU quantized; cold and subsequent observed decisions, no per-decision acceptance gate."});
        var samples=capture.Episode.Replay.Samples;
        var prefixes=new List<object>();foreach(var b in Branches(r)){
            var target=b["State"]!.Deserialize<YawState137>(Json)!;if(target.Case.Name!=c.Case.Name)continue;
            var prefix=samples.Where(x=>x.Input.Time<target.Time-1e-8).ToArray();prefixes.Add(new{target.Name,PrefixBeforeBranchSha256=Hash(JsonSerializer.Serialize(prefix,Json)),Samples=prefix.Length});
        }summary["AcceptedBranchPrefixes"]=Node(prefixes);
        summary["BranchStateObserved"]=branchObserved;summary["IsolatedRemovedPenalty"]=branchMode;summary["ObjectiveSwitchAtSeconds"]=switchAt;
        if(branch is not null){
            var counterpart=Row(r,c.Name+"-A")??throw new IOException("Full original A counterpart required first.");
            summary["OriginalCounterpart"]=c.Name+"-A";
            summary["ExactAcceptedBranchPrefix"]=JsonSerializer.Serialize(prefixes,Json)==counterpart["AcceptedBranchPrefixes"]!.ToJsonString(Json);
        }
        if(shortSeconds==3&&mode=="A"){
            var old=JsonNode.Parse(File.ReadAllText("docs/experiments/autonomous-acceleration/results.json"))!;
            var sha=Hash(JsonSerializer.Serialize(samples,Json));var expected=old["Native"]![0]!["NewPrefixSha256"]!.GetValue<string>();
            summary["Exact141OriginalPrefix"]=sha==expected;summary["PrefixSha256"]=sha;summary["Historical141PrefixSha256"]=expected;
        }
        if(mode=="A"&&branch is null&&switchAt is null&&shortSeconds is null){
            var oldPath=c.Name.StartsWith("D",StringComparison.Ordinal)?"docs/experiments/motorcycle-physics-validation/native":"docs/experiments/causal-yaw-throttle/native";
            var oldName=c.Name.StartsWith("D",StringComparison.Ordinal)?"off-"+c.Name:c.BaselineName;
            var old=RobustLearningExperiment.ReadEpisode(oldPath,oldName).Replay.Samples.SkipLast(1).ToArray();
            var actual=samples.Take(old.Length).ToArray();summary["OriginalHistoricalPrefixAgreement"]=JsonSerializer.Serialize(old,Json)==JsonSerializer.Serialize(actual,Json);
        }
        var numerical=summary["NumericalClockGatesPassed"]!.GetValue<bool>();
        ((JsonArray)r["Native"]!).Add(summary);var a=Attempts(r)[reservation]!;a["Complete"]=true;a["CpuSeconds"]=(Process.GetCurrentProcess().TotalProcessorTime-cpu).TotalSeconds;
        a["Forecasts"]=capture.Episode.Replay.Metrics.Forecasts;a["Contacts"]=capture.Episode.Replay.Metrics.PhysicsEvaluations;Cost(r);Save(path,r);
        Console.WriteLine($"{name}: {capture.Episode.Replay.Metrics.Outcome}, t={capture.Episode.Replay.Metrics.DurationSeconds:F3}, beta={capture.Episode.Replay.Metrics.MaximumBetaDegrees:F2}, CPU={a["CpuSeconds"]}, total={Used(r):F3}");
        if(!numerical||!branchObserved||summary["ExactAcceptedBranchPrefix"]?.GetValue<bool>()==false||summary["OriginalHistoricalPrefixAgreement"]?.GetValue<bool>()==false
            ||summary["Exact141OriginalPrefix"]?.GetValue<bool>()==false)throw new IOException("Numerical/clock or exact historical branch/prefix gate failed; result retained, no retry.");
    }
}
