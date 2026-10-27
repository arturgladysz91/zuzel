using System.Diagnostics;
using System.Numerics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
namespace SharedWorldSpeedway;

/// <summary>One resumable two-file experiment. No training, CEM, alternative policies or raw export.</summary>
public static class Experiment140
{
    public const string BaseHead="1a84e527536e0dc4e77eaa1acebd2866c1579593";
    public static string RepoRoot(){
        foreach(var start in new[]{Directory.GetCurrentDirectory(),AppContext.BaseDirectory})
            for(var d=new DirectoryInfo(start);d is not null;d=d.Parent)if(File.Exists(Path.Combine(d.FullName,"SpeedwayManager.sln")))return d.FullName;
        throw new DirectoryNotFoundException("Repository root required.");
    }
    public static readonly JsonSerializerOptions Json=FreeLearningExperiment.Json;
    static JsonNode Node(object x)=>JsonSerializer.SerializeToNode(x,Json)!;
    static string Hash(string text)=>Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text.Replace("\r\n","\n"))));
    static Dictionary<string,string> Sources()=>Directory.GetFiles("tools/shared-world-speedway","*.cs",SearchOption.AllDirectories)
        .Concat(new[]{"tools/tyre-law-regularization/generate.py","tools/tyre-law-regularization/Program.cs","tools/tyre-law-regularization/tyre-law-regularization.csproj"})
        .Where(x=>!x.Replace('\\','/').Contains("/obj/")&&!x.Replace('\\','/').Contains("/bin/")).Order().ToDictionary(x=>x.Replace('\\','/'),x=>Hash(File.ReadAllText(x)));
    static void Save(string path,JsonObject root){var temp=path+".tmp";File.WriteAllText(temp,root.ToJsonString(Json));File.Move(temp,path,true);}
    static NativeCase139[] Cases()=>PhysicsExperiment139.Cases();
    static string OldDirectory=>Path.Combine(RepoRoot(),"docs/experiments/motorcycle-physics-validation");
    static RobustEpisode Old(string name)=>RobustLearningExperiment.ReadEpisode(Path.Combine(OldDirectory,"native"),name);
    static JsonArray Attempts(JsonObject r)=>(JsonArray)r["Reservations"]!;
    static int Reserve(JsonObject root,string path,string name,bool native){
        var attempts=Attempts(root);
        if(attempts.Any(x=>x!["Name"]!.GetValue<string>()==name))throw new InvalidOperationException("Duplicate reservation; never rerun a completed or incomplete experiment.");
        if(attempts.Any(x=>!x!["Complete"]!.GetValue<bool>()))throw new InvalidOperationException("Incomplete reservation retained; no retry.");
        var worlds=attempts.Sum(x=>x!["NativeWorlds"]!.GetValue<int>());var cpu=attempts.Sum(x=>x!["CpuSeconds"]!.GetValue<double>());
        if(worlds+(native?1:0)>24||cpu>390)throw new InvalidOperationException("Frozen 24-world/450 CPU-s budget; retain60s safety.");
        attempts.Add(Node(new{Name=name,NativeWorlds=native?1:0,Complete=false,CpuSeconds=0d,ContactEvaluations=0L,Forecasts=0L}));Save(path,root);return attempts.Count-1;
    }
    static void Complete(JsonObject root,string path,int index,double cpu,long contacts,long forecasts){
        var a=Attempts(root)[index]!;a["Complete"]=true;a["CpuSeconds"]=cpu;a["ContactEvaluations"]=contacts;a["Forecasts"]=forecasts;
        var attempts=Attempts(root);root["Cost"]=Node(new{NewNativeWorlds=attempts.Sum(x=>x!["NativeWorlds"]!.GetValue<int>()),
            CpuSeconds=attempts.Sum(x=>x!["CpuSeconds"]!.GetValue<double>()),ContactEvaluations=attempts.Sum(x=>x!["ContactEvaluations"]!.GetValue<long>()),
            Forecasts=attempts.Sum(x=>x!["Forecasts"]!.GetValue<long>()),CpuIncludes="New mathematical matrices, native episodes and every new forecast; historical cache reads have separate timing",Limits=new{Worlds=24,CpuSeconds=450}});Save(path,root);
    }
    public static void Run(string path,string stage){
        Directory.SetCurrentDirectory(RepoRoot());path=Path.GetFullPath(path);Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        Directory.CreateDirectory("work");using var lease=new FileStream("work/tyre140.lock",FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None);
        if(stage=="freeze"){
            if(File.Exists(path))throw new IOException("Never overwrite a frozen experiment.");
            var root=new JsonObject{
                ["Protocol"]=Node(new{BaseHead,ProductionChanged=false,DefaultEnabled=false,Status="PROVISIONAL / UNCALIBRATED; not real-soil calibration",Material=TyreMaterial140.Reference,
                    ReferenceDerivation="Preserve nominal139 rear curve at m147kg,Iw0.9kgm2,R0.33m,mu1.2845000267028808. Freeze BOTH material velocity floors once; never depend on a run's Iw, mass, dt or solver tolerance. Not a fitted speed/lap target.",
                    Equations=new{Law="a=(Romega-u)/dx; b=-w/dy; (Fx,Fy)=muFz*(a,b)/sqrt(1+a2+b2)",Scales="dx2=(0.1)^2*(u2+0.5^2)+vxFloor2; dy2=(0.12float)^2*(u2+0.5^2)+vyFloor2",Solver="g(z)=z+c*z/sqrt(d2+z2)-abs(S)=0; gprime>=1; bracket[0,abs(S)]; implicit midpoint wheel/longitudinal body, original explicit planar hold",Front="Unchanged139; no Iw/dt in held-angle constitutive query; .03 lateral slope floor is not temporal relaxation"},
                    Solver=new{WheelSolver140.MaximumIterations,WheelSolver140.RootToleranceMetersPerSecond},StepsSeconds=Mathematics140.Steps,LocalDurationSeconds=.2,
                    MathematicalGates=new{MaterialIwForceDifferenceNewtons=0,MaxFloatSolverResidualMetersPerSecond=2e-5,NormalizedLongitudinalEnergyResidual=1e-10,MaximumRearUtilization=1.000001,MinimumLossJoules=-1e-6,FrontDriveToleranceNewtons=.001,
                        Convergence="last adjacent scaled-state error < first adjacent error, or first below1e-5 roundoff floor; all four h and physical0.2s endpoints retained; no h-dependent material parameters"},
                    NativeCases=Cases(),NativeModes=new{Long="Exactly139 B: one .2s causal early preparation, .6s requested full pulse near exit, then134A",Short="Exactly139 C: same preparation and first full request;134A resumes both channels at next .2s review"},
                    NativePolicy="#16015 frozen134A weights, action set, H09 and utility; only prediction/contact equations use new material when enabled. Original .24s reaction, .2s reviews, +/-35deg and90deg/s remain.",
                    NativeGates="All inherited track, reverse, energy, actuator and beta45 guards literal. Old two-bend+1s and full next62m gates separate. Max energy ledger residual <=.05J+1e-6*bodyK; actual yaw update error<=2e-5rad/s; no speed acceptance criterion.",
                    Allocation="12 planned worlds: original/new0.5s D0 preflight +8 new D0,D1,H0,H1 long/short +2 new D0 short perturbations (+/-.25deg heading,+/-.01rad/s yaw) from initial start. Eight old139 B/C outcomes reused, not rerun.",
                    Confirmation="H0/H1 withheld within140 until diagnostic lock. Families were used in139, so not a newly unseen empirical dataset. No tuning on these cases.",
                    Ordering="Freeze; mathematics; exact OFF-prefix/new preflight; diagnostic D0/D1 and perturbations; separate frozen confirmation lock; H0/H1; final standard Windows CI.",
                    Limits=new{NewWorlds=24,CpuSeconds=450,PerEpisodeCpuSeconds=30},Sources=Sources(),FrozenBeforeRecordedMatricesAndNativeWorlds=true,DevelopmentUnitChecksAllowedBeforeFreeze=true}),
                ["Reservations"]=new JsonArray(),["Native"]=new JsonArray(),["CachedOld"]=new JsonArray()};
            Save(path,root);return;
        }
        var r=JsonNode.Parse(File.ReadAllText(path))!.AsObject();
        foreach(var item in r["Protocol"]!["Sources"]!.AsObject())if(Hash(File.ReadAllText(item.Key))!=item.Value!.GetValue<string>())throw new IOException("Frozen source changed: "+item.Key);
        if(Attempts(r).Any(x=>!x!["Complete"]!.GetValue<bool>()))throw new IOException("An incomplete reservation must not be retried.");
        if(stage=="math"){
            if(r["Mathematics"] is not null)return;var index=Reserve(r,path,"mathematics",false);var m=Node(Mathematics140.Run());r["Mathematics"]=m;
            Complete(r,path,index,m["CpuSeconds"]!.GetValue<double>(),0,0);Console.WriteLine(m.ToJsonString(Json));return;
        }
        if(r["Mathematics"]?["Passed"]?.GetValue<bool>()!=true)throw new InvalidOperationException("Isolated gates must pass before native worlds.");
        if(stage=="preflight"){
            foreach(var enabled in new[]{false,true}){
                var name=enabled?"preflight-new":"preflight-off";
                if(((JsonArray)r["Native"]!).Any(x=>x!["Name"]!.GetValue<string>()==name))continue;
                Native(r,path,Cases()[0],"off",enabled,name,.5);
            }
            return;
        }
        if(!((JsonArray)r["Native"]!).Any(x=>x!["Name"]!.GetValue<string>()=="preflight-new"))throw new InvalidOperationException("Preflight required.");
        if(stage=="diagnostic"){
            CacheOld(r,path);
            foreach(var c in Cases().Take(2))foreach(var mode in new[]{"long","short"})Native(r,path,c,mode,true,c.Name+"-new-"+mode);
            foreach(var sign in new[]{-1,1}){
                var c=Cases()[0];var scenario=c.Case.Scenario with{Name="D0-perturb"+sign,HeadingError=c.Case.Scenario.HeadingError+(float)(sign*.25*Math.PI/180),YawRate=c.Case.Scenario.YawRate+sign*.01f};
                var changed=c with{Name="D0-perturb"+sign,Pool="diagnostic-perturbation",Case=c.Case with{Scenario=scenario}};
                Native(r,path,changed,"short",true,changed.Name+"-new-short");
            }
        }else if(stage=="confirmation"){
            if(r["ConfirmationLock"] is null)throw new InvalidOperationException("Commit a confirmation lock before held native cases.");
            foreach(var c in Cases().Skip(2))foreach(var mode in new[]{"long","short"})Native(r,path,c,mode,true,c.Name+"-new-"+mode);
        }else throw new ArgumentException("freeze|math|preflight|diagnostic|confirmation");
    }
    static void CacheOld(JsonObject root,string path){
        if(((JsonArray)root["CachedOld"]!).Count>0)return;var cpu=Process.GetCurrentProcess().TotalProcessorTime;
        foreach(var c in Cases())foreach(var mode in new[]{"long","short"}){
            var name=c.Name+(mode=="long"?"-B":"-C");var episode=Old(name);
            var scope=JsonNode.Parse(File.ReadAllText(Path.Combine(OldDirectory,"scope-"+name+".json")))!;
            var capture=new NativeCaptureResult140(episode,scope["HistoricalTwoBendsCompleted"]!.GetValue<bool>(),scope["HistoricalGateSeconds"]?.GetValue<double>(),0,0);
            var summary=NativeSummary140.Build(capture,c,mode,false,null,null,trace:false);
            ((JsonArray)root["CachedOld"]!).Add(Node(summary));
        }
        root["CachedReadCpuSeconds"]= (Process.GetCurrentProcess().TotalProcessorTime-cpu).TotalSeconds;Save(path,root);
    }
    static void Native(JsonObject root,string path,NativeCase139 c,string mode,bool enabled,string name,double? shortSeconds=null){
        var rows=(JsonArray)root["Native"]!;if(rows.Any(x=>x!["Name"]!.GetValue<string>()==name))return;
        var index=Reserve(root,path,name,true);var cpu=Process.GetCurrentProcess().TotalProcessorTime;
        var frame=TrackAwarenessExperiment134.Frame(c.Case.Geometry);var surface=MultiActionExperiment135.Surface(frame);var spec=FreeLearningExperiment.Rider(c.Case.Scenario);var p=FreeLearningExperiment.Parameters();
        double? prepared=null,opened=null;FreeRacingAction? held=null;
        FreeLearningDecision Choose(FreeRacingInput i){
            var d=enabled?Policy140.Choose(i,PhysicsExperiment139.Policy,spec,frame,surface,p,147,52.72097f,.9,true,true,false,false)
                :MultiActionPlanner135.Baseline(i,PhysicsExperiment139.Policy,spec,frame,surface,p);
            if(mode=="off")return d;
            var remaining=SpeedThrottleAudit136.Exit(frame,c.Corner)-i.Rider.TotalReferenceMeters;
            var bend=i.Rider.Road.IsTurn&&i.Rider.TotalReferenceMeters>=frame.SegmentStartMeters(c.Corner==1?1:5);var action=d.Action;
            if(prepared is null&&bend&&remaining<=40){prepared=i.Time;action=new(PhysicsExperiment139.PreparationAngle(i),(float)PhysicsExperiment139.Policy.Drive);}
            if(opened is null&&bend&&remaining<=Math.Max(5,frame.ReferenceSpeed(i.Rider.Road,i.Rider.Speed)*(.24+.2)+1)){
                opened=i.Time;held=new(i.Active.SteeringRadians,1);action=held.Value;
            }else if(opened is{} at&&mode=="long"&&i.Time<at+.6-1e-8)action=held!.Value;
            return d with{Action=action};
        }
        var capture=NativeCapture140.Execute(name,16015,PhysicsExperiment139.Policy,c.Case.Scenario,shortSeconds:shortSeconds,twoBends:true,horizonSeconds:.9,
            normalizeProgress:true,fullThrottleCandidate:true,trackShape:TrackAwarenessExperiment134.Variant(c.Case.Geometry),chooser:Choose,extendAfterSecondStraight:shortSeconds is null,separated:enabled);
        var summary=Node(NativeSummary140.Build(capture,c,mode,enabled,prepared,opened,trace:c.Name=="D0"&&shortSeconds is null));
        var baseline=Old(c.Name.StartsWith("D0-perturb",StringComparison.Ordinal)?"D0-C":c.Name+"-C");var first=capture.Episode.Replay.Samples[0].Input;var origin=baseline.Replay.Samples[0].Input;
        var same=first.Rider==origin.Rider&&first.Omega==origin.Omega&&first.Steering==origin.Steering;
        summary["ExactInitialPhysicalState"]=same;summary["IntentionalInitialPerturbation"]=c.Pool=="diagnostic-perturbation";
        if(!same&&c.Pool!="diagnostic-perturbation")throw new InvalidOperationException("Nominal initial physical state differs.");
        if(shortSeconds is not null){
            if(capture.Episode.Replay.Metrics.Outcome!="probe")throw new InvalidOperationException("Preflight physical guard failure.");
            if(!enabled){
                var old=Old("off-D0");var saved=old.Replay.Samples.Where(s=>s.Input.Time<=shortSeconds+1e-8).ToArray();
                var actual=capture.Episode.Replay.Samples;var exact=actual.Length==saved.Length&&actual.Zip(saved).All(x=>x.First==x.Second||JsonSerializer.Serialize(x.First,Json)==JsonSerializer.Serialize(x.Second,Json));
                summary["ExactHistoricalPrefix"]=exact;summary["HistoricalPrefixSha256"]=Hash(JsonSerializer.Serialize(saved,Json));
                summary["NewPrefixSha256"]=Hash(JsonSerializer.Serialize(actual,Json));
                if(!exact)throw new InvalidOperationException("OFF prefix differs from saved original.");
            }else{
                var run=capture.Episode;var initial=run.Replay.Samples[0].Input;var end=run.Replay.Samples[^1].Input;
                var prediction=Predictor140.Predict(initial,spec,frame,surface,p,147,52.72097f,shortSeconds.Value,
                    causalPolicy:q=>(run.Replay.Decisions.Last(x=>x.Time<=q.Time+1e-8).Action,0),reviewSeconds:.2);
                var point=prediction.Points[^1];var positionError=Vector2.Distance(point.Rider.Position,end.Rider.Position);
                summary["PredictionReplayCheck"]=Node(new{PositionErrorMeters=positionError,VelocityErrorMetersPerSecond=Vector2.Distance(point.Rider.Velocity,end.Rider.Velocity),
                    OmegaErrorRadPerSecond=Math.Abs(point.Omega-end.Omega),YawErrorRadPerSecond=Math.Abs(point.Rider.AngularVelocity-end.Rider.AngularVelocity),prediction.ForceEvaluations,
                    Meaning="Frozen recorded requests supplied to new predictor, no policy selection oracle. Same .24s queue. No native state substitution."});
                summary["ExtraPreflightPredictionContacts"]=prediction.ForceEvaluations;
                if(positionError>.003||Math.Abs(point.Omega-end.Omega)>.01)throw new InvalidOperationException("New forecast and Box2D disagree in preflight.");
            }
        }
        rows.Add(summary);var metrics=capture.Episode.Replay.Metrics;
        var extra=summary["ExtraPreflightPredictionContacts"]?.GetValue<int>()??0;
        Complete(root,path,index,(Process.GetCurrentProcess().TotalProcessorTime-cpu).TotalSeconds,metrics.PhysicsEvaluations+extra,metrics.Forecasts+(extra>0?1:0));
        Console.WriteLine($"{name}: {metrics.Outcome}, beta={metrics.MaximumBetaDegrees:F3}, duration={metrics.DurationSeconds:F3}, CPU={metrics.CpuSeconds:F3}");
        if(summary["EnergyLedgerPassed"]!.GetValue<bool>()!=true||summary["MaximumYawUpdateErrorRadiansPerSecond"]!.GetValue<double>()>2e-5)
            throw new InvalidOperationException("New native numerical gate failed; retain result and stop.");
    }
}
