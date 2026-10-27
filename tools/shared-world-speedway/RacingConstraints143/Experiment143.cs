using System.Diagnostics;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using CoreSim;

namespace SharedWorldSpeedway;
public static class Experiment143
{
    public const string BaseHead="0da21bc92ee541b6611e19f1d4bd2e3866b283ca";
    static readonly JsonSerializerOptions Json=FreeLearningExperiment.Json;
    public static readonly Variant143[] Isolated={new("A",Array.Empty<string>()),new("B",new[]{"B"}),new("C",new[]{"C"}),new("D",new[]{"D"}),
        new("E",new[]{"E"}),new("F",new[]{"F"}),new("G",new[]{"G"}),new("H",new[]{"H"}),new("I",Array.Empty<string>(),NominalHierarchy:true),new("R",new[]{"F"},Recovery:true)};
    static NativeCase139 Case(string name,string geometry,FreeLearningScenario start,int corner,string pool)=>new(name,pool,new(name,geometry,start,true),corner,"","");
    public static readonly NativeCase139[] Selection={PhysicsExperiment139.Cases()[0],PhysicsExperiment139.Cases()[1],
        Case("S0","progressive-b",new("S0",4,14.9f,5.4f,18.55,-.075f,-.012f,.018f),1,"selection"),
        Case("S1","progressive-c",new("S1",4,18.2f,6.9f,19.05,.12f,.021f,-.027f),2,"selection")};
    public static readonly NativeCase139[] Held=MakeHeld();
    static NativeCase139[] MakeHeld(){
        var r=new List<NativeCase139>{PhysicsExperiment139.Cases()[2],PhysicsExperiment139.Cases()[3]};
        var starts=new[]{new FreeLearningScenario("H2",4,14.4f,4.25f,18.8,-.085f,-.017f,.023f),new("H3",4,16.9f,6.8f,18.95,.095f,.014f,-.019f),
            new("H4",4,18.3f,5.9f,19.15,-.115f,.023f,-.031f),new("H5",4,13.15f,7.7f,18.65,.065f,-.022f,.029f)};
        foreach(var s in starts)foreach(var g in new[]{"progressive-b","progressive-c"})r.Add(Case(s.Name+"-"+g[^1],g,s,s.Name is "H3" or "H5"?2:1,"held-new"));
        r.Add(Case("H6-E","egg-asymmetric",new("H6",4,15.35f,5.6f,18.7,-.1f,-.019f,.026f),1,"held-transfer"));
        r.Add(Case("H7-E","egg-asymmetric",new("H7",4,17.4f,4.7f,19.1,.11f,.018f,-.024f),2,"held-transfer"));return r.ToArray();
    }
    static JsonNode Node(object x)=>JsonSerializer.SerializeToNode(x,Json)!;
    static JsonArray Rows(JsonObject r)=>(JsonArray)r["Native"]!;
    static JsonArray Reservations(JsonObject r)=>(JsonArray)r["Reservations"]!;
    static void Save(string path,JsonObject r){File.WriteAllText(path+".tmp",r.ToJsonString(Json));File.Move(path+".tmp",path,true);}
    static double Used(JsonObject r)=>r["OfflineCpuSeconds"]!.GetValue<double>()+Reservations(r).Sum(q=>q!["CpuSeconds"]!.GetValue<double>());
    static Dictionary<string,string> Sources()=>Directory.GetFiles("tools/shared-world-speedway","*.cs",SearchOption.AllDirectories)
        .Concat(Directory.GetFiles("tools/racing-constraints").Where(p=>p.EndsWith(".cs",StringComparison.Ordinal)||p.EndsWith(".csproj",StringComparison.Ordinal)||p.EndsWith("generate.py",StringComparison.Ordinal)))
        .Where(p=>!p.Replace('\\','/').Contains("/obj/")&&!p.Replace('\\','/').Contains("/bin/")).Order().ToDictionary(p=>p.Replace('\\','/'),p=>Offline142.Hash(File.ReadAllText(p)));
    static void Cost(JsonObject r)=>r["Cost"]=Node(new{Worlds=Reservations(r).Count,CpuSeconds=Used(r),NativeAndSearchCpuSeconds=Reservations(r).Sum(q=>q!["CpuSeconds"]!.GetValue<double>()),
        OfflineCpuSeconds=r["OfflineCpuSeconds"]!.GetValue<double>(),Forecasts=Reservations(r).Sum(q=>q!["Forecasts"]!.GetValue<long>()),Contacts=Reservations(r).Sum(q=>q!["Contacts"]!.GetValue<long>()),
        Limits=new{Worlds=160,CpuSeconds=1800},Scope="All native runs, prefixes, forecasts, summaries and raw/CSV serialization in each reservation. Offline reads separate and counted. Build/tests, final report formatting and Git operations excluded. No per-decision CPU cutoff."});
    public static void Run(string path,string raw,string stage){
        Directory.SetCurrentDirectory(Experiment140.RepoRoot());path=Path.GetFullPath(path);raw=Path.GetFullPath(raw);Directory.CreateDirectory(Path.GetDirectoryName(path)!);Directory.CreateDirectory(raw);Directory.CreateDirectory("work");
        using var lease=new FileStream("work/constraints143.lock",FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None);
        if(stage=="offline"){
            if(File.Exists(path))throw new IOException("Fresh audit only.");var cpu=Process.GetCurrentProcess().TotalProcessorTime;
            var audit=Offline143.Run();var r0=new JsonObject{["Audit"]=audit,["Reservations"]=new JsonArray(),["Native"]=new JsonArray(),["OfflineCpuSeconds"]=(Process.GetCurrentProcess().TotalProcessorTime-cpu).TotalSeconds};Cost(r0);Save(path,r0);return;
        }
        var r=JsonNode.Parse(File.ReadAllText(path))!.AsObject();
        if(stage=="freeze"){
            if(r["Protocol"] is not null||Reservations(r).Count>0)throw new IOException("Already frozen.");
            r["Protocol"]=Node(new{BaseHead,DefaultEnabled=false,PhysicsChanged=false,CemRuns=0,Policy=PhysicsExperiment139.Policy,Selection,Held,Isolated,
                Meanings=new{A="Exact142 original objective/ranking",B="globally remove partial-throttle preference",C="globally remove travel-direction cost",D="globally remove terminal-yaw cost",E="globally remove steering-change cost",F="globally remove original geometry speed preference",G="caution margins .2/.1 ->.05/.025 and corresponding soft clearance cost, real boundary/beta unchanged",H="replace local .8-capacity geometry penalty with known future geometry at attained offset, measured same-angle coast deceleration and full-capacity estimate",I="weighted original score over nominal hard-feasible actions without Safe>Uncertain priority; empty ->explicit originalA fallback",R="F plus bounded actual-force recovery feasibility, no .8 speed reserve in eligibility"},
                MatchedIsolation="Every main action set uses original FreeLearningController.Actions with full gas, sameH09, .2 reviews, .24 reaction, +/-35deg/90deg/s. A-H and I use identical prediction calls. R additionally probes <=4 gas roots to1.7s; this is a separate horizon/continuation treatment, not a claim about penalty removal alone.",
                Recovery="Future request from predicted observation at+.8s, full reaction activation+1.04s, original grid steering closest to physical contact inversion, gas0 recovery tested with original model through1.7. Only current first request is sent. OriginalA fallback if bounded recovery set empty; no global viability claim.",
                Geometry="Original Motoarena progressive-b/c and egg-asymmetric,62m straights,318.78m reference,variable curvature/width, historical35+27; no authored racing path. H extrapolation beyondH09 is explicitly heuristic, not a safety certificate.",
                Scope="Same continuous native scope as142: studied bend, full62m, entire next bend and following62m; D1/H1 and corner2 cases include the third bend. Historical gate separate. Every arm starts at same constructor physical state/pending; no branch substitutions or resets.",
                Pools="D0/D1 and two newS starts select isolated changes/combinations. Twelve held case/geometry pairs include original H0/H1, four new starts on both B/C, two newegg starts: EIGHT held physical families, not12 independent riders. Original H0/H1 are historically known, six held families are new. No final tuning.",
                Combinations="After isolated full results select top3 nontrivially action-changing modifiers B-H by completions, paired mean legal time ratio, then changed choices. Exclude simultaneous F/H redundancy. Test all pairs and triple, then add recovery to highest-ranked pair and triple; <=6 full configurations on all selection cases. Frozen selection only, not held.",
                Confirmation="Before opening held worlds, lock top3 nonA variants across singles/combinations by reliability then mean paired time ratio. Run each plusA on all12 held pairs. Family-weighted improvements prevent width duplicates inflating evidence. Orientational GO>=5% mean paired family time gain with no newDNF; report smaller gains and all failures asPARTIAL, no threshold fitting.",
                Diagnostics="Up to3 beta45 native failures from distinct case/objective histories continued with unchanged controller and ONLY native threshold60 or omitted. Other physical guards retained. Up to2 native uncertified stops repeated with original refined sweep certificate. Auxiliary detector tests never promote main results or prove real-motorcycle validity.",
                Budget="160 new worlds maximum,1800CPU-s maximum; planned40isolated+<=24combinations+48held+<=8detector diagnostics+6warm repeats=126. Full autonomous held runs take precedence; optional repeats omitted near total budget. Never stop because one decision exceeds.2CPU-s. Cold and subsequent decisions reported separately.",
                Sources=Sources(),FrozenBeforeNative=true});Cost(r);Save(path,r);return;
        }
        foreach(var s in r["Protocol"]!["Sources"]!.AsObject())if(Offline142.Hash(File.ReadAllText(s.Key))!=s.Value!.GetValue<string>())throw new IOException("Frozen source changed: "+s.Key);
        if(Reservations(r).Any(q=>!q!["Complete"]!.GetValue<bool>()))throw new IOException("Incomplete attempt retained. Never silently repeat a world.");
        if(stage=="preflight"){
            foreach(var id in new[]{"A","B","H"})Native(r,path,raw,Selection[0],Isolated.Single(v=>v.Id==id));
            r["Preflight"]=Node(new{Passed=true,FullAutonomous=true,NoDecisionCpuCutoff=true,Meaning="Full comparable native worlds, original142 fingerprint/time parity and numerical/queue guards. Behavioral DNF is evidence, not a pipeline failure."});Save(path,r);return;
        }
        if(r["Preflight"]?["Passed"]?.GetValue<bool>()!=true)throw new IOException("Preflight first.");
        if(stage=="isolated"){
            foreach(var c in Selection)foreach(var v in Isolated)Native(r,path,raw,c,v);r["IsolatedComplete"]=true;Cost(r);Save(path,r);return;
        }
        if(stage=="combinations"){
            if(r["IsolatedComplete"]?.GetValue<bool>()!=true)throw new IOException("Isolated full trials first.");
            if(r["CombinationLock"] is null){
                var ranked=Rank(r,Isolated.Where(v=>v.Id is not "A" and not "I" and not "R").Where(v=>Changed(r,v.Id)>0)).ToList();
                var components=new List<Variant143>();foreach(var v in ranked){if(components.Any(q=>q.Id=="F")&&v.Id=="H"||components.Any(q=>q.Id=="H")&&v.Id=="F")continue;components.Add(v);if(components.Count==3)break;}
                if(components.Count<2)throw new IOException("Fewer than2 action-changing modifiers; no fabricated combination evidence.");
                var configurations=new List<Variant143>();int n=1;
                for(int a=0;a<components.Count;a++)for(int b=a+1;b<components.Count;b++)configurations.Add(new("K"+n++,components[a].Changes.Concat(components[b].Changes).Distinct().Order().ToArray()));
                if(components.Count==3)configurations.Add(new("K"+n++,components.SelectMany(v=>v.Changes).Distinct().Order().ToArray()));
                r["CombinationLock"]=Node(new{BeforeCombinationWorlds=true,Components=components,RankedModifiers=ranked,Configurations=configurations,Rule="Reliability, paired mean completed time ratio, changed decisions; no held input."});Save(path,r);
            }
            var configs=r["CombinationLock"]!["Configurations"]!.Deserialize<Variant143[]>(Json)!;
            foreach(var c in Selection)foreach(var v in configs)Native(r,path,raw,c,v);
            if(r["RecoveryCombinationLock"] is null){
                var ranked=Rank(r,configs).ToArray();var chosen=new[]{ranked[0],configs.OrderByDescending(v=>v.Changes.Length).ThenBy(v=>v.Id).First()}.DistinctBy(v=>v.Id).ToArray();
                var recovery=chosen.Select((v,i)=>new Variant143("KR"+(i+1),v.Changes,true)).ToArray();
                r["RecoveryCombinationLock"]=Node(new{BeforeRecoveryCombinationWorlds=true,Chosen=recovery,Rule="Best completed combination and the maximal compatible triple; physical continuation treatment kept distinct."});Save(path,r);
            }
            foreach(var c in Selection)foreach(var v in r["RecoveryCombinationLock"]!["Chosen"]!.Deserialize<Variant143[]>(Json)!)Native(r,path,raw,c,v);
            r["CombinationsComplete"]=true;Cost(r);Save(path,r);return;
        }
        if(stage=="confirmation"){
            if(r["CombinationsComplete"]?.GetValue<bool>()!=true)throw new IOException("Full combination selection first.");
            if(r["ConfirmationLock"] is null){
                var candidates=Isolated.Skip(1).Concat(r["CombinationLock"]!["Configurations"]!.Deserialize<Variant143[]>(Json)!)
                    .Concat(r["RecoveryCombinationLock"]!["Chosen"]!.Deserialize<Variant143[]>(Json)!);
                r["ConfirmationLock"]=Node(new{BeforeAnyHeldWorld=true,Winners=Rank(r,candidates).Take(3).ToArray(),Held,Rule="Reliability, then paired mean completed time ratio; never tune on held."});Save(path,r);
            }
            foreach(var c in Held)foreach(var v in new[]{Isolated[0]}.Concat(r["ConfirmationLock"]!["Winners"]!.Deserialize<Variant143[]>(Json)!))Native(r,path,raw,c,v);
            r["ConfirmationComplete"]=true;Cost(r);Save(path,r);return;
        }
        if(stage=="diagnostics"){
            if(r["ConfirmationComplete"]?.GetValue<bool>()!=true)throw new IOException("Full held autonomous trials have priority.");
            if(r["DetectorLock"] is null){
                var beta=Rows(r).Where(q=>q!["Outcome"]!.GetValue<string>()=="control envelope"&&q["Pool"]!.GetValue<string>()=="selection")
                    .GroupBy(q=>q!["Family"]!.GetValue<string>()).Select(g=>g.First()).Take(3).Select(q=>new{Case=q!["Case"]!.Deserialize<NativeCase139>(Json),Variant=q["Variant"]!.Deserialize<Variant143>(Json),MainName=q["Name"]!.GetValue<string>()}).ToArray();
                var boundary=Rows(r).Where(q=>q!["Outcome"]!.GetValue<string>()=="uncertified interval"&&q["Pool"]!.GetValue<string>()=="selection").Take(2)
                    .Select(q=>new{Case=q!["Case"]!.Deserialize<NativeCase139>(Json),Variant=q["Variant"]!.Deserialize<Variant143>(Json),MainName=q["Name"]!.GetValue<string>()}).ToArray();
                r["DetectorLock"]=Node(new{BeforeAuxiliaryWorlds=true,Beta=beta,Boundary=boundary});Save(path,r);
            }
            foreach(var q in (JsonArray)r["DetectorLock"]!["Beta"]!)foreach(var limit in new double?[]{60,null}){
                var c=q!["Case"]!.Deserialize<NativeCase139>(Json)!;var v=q["Variant"]!.Deserialize<Variant143>(Json)!;
                Native(r,path,raw,c,v,"beta-"+(limit?.ToString(System.Globalization.CultureInfo.InvariantCulture)??"none")+"-"+q["MainName"]!.GetValue<string>(),limit,false,true);
            }
            foreach(var q in (JsonArray)r["DetectorLock"]!["Boundary"]!)Native(r,path,raw,q!["Case"]!.Deserialize<NativeCase139>(Json)!,q["Variant"]!.Deserialize<Variant143>(Json)!,"refined-"+q["MainName"]!.GetValue<string>(),45,true,true);
            Cost(r);Save(path,r);return;
        }
        if(stage=="benchmark"){
            if(r["ConfirmationComplete"]?.GetValue<bool>()!=true)throw new IOException("Held first.");
            var winner=r["ConfirmationLock"]!["Winners"]![0]!.Deserialize<Variant143>(Json)!;
            for(int i=0;i<3;i++)foreach(var v in new[]{Isolated[0],winner}){
                if(Used(r)>1680){r["OptionalBenchmarksOmittedForBudget"]=true;Save(path,r);return;}
                Native(r,path,raw,Selection[0],v,"warm-"+i+"-"+v.Id,45,false,true);
            }Cost(r);Save(path,r);return;
        }
        throw new ArgumentException("Explicit bounded stage required.");
    }
    static int Changed(JsonObject r,string id)=>Rows(r).Where(q=>q!["Variant"]!["Id"]!.GetValue<string>()==id&&q["Pool"]!.GetValue<string>()=="selection").Sum(q=>q!["ChangedDecisions"]!.GetValue<int>());
    static IOrderedEnumerable<Variant143> Rank(JsonObject r,IEnumerable<Variant143> variants){
        JsonNode[] For(string id)=>Rows(r).Where(q=>q!["Variant"]!["Id"]!.GetValue<string>()==id&&q["Pool"]!.GetValue<string>()=="selection").Select(q=>q!).ToArray();
        double Ratio(string id){var rows=For(id);return rows.Where(q=>q["Completed"]!.GetValue<bool>()).Select(q=>{
            var a=For("A").Single(x=>x["Family"]!.GetValue<string>()==q["Family"]!.GetValue<string>());
            return a["Completed"]!.GetValue<bool>()?q["FullTimeSeconds"]!.GetValue<double>()/a["FullTimeSeconds"]!.GetValue<double>():1d;}).DefaultIfEmpty(double.PositiveInfinity).Average();}
        return variants.DistinctBy(v=>v.Id).OrderByDescending(v=>For(v.Id).Count(q=>q["Completed"]!.GetValue<bool>())).ThenBy(v=>Ratio(v.Id)).ThenByDescending(v=>Changed(r,v.Id)).ThenBy(v=>v.Id,StringComparer.Ordinal);
    }
    static void Native(JsonObject r,string path,string raw,NativeCase139 c,Variant143 variant,string? name=null,double? beta=45,bool refined=false,bool auxiliary=false){
        name??=c.Name+"-"+variant.Id;if(Rows(r).Any(q=>q!["Name"]!.GetValue<string>()==name))return;
        if(Reservations(r).Count>=160||Used(r)>=1750)throw new IOException("Total experiment budget reached; incomplete scopes remain explicit.");
        var reservation=new JsonObject{["Name"]=name,["Complete"]=false,["CpuSeconds"]=0d,["Forecasts"]=0L,["Contacts"]=0L};Reservations(r).Add(reservation);Save(path,r);
        var cpu=Process.GetCurrentProcess().TotalProcessorTime;var chooser=new Controller143(c);var logs=new List<object>();var times=new List<double>();int changed=0,contacts=0;
        FreeLearningDecision Choose(FreeRacingInput input){
            var t=Process.GetCurrentProcess().TotalProcessorTime;var q=chooser.Choose(input,variant);var elapsed=(Process.GetCurrentProcess().TotalProcessorTime-t).TotalSeconds;times.Add(elapsed);
            if(q.Decision.Action!=q.Original.Action)changed++;
            logs.Add(new{input.Time,Reference=input.Rider.TotalReferenceMeters,SpeedKph=input.Rider.Speed*3.6,BetaDegrees=input.Rider.SlideRadians*180/Math.PI,Yaw=input.Rider.AngularVelocity,
                input.Steering,input.Active,Request=q.Decision.Action,Original=q.Original.Action,q.Decision.Safety,q.Score,q.Terms,q.PreparationCost,q.HardFeasible,q.Reason,
                q.Forecasts,q.Contacts,CpuSeconds=elapsed,q.ForecastRecovery,q.RecoveryObservedAt,q.ViableRoots});return q.Decision;
        }
        try{
            var capture=NativeCapture143.Execute(name,16015,PhysicsExperiment139.Policy,c.Case.Scenario,twoBends:true,horizonSeconds:.9,normalizeProgress:true,fullThrottleCandidate:true,
                trackShape:TrackAwarenessExperiment134.Variant(c.Case.Geometry),chooser:Choose,extendAfterSecondStraight:true,studiedCorner:c.Corner,
                cpuCeilingSeconds:Math.Max(1,1785-Used(r)),betaLimitDegrees:beta,refinedDiagnostic:refined,completedObservation:result=>contacts=result.Contacts.Count(e=>e.Kind=="begin"));
            var summary=Summary142.Native(capture,c,variant.Id);summary["Name"]=name;summary["Family"]=c.Name;summary["Pool"]=auxiliary?"auxiliary":Held.Any(h=>h.Name==c.Name)?"held":"selection";
            summary["Variant"]=Node(variant);summary["ChangedDecisions"]=changed;summary["Decisions"]=Node(logs);summary["ActualRiderContactBegins"]=contacts;
            summary["NativeBetaDetectorDegrees"]=beta;summary["RefinedBoundaryDiagnostic"]=refined;
            summary["StopCategory"]=capture.Episode.Replay.Metrics.Outcome switch{"completed"=>"legal completion","uncertified interval"=>"native swept-certificate uncertainty; no confirmed crossing", "confirmed crossing"=>"native footprint boundary violation", "control envelope"=>"experimental beta detector, not empirical fall threshold", "reverse" or "projection"=>"kinematic/progress guard", "total experiment CPU limit"=>"budget stop",_=>capture.Episode.Replay.Metrics.Outcome};
            if(capture.Episode.Replay.Metrics.Outcome=="control envelope")summary["FailureClassification"]="experimental beta"+beta+" detector; not a calibrated real-motorcycle fall threshold";
            summary["DecisionCpu"]=Node(new{Count=times.Count,Total=times.Sum(),Mean=times.Average(),Maximum=times.Max(),ColdFirst=times[0],WarmMean=times.Skip(1).DefaultIfEmpty(times[0]).Average(),P95=times.Order().ElementAt(Math.Min(times.Count-1,(int)Math.Ceiling(.95*times.Count)-1)),NoDecisionCpuCutoff=true});
            summary["Telemetry"]=Telemetry143.Summary(capture.Episode,capture.StopReference);
            if(variant.Id=="A"&&!auxiliary&&c.Name is "D0" or "D1" or "H0" or "H1"){
                var previous=JsonNode.Parse(File.ReadAllText("docs/experiments/racing-objective-ablation/results.json"))!;
                var old=((JsonArray)previous["Native"]!).Single(q=>q!["Name"]!.GetValue<string>()==c.Name+"-A")!;
                var equal=old["NativeMetrics"]!["Fingerprint"]!.GetValue<string>()==capture.Episode.Replay.Metrics.Fingerprint;
                summary["Exact142Fingerprint"]=equal;summary["Historical142Time"]=old["FullTimeSeconds"]!.DeepClone();
                if(!equal)throw new IOException("Original142 A fingerprint differs.");
            }
            Directory.CreateDirectory(Path.Combine(Path.GetDirectoryName(path)!,"telemetry"));
            var csv=Path.Combine(Path.GetDirectoryName(path)!,"telemetry",name+".csv.gz");Telemetry143.Write(csv,capture.Episode);
            var rawPath=Path.Combine(raw,name+".json.gz");using(var z=new GZipStream(File.Create(rawPath),CompressionLevel.Optimal))JsonSerializer.Serialize(z,capture.Episode,Json);
            summary["NativeRawSha256"]=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(rawPath)));summary["CsvSha256"]=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(csv)));
            Rows(r).Add(summary);reservation["CpuSeconds"]=(Process.GetCurrentProcess().TotalProcessorTime-cpu).TotalSeconds;reservation["Forecasts"]=capture.Episode.Replay.Metrics.Forecasts;
            reservation["Contacts"]=capture.Episode.Replay.Metrics.PhysicsEvaluations;reservation["Complete"]=true;
            if(summary["NumericalClockGatesPassed"]?.GetValue<bool>()!=true)throw new IOException("Numerical/actuator/queue invariant failed; preserve run and stop.");
            Cost(r);Save(path,r);Console.WriteLine($"{Reservations(r).Count}/160 {name}: {capture.Episode.Replay.Metrics.Outcome}, full={summary["FullTimeSeconds"]}, CPU={reservation["CpuSeconds"]}, used={Used(r):F3}");
        }catch(Exception ex){reservation["CpuSeconds"]=(Process.GetCurrentProcess().TotalProcessorTime-cpu).TotalSeconds;reservation["Error"]=ex.Message;Cost(r);Save(path,r);throw;}
    }
}
