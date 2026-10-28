using System.Diagnostics;
using System.IO.Compression;
using System.Numerics;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using CoreSim;
using SharedWorldSpeedway;

if(args.Length<2||args[0] is not ("--audit-saved" or "--audit-all-saved" or "--enable-native145"))
    throw new ArgumentException("Default OFF: --audit-saved <output> | --enable-native145 <output> <treatments.json>");
var output=Path.GetFullPath(args[1]);Directory.CreateDirectory(output);
var json=FreeLearningExperiment.Json;
var oldRoot="docs/experiments/standing-start-three-laps/native";
Run144 Read(string name){using var z=new GZipStream(File.OpenRead(Path.Combine(oldRoot,name+".json.gz")),CompressionMode.Decompress);return JsonSerializer.Deserialize<Run144>(z,json)!;}
void Save(string name,object value)=>File.WriteAllText(Path.Combine(output,name),JsonSerializer.Serialize(value,json));
string Hash(object value)=>Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(value,json)));

if(args[0]=="--audit-all-saved"){FullScoreAudit145.Run(output);return;}

if(args[0]=="--audit-saved"){
    if(File.Exists(Path.Combine(output,"score-audit.json")))throw new IOException("Preserve completed read-only audit.");
    var started=Process.GetCurrentProcess().TotalProcessorTime;var audits=new List<object>();
    var frame=StandingWorld144.Frame();var surface=MultiActionExperiment135.Surface(frame);var p=FreeLearningExperiment.Parameters();var policy=Controller143.Policy;
    foreach(var name in new[]{"A","B","F","K1"}){
        var run=Read(name);var rider=run.Riders[0];
        var times=name=="K1"?new[]{28.4,29,29.6,31,32,33.8}:new[]{rider.Decisions.MinBy(d=>Math.Abs(d.Choice.Time-(run.Crossings[0].Time+9)))!.Choice.Time};
        foreach(var target in times){
            var d=rider.Decisions.MinBy(d=>Math.Abs(d.Choice.Time-target))!;var s=rider.Samples.MinBy(s=>Math.Abs(s.Input.Time-d.Choice.Time))!;
            var input=s.Input with{Time=d.Choice.Time,NextReview=d.Choice.Time,Pending=s.Input.Pending.Where(c=>c.ObservedAt<d.Choice.Time-1e-8).ToArray()};
            var shaft=new CrankClutch144(omega:s.CrankOmega);var actions=FreeLearningController.Actions(input,policy,true).AsEnumerable();
            if(name=="K1")actions=actions.Concat(new[]{-35f,-25f,-10f,-7.5f,7.5f,10f,25f,35f}.SelectMany(angle=>new[]{0f,(float)policy.Drive,.5f,1f}.Select(gas=>new FreeRacingAction(angle*MathF.PI/180,gas))));
            var leaves=new List<object>();double maximumRecordedScoreDifference=0;
            foreach(var action in actions.Distinct()){
                var prediction=Predictor144.Predict(input,rider.Spec,frame,surface,p,147,52.72097f,.9,action,powertrain:shaft);
                var leaf=Objective142.FromPrediction(input,policy,frame,surface,prediction,action);var score=Controller143.Score(leaf,Policy144.Variant(name));
                var recorded=d.Choice.Candidates.FirstOrDefault(c=>c.Action==action);
                if(recorded is not null)maximumRecordedScoreDifference=Math.Max(maximumRecordedScoreDifference,Math.Abs(score.Score-recorded.Score));
                leaves.Add(new{Action=action,InOriginalGrid=recorded is not null,Selected=action==d.Choice.Action,Score=score.Score,Safety=score.Safety,
                    leaf.Terms,RemovedThrottle=name is "B" or "K1"?leaf.Terms.Throttle:0,RemovedGeometry=name is "F" or "K1"?leaf.Terms.Geometry:0,
                    leaf.HardViolation,leaf.MaximumBetaDegrees,leaf.MaximumYawRate,leaf.EndYawRate,Candidate=leaf.Candidate,
                    prediction.ForceEvaluations,End=prediction.Points[^1]});
            }
            audits.Add(new{Run=name,Time=d.Choice.Time,Input=input,Crank=s.CrankOmega,StateFingerprint=Hash(new{input,s.CrankOmega,shaft.Parameters,p,Geometry="progressive-b",surface}),
                Chosen=d.Choice.Action,maximumRecordedScoreDifference,Leaves=leaves});
        }
    }
    var loads=LoadBasedTyreGrip.Budget(147,surface.SampleSurface(1,2),TwoContactBikeParameters.Default);
    Save("score-audit.json",new{NativeWorlds=0,CpuSeconds=(Process.GetCurrentProcess().TotalProcessorTime-started).TotalSeconds,policy,loads,
        ForceBounds=new[]{24d,30,36,42,50,55}.Select(radius=>new{RadiusMeters=radius,AbsoluteUpperKph=3.6*Math.Sqrt(loads.TotalCapacityNewtons/147*radius),Demand90Newtons=147*625/radius,
            RequiredYaw90RadiansPerSecond=25/radius}),Audits=audits});
    Console.WriteLine("Saved score decomposition and finite action-space probes; native worlds=0.");return;
}

if(args.Length!=3)throw new ArgumentException("A frozen treatment list is required.");
// The saved-data phase must exist before any native reservation.
if(!File.Exists("docs/experiments/corner-speed-loss/saved-analysis.json")||!File.Exists(Path.Combine(output,"score-audit.json")))throw new IOException("Complete saved analysis and score audit first.");
var treatments=JsonSerializer.Deserialize<Treatment145[]>(File.ReadAllText(args[2]),json)!;
var ledgerPath=Path.Combine(output,"budget.json");
using var lease=new FileStream(Path.Combine(output,"budget.lock"),FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None);
var ledger=File.Exists(ledgerPath)?JsonNode.Parse(File.ReadAllText(ledgerPath))!.AsArray():new JsonArray();
if(ledger.Any(x=>!x!["Complete"]!.GetValue<bool>()))throw new IOException("Incomplete native attempt retained; inspect before any retry.");
foreach(var treatment in treatments){
    if(ledger.Any(x=>x!["Name"]!.GetValue<string>()==treatment.Name))continue;
    if(ledger.Count>=60||ledger.Sum(x=>x!["CpuSeconds"]!.GetValue<double>())>800)throw new IOException("Study budget reserve reached; preserve remaining final-run allowance.");
    var baseline=Read(treatment.Variant);var original=baseline.Riders[0];
    foreach(var change in treatment.Changes){change.Action.Validate();if(change.Time<0||Math.Abs(change.Time/.2-Math.Round(change.Time/.2))>1e-7)throw new ArgumentException("Intervene at native reviews only.");}
    var first=treatment.Autonomous?0:treatment.Changes.Select(x=>x.Time).DefaultIfEmpty(double.PositiveInfinity).Min();
    var reservation=new JsonObject{["Name"]=treatment.Name,["Complete"]=false,["CpuSeconds"]=0};ledger.Add(reservation);Save("budget.json",ledger);
    var started=Process.GetCurrentProcess().TotalProcessorTime;var checkpoints=new List<object>();
    Algorithm145.Logs.Clear();
    Decision144 Choose(FreeRacingInput input,CrankClutch144 shaft,RiderSpec spec,TrackFrame frame,TrackStateSnapshot surface,WorldParameters p,float mass,float inertia,string variant){
        if((Process.GetCurrentProcess().TotalProcessorTime-started).TotalSeconds>(treatment.Autonomous?180:75))throw new InvalidOperationException("Bounded study CPU reserve stop");
        var recorded=original.Decisions.FirstOrDefault(d=>Math.Abs(d.Choice.Time-input.Time)<1e-7);
        if(input.Time<first-1e-8||treatment.Changes.Length==0&&!treatment.Autonomous)return recorded??throw new IOException("Frozen prefix ended.");
        var change=treatment.Changes.FirstOrDefault(c=>Math.Abs(c.Time-input.Time)<1e-7);
        if(change is not null){
            var expected=original.Samples.MinBy(s=>Math.Abs(s.Input.Time-input.Time))!;
            var originalInput=expected.Input with{Time=input.Time,NextReview=input.Time,Pending=expected.Input.Pending.Where(c=>c.ObservedAt<input.Time-1e-8).ToArray()};
            var nativeInput=input with{NextReview=input.Time};
            checkpoints.Add(new{input.Time,NativeStateFingerprint=Hash(new{Input=nativeInput,Crank=shaft.Omega,shaft.Parameters,p,surface}),
                OriginalStateFingerprint=Hash(new{Input=originalInput,Crank=expected.CrankOmega,shaft.Parameters,p,surface}),
                NativeState=nativeInput,Crank=shaft.Omega,OriginalAction=recorded?.Choice.Action,Override=change.Action});
            return new(new(input.Time,change.Action,FreeActionSafety.Uncertain,false,Array.Empty<FreeLearningCandidate>(),0,0),0);
        }
        if(treatment.Autonomous)return Algorithm145.Choose(input,shaft,spec,frame,surface,p,mass,inertia,variant,treatment.Mode);
        return Policy144.Choose(input,shaft,spec,frame,surface,p,mass,inertia,variant);
    }
    Run144 run;using(var world=new DiagnosticWorld145(new(Enabled:true)))run=world.Run(treatment.Name,treatment.Variant,Choose);
    var raw=Path.Combine(output,treatment.Name+".json.gz");using(var z=new GZipStream(File.Create(raw),CompressionLevel.SmallestSize))JsonSerializer.Serialize(z,run,json);
    if(Algorithm145.Logs.Count>0)using(var z=new GZipStream(File.Create(Path.Combine(output,treatment.Name+"-plans.json.gz")),CompressionLevel.SmallestSize))JsonSerializer.Serialize(z,Algorithm145.Logs,json);
    var prefix=run.Riders[0].Samples.Where(s=>s.Input.Time<=first+1e-8).ToArray();
    var expectedPrefix=original.Samples.Where(s=>s.Input.Time<=first+1e-8).ToArray();
    var prefixEqual=prefix.Length==expectedPrefix.Length&&prefix.Zip(expectedPrefix).All(pair=>Hash(pair.First)==Hash(pair.Second));
    var result=new{Treatment=treatment,run.Outcome,run.EndSeconds,run.Fingerprint,FullOriginalFingerprint=baseline.Fingerprint,PrefixExact=prefixEqual,PrefixSamples=prefix.Length,
        PrefixFingerprint=Hash(prefix),OriginalPrefixFingerprint=Hash(expectedPrefix),Checkpoints=checkpoints,
        CompletedLaps=run.Riders[0].CompletedLaps,run.Crossings,run.TotalCpuSeconds,run.ControllerCpuSeconds,
        MinimumInner=run.Riders[0].Samples.Min(s=>s.Inner),MinimumOuter=run.Riders[0].Samples.Min(s=>s.Outer),
        MaximumBetaDegrees=run.Riders[0].Samples.Max(s=>Math.Abs(s.Input.Rider.SlideRadians)*180/Math.PI),
        RawSha256=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(raw))),CpuSeconds=(Process.GetCurrentProcess().TotalProcessorTime-started).TotalSeconds};
    Save(treatment.Name+"-result.json",result);reservation["Complete"]=true;reservation["CpuSeconds"]=result.CpuSeconds;Save("budget.json",ledger);
    Console.WriteLine(JsonSerializer.Serialize(new{treatment.Name,run.Outcome,CompletedLaps=result.CompletedLaps,run.EndSeconds,result.CpuSeconds,PrefixExact=prefixEqual}));
    if(!prefixEqual)throw new IOException("Native prefix mismatch: no causal comparison may be promoted.");
}

public sealed record Change145(double Time,FreeRacingAction Action);
public sealed record Treatment145(string Name,string Variant,Change145[] Changes,bool Autonomous=false,string Mode="");
public static class Algorithm145{
    public static readonly List<object> Logs=new();
    // Only called by an explicit research treatment. No production default.
    public static Decision144 Choose(FreeRacingInput input,CrankClutch144 shaft,RiderSpec spec,TrackFrame frame,TrackStateSnapshot surface,WorldParameters p,float mass,float inertia,string variant,string mode){
        if(mode!="two-command")throw new ArgumentException("Explicit bounded planning treatment required.");
        var started=Process.GetCurrentProcess().TotalProcessorTime;var policy=Controller143.Policy;var version=Policy144.Variant(variant);
        int contacts=0,forecasts=0;
        FreeRacingPrediction Predict(FreeRacingInput i,CrankClutch144 c,double h,FreeRacingAction? a=null,Action<double,double>? observe=null){
            var result=Predictor144.Predict(i,spec,frame,surface,p,mass,inertia,h,a,powertrain:c,observeCrank:observe);contacts+=result.ForceEvaluations;forecasts++;return result;
        }
        var held=FreeLearningController.Actions(input,policy,true).Select(action=>{
            var prediction=Predict(input,shaft,.9,action);var leaf=Objective142.FromPrediction(input,policy,frame,surface,prediction,action);var score=Controller143.Score(leaf,version);
            return(Leaf:leaf,Score:score.Score,Safety:score.Safety);
        }).ToArray();
        var order=held.OrderBy(x=>x.Safety).ThenBy(x=>x.Score).ThenBy(x=>Math.Abs(x.Leaf.Candidate.Action.SteeringRadians-input.Active.SteeringRadians)).ThenBy(x=>x.Leaf.Candidate.Action.SteeringRadians);
        // One algorithmic change: score a delayed two-command continuation instead
        // of assuming the first action remains held for the entire H09 horizon.
        // At most six roots/continuations, taken from the unchanged held grid.
        var roots=held.GroupBy(x=>x.Leaf.Candidate.Action.Drive).SelectMany(g=>g.OrderBy(x=>x.Safety).ThenBy(x=>x.Score).Take(2)).Select(x=>x.Leaf.Candidate.Action).ToArray();
        double omega=shaft.Omega;
        var prefix=Predict(input,shaft,.2,observe:(t,w)=>omega=w);var end=prefix.Points[^1];
        var rows=new List<(FreeLearningCandidate Candidate,FreeRacingAction Second,Terms142 Terms)>();
        foreach(var root in roots){
            var queue=input.Pending.Append(new FreeRacingCommand(input.Time,input.Time+RiderController.ReactionSeconds(spec),root)).OrderBy(c=>c.ActivationAt).ToArray();
            var active=queue.Where(c=>c.ActivationAt<=end.Time+1e-10).Select(c=>c.Action).LastOrDefault(input.Active);
            var future=new FreeRacingInput(end.Rider with{Drive=active.Drive},end.Time,end.Omega,end.Steering,active,queue.Where(c=>c.ActivationAt>end.Time+1e-10).ToArray(),end.Time);
            foreach(var second in roots){
                var tail=Predict(future,new CrankClutch144(shaft.Parameters,omega),.7,second);
                var prediction=new FreeRacingPrediction(prefix.Points.Concat(tail.Points.Skip(1)).ToArray(),prefix.ForceEvaluations+tail.ForceEvaluations,prefix.DistanceMeters+tail.DistanceMeters);
                var leaf=Objective142.FromPrediction(input,policy,frame,surface,prediction,root);var score=Controller143.Score(leaf,version);
                rows.Add((leaf.Candidate with{Score=score.Score,Safety=score.Safety},second,leaf.Terms));
            }
        }
        var best=rows.OrderBy(x=>x.Candidate.Safety).ThenBy(x=>x.Candidate.Score).ThenBy(x=>Math.Abs(x.Candidate.Action.SteeringRadians-input.Active.SteeringRadians)).ThenBy(x=>x.Candidate.Action.SteeringRadians).First();
        Logs.Add(new{input.Time,Original=order.First().Leaf.Candidate.Action,Chosen=best.Candidate.Action,ForecastSecond=best.Second,
            Scope="Only first command requested. Native controller observes/replans at +.2; second forecast action is not a script or a guarantee.",
            Candidates=rows.Select(x=>new{x.Candidate,x.Second,x.Terms})});
        return new(new(input.Time,best.Candidate.Action,best.Candidate.Safety,rows.All(r=>r.Candidate.Safety!=FreeActionSafety.SafeInHorizon),rows.Select(x=>x.Candidate).ToArray(),contacts,forecasts),
            (Process.GetCurrentProcess().TotalProcessorTime-started).TotalSeconds);
    }
}
